using System.Data;
using Microsoft.Data.SqlClient;
using BlazorReporting.Data.Models;
using BlazorReporting.Models;
using BlazorReporting.Services;

namespace BlazorReporting.Data;

public sealed class DataRepository : IDataRepository
{
    private readonly string _cs;
    private readonly ILogger<DataRepository> _log;
    private readonly IConfiguration _cfg;
    private readonly AuthService _auth;

    public DataRepository(IConfiguration cfg, ILogger<DataRepository> log, AuthService auth)
    {
        _cs  = cfg.GetConnectionString("DefaultConnection")
               ?? throw new InvalidOperationException("DefaultConnection not configured.");
        _cfg = cfg;
        _log = log;
        _auth = auth;
    }

    // Username hiện tại đăng nhập (dùng cho các SP cần @Username)
    private string CurrentUser =>
        !string.IsNullOrEmpty(_auth.UserName) ? _auth.UserName.ToUpper() : "SUPPORT";

    // ══════════════════════════════════════════════════════════════
    // FetchPagedAsync
    //   • SP  → stream ALL rows, page in memory (with progress)
    //   • View/Table → SQL OFFSET/FETCH (true server-side paging)
    // ══════════════════════════════════════════════════════════════

    public async Task<PagedResult<Dictionary<string, object?>>> FetchPagedAsync(
        DataSourceConfig config, CancellationToken ct = default)
    {
        if (config.SourceType == DataSourceType.StoredProcedure)
        {
            // Check whether SP has native @PageNumber / @PageSize params
            var spParams = await GetSpParametersAsync(config.Source, ct);
            bool hasNativePaging =
                spParams.Any(p => p.Name.Equals("PageNumber", StringComparison.OrdinalIgnoreCase)) &&
                spParams.Any(p => p.Name.Equals("PageSize",   StringComparison.OrdinalIgnoreCase));

            if (hasNativePaging)
                return await ExecuteSpPagedNativeAsync(config, ct);

            // Fallback: load all, slice in memory
            var all = await FetchAllAsync(config, ct: ct);
            return SliceInMemory(all, config);
        }

        return await ExecuteSqlPagedAsync(config, ct);
    }

    // ══════════════════════════════════════════════════════════════
    // FetchAllAsync  (SP full load with streaming + progress)
    // ══════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<Dictionary<string, object?>>> FetchAllAsync(
        DataSourceConfig config,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        if (config.SourceType != DataSourceType.StoredProcedure)
            return await ExecuteSqlAllAsync(config, progress, ct);

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = config.Source;
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 300;
        foreach (var (k, v) in config.Parameters)
            cmd.Parameters.AddWithValue($"@{k}", v ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess, ct);

        return await MaterializeAsync(reader, progress, ct);
    }

    // ══════════════════════════════════════════════════════════════
    // FetchPivotGroupByAsync — SQL-side GROUP BY for View/Table
    // Avoids loading all rows into memory for pivot on large sources.
    // ══════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<Dictionary<string, object?>>> FetchPivotGroupByAsync(
        DataSourceConfig config,
        string rowField,
        IReadOnlyList<string> colFields,
        IReadOnlyList<PivotValueDef> valueDefs,
        CancellationToken ct = default)
    {
        var src  = $"[{Esc(config.Source)}]";
        var rCol = $"[{Esc(rowField)}]";

        // Support 1..N column fields
        var cCols = colFields.Select(f => $"[{Esc(f)}]").ToList();

        // Build aggregate columns — alias = vd.Label so PivotService can locate by name
        var aggCols = valueDefs.Select(vd =>
            $"{ToSqlAgg(vd.Aggregation)}([{Esc(vd.Field)}]) AS [{Esc(vd.Label)}]");

        var (whereClause, filterParms) = BuildWhereClause(config.Filters);

        var selectCols  = string.Join(", ", cCols);
        var groupByPart = string.Join(", ", cCols);

        var sql = $"""
            SELECT {rCol}, {selectCols}, {string.Join(", ", aggCols)}
            FROM   {src}
            {whereClause}
            GROUP  BY {rCol}, {groupByPart}
            ORDER  BY {rCol}, {groupByPart}
            """;

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText    = sql;
        cmd.CommandTimeout = 120;
        ApplyParams(cmd, filterParms);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await MaterializeAsync(reader, null, ct);
    }

    // ══════════════════════════════════════════════════════════════
    // GetColumnsAsync
    // ══════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<string>> GetColumnsAsync(
        string source, DataSourceType sourceType, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        var sql = sourceType == DataSourceType.StoredProcedure
            ? """
              SELECT c.name
              FROM   sys.all_objects  o
              INNER  JOIN sys.all_columns c ON c.object_id = o.object_id
              WHERE  o.name = @source AND o.type = 'P'
              ORDER  BY c.column_id
              """
            : """
              SELECT COLUMN_NAME
              FROM   INFORMATION_SCHEMA.COLUMNS
              WHERE  TABLE_NAME = @source
              ORDER  BY ORDINAL_POSITION
              """;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@source", source);

        var cols = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            cols.Add(reader.GetString(0));
        return cols;
    }

    // ══════════════════════════════════════════════════════════════
    // GetSpParametersAsync
    // ══════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<SpParameter>> GetSpParametersAsync(
        string spName, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT p.name, t.name AS type_name, p.has_default_value
            FROM   sys.parameters p
            INNER  JOIN sys.types t ON t.user_type_id = p.user_type_id
            WHERE  OBJECT_NAME(p.object_id) = @spName AND p.parameter_id > 0
            ORDER  BY p.parameter_id
            """;
        cmd.Parameters.AddWithValue("@spName", spName);

        var result = new List<SpParameter>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(new SpParameter(
                reader.GetString(0).TrimStart('@'),
                reader.GetString(1),
                reader.GetBoolean(2)));
        return result;
    }

    // ══════════════════════════════════════════════════════════════
    // FetchDrillDownAsync
    // ══════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<Dictionary<string, object?>>> FetchDrillDownAsync(
        DataSourceConfig config,
        Dictionary<string, object?> drillFilters,
        CancellationToken ct = default)
    {
        // For View/Table: push drill filters to SQL WHERE (fast)
        if (config.SourceType != DataSourceType.StoredProcedure)
        {
            var drillConfig = new DataSourceConfig
            {
                Source      = config.Source,
                SourceType  = config.SourceType,
                Parameters  = config.Parameters,
                PageNumber  = 1,
                PageSize    = 10_000,
                Filters     = drillFilters
                    .Where(kv => kv.Value != null)
                    .ToDictionary(kv => kv.Key, kv => kv.Value!.ToString()!,
                        StringComparer.OrdinalIgnoreCase)
            };
            var paged = await ExecuteSqlPagedAsync(drillConfig, ct);
            return paged.Items.ToList();
        }

        // For SP: filter the in-memory cached data
        var all = await FetchAllAsync(config, ct: ct);
        return all.Where(row =>
            drillFilters.All(kv =>
                row.TryGetValue(kv.Key, out var v) &&
                string.Equals(v?.ToString(), kv.Value?.ToString(),
                    StringComparison.OrdinalIgnoreCase))
        ).ToList();
    }

    // ══════════════════════════════════════════════════════════════
    // Private — SP with native paging params (@PageNumber / @PageSize)
    // ══════════════════════════════════════════════════════════════

    private async Task<PagedResult<Dictionary<string, object?>>> ExecuteSpPagedNativeAsync(
        DataSourceConfig config, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        // Count call (SP with @PageSize=0 convention, or separate count SP)
        // Simpler: run with PageNumber=1,PageSize=1 just for the total,
        // then run real page. Many SPs return total as OUTPUT or last column.
        // Here we do two calls: count (PageSize=MAX_INT) then slice.
        // Override for true count:
        var countParams = new Dictionary<string, object?>(config.Parameters)
        {
            ["PageNumber"] = 1,
            ["PageSize"]   = int.MaxValue
        };
        var countConfig = new DataSourceConfig
        {
            Source = config.Source, SourceType = DataSourceType.StoredProcedure,
            Parameters = countParams
        };
        // Count is expensive on SPs — just run real page, report -1 total
        // unless the SP supports COUNT param. Return actual page only.
        using var cmd = conn.CreateCommand();
        cmd.CommandText = config.Source;
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandTimeout = 300;

        var pagedParams = new Dictionary<string, object?>(config.Parameters)
        {
            ["PageNumber"] = config.PageNumber,
            ["PageSize"]   = config.PageSize
        };
        foreach (var (k, v) in pagedParams)
            cmd.Parameters.AddWithValue($"@{k}", v ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
        var items = await MaterializeAsync(reader, null, ct);

        return new PagedResult<Dictionary<string, object?>>
        {
            Items      = items,
            TotalCount = -1,   // unknown without a second count call
            PageNumber = config.PageNumber,
            PageSize   = config.PageSize
        };
    }

    // ══════════════════════════════════════════════════════════════
    // Private — View / Table paged (OFFSET / FETCH)
    // ══════════════════════════════════════════════════════════════

    private async Task<PagedResult<Dictionary<string, object?>>> ExecuteSqlPagedAsync(
        DataSourceConfig config, CancellationToken ct)
    {
        var (whereClause, filterParms) = BuildWhereClause(config.Filters);

        var orderClause = string.IsNullOrWhiteSpace(config.SortColumn)
            ? "(SELECT NULL)"
            : $"[{Esc(config.SortColumn)}] {(config.SortDescending ? "DESC" : "ASC")}";

        var src    = $"[{Esc(config.Source)}]";
        var offset = (config.PageNumber - 1) * config.PageSize;

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        // Row count (fast — uses COUNT_BIG to avoid int overflow)
        using var countCmd = conn.CreateCommand();
        countCmd.CommandText    = $"SELECT COUNT_BIG(1) FROM {src} {whereClause}";
        countCmd.CommandTimeout = 60;
        ApplyParams(countCmd, filterParms);
        var total = (long)(await countCmd.ExecuteScalarAsync(ct))!;

        // Data page
        using var dataCmd = conn.CreateCommand();
        dataCmd.CommandText = $"""
            SELECT * FROM {src}
            {whereClause}
            ORDER BY {orderClause}
            OFFSET {offset} ROWS FETCH NEXT {config.PageSize} ROWS ONLY
            """;
        dataCmd.CommandTimeout = 120;
        ApplyParams(dataCmd, filterParms);

        await using var reader = await dataCmd.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess, ct);
        var items = await MaterializeAsync(reader, null, ct);

        return new PagedResult<Dictionary<string, object?>>
        {
            Items      = items,
            TotalCount = (int)Math.Min(total, int.MaxValue),
            PageNumber = config.PageNumber,
            PageSize   = config.PageSize
        };
    }

    // ══════════════════════════════════════════════════════════════
    // Private — View / Table full load (for export / in-memory pivot)
    // ══════════════════════════════════════════════════════════════

    private async Task<List<Dictionary<string, object?>>> ExecuteSqlAllAsync(
        DataSourceConfig config,
        IProgress<int>? progress,
        CancellationToken ct)
    {
        var src = $"[{Esc(config.Source)}]";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText    = $"SELECT * FROM {src}";
        cmd.CommandTimeout = 300;
        await using var reader = await cmd.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess, ct);
        return await MaterializeAsync(reader, progress, ct);
    }

    // ══════════════════════════════════════════════════════════════
    // Materialize SqlDataReader → List<Dictionary>  (streaming)
    // ══════════════════════════════════════════════════════════════

    private static async Task<List<Dictionary<string, object?>>> MaterializeAsync(
        SqlDataReader reader,
        IProgress<int>? progress,
        CancellationToken ct)
    {
        // NOTE: SequentialAccess requires reading columns in order.
        // We switch to Default for dictionary materialisation so we can
        // access columns by name in any order after reading the schema.
        var schema  = reader.GetColumnSchema();
        var results = new List<Dictionary<string, object?>>();
        int count   = 0;

        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>(schema.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var col in schema)
            {
                var raw = reader[col.ColumnName];
                row[col.ColumnName] = raw == DBNull.Value ? null : raw;
            }
            results.Add(row);

            if (++count % 2_000 == 0)
                progress?.Report(count);
        }

        progress?.Report(count);
        return results;
    }

    // ══════════════════════════════════════════════════════════════
    // In-memory page slice (for SP results)
    // ══════════════════════════════════════════════════════════════

    private static PagedResult<Dictionary<string, object?>> SliceInMemory(
        IReadOnlyList<Dictionary<string, object?>> all,
        DataSourceConfig config)
    {
        var q = all.AsEnumerable();

        foreach (var (col, term) in config.Filters)
            if (!string.IsNullOrEmpty(term))
                q = q.Where(r =>
                    r.TryGetValue(col, out var v) &&
                    v?.ToString()?.Contains(term, StringComparison.OrdinalIgnoreCase) == true);

        if (!string.IsNullOrEmpty(config.SortColumn))
            q = config.SortDescending
                ? q.OrderByDescending(r => r.TryGetValue(config.SortColumn, out var v) ? v : null,
                    NullSafeComparer.Instance)
                : q.OrderBy(r => r.TryGetValue(config.SortColumn, out var v) ? v : null,
                    NullSafeComparer.Instance);

        var list = q.ToList();
        var page = list
            .Skip((config.PageNumber - 1) * config.PageSize)
            .Take(config.PageSize)
            .ToList();

        return new PagedResult<Dictionary<string, object?>>
        {
            Items      = page,
            TotalCount = list.Count,
            PageNumber = config.PageNumber,
            PageSize   = config.PageSize
        };
    }

    // ══════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════

    private static (string clause, Dictionary<string, object?> parms) BuildWhereClause(
        Dictionary<string, string> filters)
    {
        if (filters.Count == 0) return (string.Empty, new());

        var conditions = new List<string>();
        var parms      = new Dictionary<string, object?>();
        int i = 0;

        foreach (var (col, term) in filters)
        {
            if (string.IsNullOrWhiteSpace(term)) continue;
            var p = $"@fw{i++}";
            conditions.Add($"CAST([{Esc(col)}] AS NVARCHAR(MAX)) LIKE {p}");
            parms[p] = $"%{term}%";
        }

        var clause = conditions.Count > 0
            ? "WHERE " + string.Join(" AND ", conditions)
            : string.Empty;
        return (clause, parms);
    }

    private static void ApplyParams(SqlCommand cmd, Dictionary<string, object?> parms)
    {
        foreach (var (k, v) in parms)
            cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
    }

    private static string Esc(string name) => name.Replace("]", "]]");

    private static string ToSqlAgg(AggregationType agg) => agg switch
    {
        AggregationType.Count => "COUNT",
        AggregationType.Avg   => "AVG",
        AggregationType.Min   => "MIN",
        AggregationType.Max   => "MAX",
        _                     => "SUM"
    };

    // ══════════════════════════════════════════════════════════════
    // GetSalesmanRouteAsync — toàn bộ điểm check-in của 1 SM trong ngày
    // ══════════════════════════════════════════════════════════════
    public async Task<IReadOnlyList<SalesmanLocation>> GetSalesmanRouteAsync(
        string userName, DateTime date, CancellationToken ct = default)
    {
        // SalesmanVisit lưu từng điểm GPS theo thứ tự thời gian
        const string sql = """
            SELECT SalesmanID AS UserName,
                   VisitTime  AS Checktime,
                   CONVERT(decimal(18,6), Latitude)   AS Lattitude,
                   CONVERT(decimal(18,6), Longtitude) AS Longtitude
            FROM SalesmanVisit
            WHERE SalesmanID = @UserName
              AND CONVERT(date, VisitDate) = @Date
              AND TRY_CAST(Latitude   AS float) > 0
              AND TRY_CAST(Longtitude AS float) > 0
            ORDER BY VisitTime ASC
            """;

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText    = sql;
        cmd.CommandTimeout = 30;
        cmd.Parameters.AddWithValue("@UserName", userName);
        cmd.Parameters.AddWithValue("@Date",     date.Date);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<SalesmanLocation>();

        while (await reader.ReadAsync(ct))
        {
            var lat = Convert.ToDouble(reader["Lattitude"]);
            var lng = Convert.ToDouble(reader["Longtitude"]);
            if (lat < -90 || lat > 90 || lng < -180 || lng > 180) continue;

            results.Add(new SalesmanLocation(
                UserName  : reader["UserName"].ToString()!,
                Checktime : Convert.ToDateTime(reader["Checktime"]),
                Lattitude : lat,
                Longtitude: lng));
        }
        return results;
    }

    // ══════════════════════════════════════════════════════════════
    // GetSalesmanLocationsAsync
    //   Vị trí cuối cùng trong ngày — dùng pp_GetSalemanLastLocation
    //   giống DMS2.0 TrackingController
    // ══════════════════════════════════════════════════════════════
    public async Task<IReadOnlyList<SalesmanLocation>> GetSalesmanLocationsAsync(
        DateTime date, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandType    = CommandType.StoredProcedure;
        cmd.CommandText    = "pp_GetSalemanLastLocation";
        cmd.CommandTimeout = 60;
        cmd.Parameters.AddWithValue("@Username",     CurrentUser);
        cmd.Parameters.AddWithValue("@SalesupID",    "");
        cmd.Parameters.AddWithValue("@DistributorID", 0);
        cmd.Parameters.AddWithValue("@SalesmanID",   "");
        cmd.Parameters.AddWithValue("@Date",         date.Date);
        cmd.Parameters.AddWithValue("@Time",         0);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<SalesmanLocation>();

        while (await reader.ReadAsync(ct))
        {
            // SP trả Latitude/Longtitude — vị trí cuối ngày
            var latObj = reader["Latitude"];
            var lngObj = reader["Longtitude"];
            if (latObj is DBNull || lngObj is DBNull) continue;

            var lat = Convert.ToDouble(latObj);
            var lng = Convert.ToDouble(lngObj);
            if (lat is 0 && lng is 0) continue;
            if (lat < -90 || lat > 90 || lng < -180 || lng > 180) continue;

            // LastSyncTime là thời điểm cuối cùng đồng bộ
            var timeObj = reader["LastSyncTime"];
            var checktime = timeObj is DBNull
                ? date.Date
                : Convert.ToDateTime(timeObj);

            results.Add(new SalesmanLocation(
                UserName  : reader["SalesmanID"].ToString()!,
                Checktime : checktime,
                Lattitude : lat,
                Longtitude: lng));
        }

        return results;
    }

    // ══════════════════════════════════════════════════════════════
    // GetSalesmanRouteWithSalesAsync
    //   Giống DMS2.0: pp_GetVisitSMTracking (GPS tracking từng outlet)
    //   RouteCD + DistributorID lấy từ VisitPlanHistory hoặc OrderHeader
    // ══════════════════════════════════════════════════════════════
    public async Task<IReadOnlyList<SalesmanRoutePoint>> GetSalesmanRouteWithSalesAsync(
        string userName, DateTime date, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        // Bước 1: lấy RouteCD + DistributorID (từ VisitPlanHistory, fallback OrderHeader)
        string? routeCD = null;
        int? distributorID = null;

        const string routeSql = """
            SELECT TOP 1 RouteID AS RouteCD, DistributorID
            FROM VisitPlanHistory
            WHERE CONVERT(date, VisitDate) = @Date AND SalesmanID = @SM
            UNION ALL
            SELECT TOP 1 RouteCD, DistributorID
            FROM OrderHeader
            WHERE CONVERT(date, VisitDate) = @Date AND SalesmanID = @SM
            """;

        using (var rc = conn.CreateCommand())
        {
            rc.CommandText    = routeSql;
            rc.CommandTimeout = 15;
            rc.Parameters.AddWithValue("@Date", date.Date);
            rc.Parameters.AddWithValue("@SM",   userName);
            await using var rr = await rc.ExecuteReaderAsync(ct);
            if (await rr.ReadAsync(ct))
            {
                routeCD      = rr["RouteCD"]?.ToString();
                distributorID= rr["DistributorID"] is DBNull ? null : Convert.ToInt32(rr["DistributorID"]);
            }
        }

        // Bước 2: load OrderHeader để có tên outlet, doanh số, thời gian
        var orderMap = new Dictionary<string, (string Name, string Address, string Route, decimal Amt, DateTime Start, DateTime? End)>(
            StringComparer.OrdinalIgnoreCase);
        try
        {
            const string ohSql = """
                SELECT h.OutletID,
                       ISNULL(o.OutletName, h.OutletID) AS OutletName,
                       ISNULL(o.Address,'')             AS Address,
                       ISNULL(h.RouteCD,'')             AS RouteCD,
                       ISNULL(h.TotalAmt, 0)            AS TotalAmt,
                       h.StartTime,
                       h.EndTime
                FROM   OrderHeader h
                LEFT JOIN Outlets o ON o.OutletID = h.OutletID
                WHERE  h.SalesmanID = @User
                  AND  CONVERT(date, h.VisitDate) = @Date
                """;
            using var oc = conn.CreateCommand();
            oc.CommandText = ohSql; oc.CommandTimeout = 15;
            oc.Parameters.AddWithValue("@User", userName);
            oc.Parameters.AddWithValue("@Date", date.Date);
            await using var or = await oc.ExecuteReaderAsync(ct);
            while (await or.ReadAsync(ct))
            {
                var oid   = or["OutletID"].ToString()!;
                var start = or["StartTime"] is DBNull ? date : Convert.ToDateTime(or["StartTime"]);
                var end   = or["EndTime"]   is DBNull ? (DateTime?)null : Convert.ToDateTime(or["EndTime"]);
                orderMap[oid] = (or["OutletName"].ToString()!, or["Address"].ToString()!,
                                 or["RouteCD"].ToString()!, Convert.ToDecimal(or["TotalAmt"]), start, end);
            }
        }
        catch { /* optional */ }

        // Bước 3: pp_GetVisitSMTracking — GPS tracking tại từng outlet (giống DMS2.0)
        var result = new List<SalesmanRoutePoint>();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandType    = CommandType.StoredProcedure;
            cmd.CommandText    = "pp_GetVisitSMTracking";
            cmd.CommandTimeout = 60;
            cmd.Parameters.AddWithValue("@RouteCD",      string.IsNullOrEmpty(routeCD) ? DBNull.Value : (object)routeCD);
            cmd.Parameters.AddWithValue("@DistributorID",distributorID.HasValue ? (object)distributorID.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@VisitDate",    date.Date);
            cmd.Parameters.AddWithValue("@UserName",     CurrentUser);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var smId = r["SalesmanID"]?.ToString() ?? "";
                if (!smId.Equals(userName, StringComparison.OrdinalIgnoreCase)) continue;

                var lat = r["Latitude"]   is DBNull ? 0d : Convert.ToDouble(r["Latitude"]);
                var lng = r["Longtitude"] is DBNull ? 0d : Convert.ToDouble(r["Longtitude"]);
                if (lat == 0 && lng == 0) continue;
                if (lat < -90 || lat > 90) continue;

                DateTime checktime = date;
                if (r["StartTime"] is not DBNull && TimeSpan.TryParse(r["StartTime"].ToString(), out var t))
                    checktime = date.Date + t;

                CustomerVisit? visit = null;
                if (r["OutletID"] is not DBNull)
                {
                    var oid = r["OutletID"].ToString()!;
                    orderMap.TryGetValue(oid, out var info);
                    visit = new CustomerVisit(
                        CustomerCD  : oid,
                        LocationName: info.Name    ?? oid,
                        RouteCode   : info.Route   ?? routeCD ?? "",
                        OrderAmount : info.Amt,
                        OrderDate   : info.Start != default ? info.Start : checktime,
                        EndTime     : info.End,
                        Address     : info.Address ?? "");
                }

                result.Add(new SalesmanRoutePoint(userName, checktime, lat, lng, visit));
            }
        }
        catch { /* fallback bên dưới */ }

        if (result.Count > 0)
            return result.OrderBy(p => p.Checktime).ToList();

        // Fallback: SalesmanVisit GPS tracking
        const string gpsSql = """
            SELECT CONVERT(decimal(18,6), Latitude)   AS Lat,
                   CONVERT(decimal(18,6), Longtitude) AS Lng,
                   VisitTime AS Checktime
            FROM   SalesmanVisit
            WHERE  SalesmanID = @User
              AND  CONVERT(date, VisitDate) = @Date
              AND  TRY_CAST(Latitude   AS float) > 0
              AND  TRY_CAST(Longtitude AS float) > 0
            ORDER BY VisitTime ASC
            """;

        using var cmd2 = conn.CreateCommand();
        cmd2.CommandText    = gpsSql;
        cmd2.CommandTimeout = 30;
        cmd2.Parameters.AddWithValue("@User", userName);
        cmd2.Parameters.AddWithValue("@Date", date.Date);

        await using var r2 = await cmd2.ExecuteReaderAsync(ct);
        while (await r2.ReadAsync(ct))
        {
            var lat = Convert.ToDouble(r2["Lat"]);
            var lng = Convert.ToDouble(r2["Lng"]);
            if (lat < -90 || lat > 90) continue;
            result.Add(new SalesmanRoutePoint(userName,
                Convert.ToDateTime(r2["Checktime"]), lat, lng));
        }
        return result;
    }

    // ══════════════════════════════════════════════════════════════
    // GetDailySalesBySmAsync — tổng doanh số theo SM trong ngày
    // ══════════════════════════════════════════════════════════════
    public async Task<IReadOnlyDictionary<string, decimal>> GetDailySalesBySmAsync(
        DateTime date, CancellationToken ct = default)
    {
        const string sql = """
            SELECT SalesmanID AS UserName, SUM(TotalAmt) AS TotalAmount
            FROM   OrderHeader
            WHERE  CONVERT(date, VisitDate) = @Date
            GROUP  BY SalesmanID
            """;

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText    = sql;
        cmd.CommandTimeout = 30;
        cmd.Parameters.AddWithValue("@Date", date.Date);

        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            result[r["UserName"].ToString()!] = Convert.ToDecimal(r["TotalAmount"]);

        return result;
    }

    // ══════════════════════════════════════════════════════════════
    // GetSalesByAreaAsync — doanh số nhóm theo tuyến / khu vực
    // ══════════════════════════════════════════════════════════════
    public async Task<IReadOnlyList<SalesAreaItem>> GetSalesByAreaAsync(
        DateTime date, string groupBy = "route", CancellationToken ct = default)
    {
        // Đọc từ RPT_DailySales — bảng aggregate nhanh
        var (groupCol, labelFallback) = groupBy switch
        {
            "sm"       => ("SalesmanID",                           "(SM)"),
            "province" => ("ISNULL(STLvl2CD,'(Khác)')",           "(Khác)"),
            _          => ("ISNULL(RouteCD,'(Khác)')",             "(Tuyến)")
        };

        var sql = $"""
            SELECT {groupCol} AS Label,
                   SUM(TotalAmt)                AS TotalAmount,
                   SUM(OrderCount)              AS OrderCount,
                   COUNT(DISTINCT SalesmanID)   AS SmCount
            FROM   RPT_DailySales
            WHERE  VisitDate = @Date
            GROUP  BY {groupCol}
            ORDER  BY TotalAmount DESC
            """;

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText    = sql;
        cmd.CommandTimeout = 10;
        cmd.Parameters.AddWithValue("@Date", date.Date);

        var result = new List<SalesAreaItem>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            result.Add(new SalesAreaItem(
                Label      : r["Label"]?.ToString() ?? labelFallback,
                TotalAmount: Convert.ToDecimal(r["TotalAmount"]),
                OrderCount : Convert.ToInt32(r["OrderCount"]),
                SmCount    : Convert.ToInt32(r["SmCount"])));

        // Fallback sang pp_VisitOrderInDay nếu chưa sync
        if (result.Count == 0)
        {
            await using var cmd2 = conn.CreateCommand();
            cmd2.CommandType    = CommandType.StoredProcedure;
            cmd2.CommandText    = "pp_VisitOrderInDay";
            cmd2.CommandTimeout = 60;
            cmd2.Parameters.AddWithValue("@VisitDate", date.Date);
            cmd2.Parameters.AddWithValue("@UserName",  CurrentUser);

            var raw = new List<(string Route, string Sm, string Lvl2, decimal Amt, int Orders)>();
            await using var r2 = await cmd2.ExecuteReaderAsync(ct);
            while (await r2.ReadAsync(ct))
            {
                var amt = r2["TotalAmount"] is DBNull ? 0m : Convert.ToDecimal(r2["TotalAmount"]);
                if (amt <= 0) continue;
                raw.Add((
                    r2["RouteCD"]?.ToString()    ?? "(Khác)",
                    r2["SalesmanCD"]?.ToString() ?? "(Khác)",
                    r2["STLvl2Name"]?.ToString() ?? "(Khác)",
                    amt,
                    r2["OrderCount"] is DBNull ? 0 : Convert.ToInt32(r2["OrderCount"])
                ));
            }

            var grouped = groupBy switch
            {
                "sm"       => raw.GroupBy(x => x.Sm),
                "province" => raw.GroupBy(x => x.Lvl2),
                _          => raw.GroupBy(x => x.Route)
            };

            result = grouped
                .Select(g => new SalesAreaItem(g.Key, g.Sum(x => x.Amt), g.Sum(x => x.Orders), g.Select(x => x.Sm).Distinct().Count()))
                .OrderByDescending(x => x.TotalAmount)
                .ToList();
        }

        return result;
    }

    // ══════════════════════════════════════════════════════════════
    // GetSmsByGroupAsync — UserNames thuộc nhóm cụ thể
    // ══════════════════════════════════════════════════════════════
    public async Task<IReadOnlyList<string>> GetSmsByGroupAsync(
        DateTime date, string groupBy, string groupLabel, CancellationToken ct = default)
    {
        var (groupCol, _) = groupBy switch
        {
            "sm"       => ("h.SalesmanID",              ""),
            "province" => ("ISNULL(o.City,'(Khác)')",  ""),
            _          => ("ISNULL(h.RouteCD,'(Khác)')","")
        };

        var sql = $"""
            SELECT DISTINCT h.SalesmanID AS UserName
            FROM  OrderHeader h
            LEFT JOIN Outlets o ON o.OutletID = h.OutletID
            WHERE CONVERT(date, h.VisitDate) = @Date
              AND {groupCol} = @Label
            """;

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText    = sql;
        cmd.CommandTimeout = 15;
        cmd.Parameters.AddWithValue("@Date",  date.Date);
        cmd.Parameters.AddWithValue("@Label", groupLabel);

        var result = new List<string>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            result.Add(r.GetString(0));
        return result;
    }

    // ══════════════════════════════════════════════════════════════
    // GetProductSalesByAreaAsync
    //   Top N sản phẩm × khu vực trong kỳ chọn — dùng view DMSOrder
    //   Columns: InventoryCD, InventoryName, ProvinceID, AreaLabel,
    //            TotalQty, TotalAmount, OrderCount, OrderDate
    // ══════════════════════════════════════════════════════════════
    public async Task<IReadOnlyList<ProductAreaItem>> GetProductSalesByAreaAsync(
        int year, int? quarter, int? month,
        string groupBy = "route", int topN = 10,
        CancellationToken ct = default)
    {
        var (dateFrom, dateTo) = BuildDateRange(year, quarter, month);

        // Đọc từ RPT_ProductSales — bảng aggregate theo tháng, cực nhanh
        var rptAreaExpr = groupBy switch
        {
            "sm" => "SalesmanID",
            _    => "ISNULL(DistributorCode, N'(Khác)')"
        };

        var sql = $"""
            ;WITH TopProds AS (
                SELECT TOP (@TopN) InventoryCD
                FROM   RPT_ProductSales
                WHERE  SalesYear * 100 + SalesMonth >= {dateFrom:yyyyMM}
                  AND  SalesYear * 100 + SalesMonth <= {dateTo.AddMonths(-1):yyyyMM}
                GROUP  BY InventoryCD
                ORDER  BY SUM(OrderQty) DESC
            )
            SELECT
                p.InventoryCD,
                MAX(ISNULL(p.InventoryName, p.InventoryCD)) AS InventoryName,
                {rptAreaExpr}                                AS AreaLabel,
                SUM(p.OrderQty)                             AS TotalQty,
                SUM(p.LineAmt)                              AS TotalAmount,
                SUM(p.OrderCount)                           AS OrderCount
            FROM   RPT_ProductSales p
            INNER  JOIN TopProds tp ON p.InventoryCD = tp.InventoryCD
            WHERE  p.SalesYear * 100 + p.SalesMonth >= {dateFrom:yyyyMM}
              AND  p.SalesYear * 100 + p.SalesMonth <= {dateTo.AddMonths(-1):yyyyMM}
            GROUP  BY p.InventoryCD, {rptAreaExpr}
            ORDER  BY TotalQty DESC
            """;

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText    = sql;
        cmd.CommandTimeout = 15;
        cmd.Parameters.AddWithValue("@TopN", topN);

        var result = new List<ProductAreaItem>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            result.Add(new ProductAreaItem(
                InventoryCD  : r["InventoryCD"].ToString()!,
                InventoryName: r["InventoryName"]?.ToString() ?? r["InventoryCD"].ToString()!,
                AreaLabel    : r["AreaLabel"]?.ToString() ?? "(Khác)",
                TotalQty     : Convert.ToInt64(r["TotalQty"]),
                TotalAmount  : Convert.ToDecimal(r["TotalAmount"]),
                OrderCount   : Convert.ToInt32(r["OrderCount"])));

        return result;
    }

    // ══════════════════════════════════════════════════════════════
    // GetProductTrendAsync
    //   Dữ liệu theo tháng của các sản phẩm chọn (để vẽ xu hướng)
    // ══════════════════════════════════════════════════════════════
    public async Task<IReadOnlyList<ProductTrendPoint>> GetProductTrendAsync(
        IReadOnlyList<string> products, int historyMonths = 12,
        CancellationToken ct = default)
    {
        if (products.Count == 0) return [];

        // Build IN clause động
        var paramNames = products.Select((_, i) => $"@p{i}").ToList();
        var inClause   = string.Join(",", paramNames);

        var trendFrom  = DateTime.Today.AddMonths(-historyMonths);
        int fromYearMo = trendFrom.Year * 100 + trendFrom.Month;

        // Đọc từ RPT_ProductSales — aggregate theo tháng, không scan 56M rows
        var sql = $"""
            SELECT
                p.InventoryCD,
                MAX(ISNULL(p.InventoryName, p.InventoryCD)) AS InventoryName,
                p.SalesYear  AS Yr,
                p.SalesMonth AS Mo,
                SUM(p.OrderQty)  AS TotalQty,
                SUM(p.LineAmt)   AS TotalAmount
            FROM RPT_ProductSales p
            WHERE p.InventoryCD IN ({inClause})
              AND p.SalesYear * 100 + p.SalesMonth >= {fromYearMo}
            GROUP BY p.InventoryCD, p.SalesYear, p.SalesMonth
            ORDER BY p.InventoryCD, Yr, Mo
            """;

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText    = sql;
        cmd.CommandTimeout = 10;
        for (int i = 0; i < products.Count; i++)
            cmd.Parameters.AddWithValue($"@p{i}", products[i]);

        var result = new List<ProductTrendPoint>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            result.Add(new ProductTrendPoint(
                InventoryCD  : r["InventoryCD"].ToString()!,
                InventoryName: r["InventoryName"]?.ToString() ?? r["InventoryCD"].ToString()!,
                Year         : Convert.ToInt32(r["Yr"]),
                Month        : Convert.ToInt32(r["Mo"]),
                TotalQty     : Convert.ToInt64(r["TotalQty"]),
                TotalAmount  : Convert.ToDecimal(r["TotalAmount"])));

        return result;
    }

    /// <summary>
    /// Tính ngày bắt đầu / ngày kết thúc (exclusive) của kỳ báo cáo.
    /// Dùng range thay vì YEAR()/MONTH() để tận dụng index trên OrderDate.
    /// </summary>
    // ══════════════════════════════════════════════════════════════
    // GetProvinceSalesHistoryAsync
    //   Lịch sử tháng × Tỉnh/TP × Sản phẩm từ view DMSOrder.
    //   year/quarter/month → xác định mốc cuối kỳ (toDate).
    //   historyMonths     → lùi từ toDate để lấy khoảng train.
    // ══════════════════════════════════════════════════════════════
    public async Task<IReadOnlyList<ProvinceSalesHistory>> GetProvinceSalesHistoryAsync(
        int year, int? quarter, int? month,
        int historyMonths = 12, int topProducts = 20,
        CancellationToken ct = default)
    {
        // Tính mốc cuối kỳ từ period đã chọn
        var (_, periodEnd) = BuildDateRange(year, quarter, month);
        // Lùi historyMonths tháng từ cuối kỳ → đầu cửa sổ train
        var fromDate = periodEnd.AddMonths(-historyMonths).Date;

        const string sql = """
            ;WITH TopProds AS (
                SELECT TOP (@TopProducts) InventoryCD
                FROM   OrderDetail WITH (NOLOCK)
                WHERE  VisitDate >= @FromDate AND VisitDate < @ToDate
                GROUP  BY InventoryCD
                ORDER  BY SUM(OrderQty) DESC
            )
            SELECT
                ISNULL(ou.City, N'(Khác)')                  AS ProvinceCode,
                d.InventoryCD,
                MAX(ISNULL(d.InventoryName, d.InventoryCD)) AS InventoryName,
                YEAR(d.VisitDate)                           AS Yr,
                MONTH(d.VisitDate)                          AS Mo,
                SUM(d.OrderQty)                             AS TotalQty,
                SUM(d.LineAmt)                              AS TotalAmount,
                COUNT(DISTINCT d.OrderCode)                 AS OrderCount
            FROM   OrderDetail d WITH (NOLOCK)
            INNER  JOIN TopProds tp ON d.InventoryCD = tp.InventoryCD
            LEFT   JOIN OrderHeader h WITH (NOLOCK)
                   ON  h.SalesmanID = d.SalesmanID
                   AND h.VisitDate  = d.VisitDate
                   AND h.Code       = d.OrderCode
            LEFT   JOIN Outlets ou ON ou.OutletID = h.OutletID
            WHERE  d.VisitDate >= @FromDate AND d.VisitDate < @ToDate
            GROUP  BY ISNULL(ou.City, N'(Khác)'),
                      d.InventoryCD,
                      YEAR(d.VisitDate),
                      MONTH(d.VisitDate)
            ORDER  BY ProvinceCode, d.InventoryCD, Yr, Mo
            """;

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText    = sql;
        cmd.CommandTimeout = 60;
        cmd.Parameters.AddWithValue("@FromDate",    fromDate);
        cmd.Parameters.AddWithValue("@ToDate",      periodEnd);
        cmd.Parameters.AddWithValue("@TopProducts", topProducts);

        var result = new List<ProvinceSalesHistory>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            result.Add(new ProvinceSalesHistory(
                ProvinceCode : r["ProvinceCode"]?.ToString() ?? "(Khác)",
                InventoryCD  : r["InventoryCD"].ToString()!,
                InventoryName: r["InventoryName"]?.ToString() ?? r["InventoryCD"].ToString()!,
                Year         : Convert.ToInt32(r["Yr"]),
                Month        : Convert.ToInt32(r["Mo"]),
                TotalQty     : Convert.ToInt64(r["TotalQty"]),
                TotalAmount  : Convert.ToDecimal(r["TotalAmount"]),
                OrderCount   : Convert.ToInt32(r["OrderCount"])));

        return result;
    }

    // ── Map filter / compare ─────────────────────────────────────

    public async Task<IReadOnlyList<MapGroupItem>> GetMapGroupsAsync(DateTime date, CancellationToken ct = default)
    {
        // Lấy danh sách Distributor có SM hoạt động trong ngày từ SalesmanVisit
        const string sql = """
            SELECT 'distributor' AS GroupType, '' AS dummy FROM (SELECT 1 x) t WHERE 1=0
            """;

        // Group SM theo Distributor từ SalesmanVisit
        const string fallbackSql = """
            SELECT 'distributor'                        AS GroupType,
                   CAST(s.DistributorID AS nvarchar)    AS GroupId,
                   ISNULL(d.DistributorName,'Unknown')  AS GroupName,
                   COUNT(DISTINCT sv.SalesmanID)        AS SmCount
            FROM SalesmanVisit sv
            JOIN Salesman s ON s.SalesmanID = sv.SalesmanID
            LEFT JOIN Distributor d ON d.DistributorID = s.DistributorID
            WHERE CONVERT(date, sv.VisitDate) = @Date
              AND s.DistributorID IS NOT NULL
            GROUP BY s.DistributorID, d.DistributorName
            ORDER BY SmCount DESC
            """;

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.Parameters.AddWithValue("@Date", date.Date);
        cmd.CommandTimeout = 30;

        var result = new List<MapGroupItem>();
        try
        {
            cmd.CommandText = fallbackSql;
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                result.Add(new MapGroupItem(
                    GroupType : r["GroupType"].ToString()!,
                    GroupId   : r["GroupId"].ToString()!,
                    GroupName : r["GroupName"].ToString()!,
                    SmCount   : Convert.ToInt32(r["SmCount"])));
        }
        catch { /* ignore — groups are optional */ }
        return result;
    }

    public async Task<SmLocationResult> GetSalesmanLocationsWithTreeAsync(DateTime date, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandType    = CommandType.StoredProcedure;
        cmd.CommandText    = "pp_GetSalemanLastLocation";
        cmd.CommandTimeout = 60;
        cmd.Parameters.AddWithValue("@Username",      CurrentUser);
        cmd.Parameters.AddWithValue("@SalesupID",     "");
        cmd.Parameters.AddWithValue("@DistributorID",  0);
        cmd.Parameters.AddWithValue("@SalesmanID",    "");
        cmd.Parameters.AddWithValue("@Date",          date.Date);
        cmd.Parameters.AddWithValue("@Time",           0);

        var locations = new List<SalesmanLocation>();
        var smGroups  = new Dictionary<string, SmGroupKeys>(StringComparer.OrdinalIgnoreCase);

        // Tree dedup structures
        var lvl0  = new Dictionary<string, (string Name, HashSet<string> Sms)>();
        var lvl1  = new Dictionary<string, (string Name, string P0, HashSet<string> Sms)>();
        var lvl2  = new Dictionary<string, (string Name, string P1, HashSet<string> Sms)>();
        var ss    = new Dictionary<string, (string Name, string P2, HashSet<string> Sms)>();
        var dist  = new Dictionary<string, (string Name, string Pss, HashSet<string> Sms)>();
        var route = new Dictionary<string, (string Name, string Pdist, HashSet<string> Sms)>();

        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var smId   = r["SalesmanID"]?.ToString() ?? "";
            var lat    = r["Latitude"]   is DBNull ? 0d : Convert.ToDouble(r["Latitude"]);
            var lng    = r["Longtitude"] is DBNull ? 0d : Convert.ToDouble(r["Longtitude"]);
            var t0     = r["STLvl0CD"]?.ToString()   ?? "";
            var t1     = r["STLvl1CD"]?.ToString()   ?? "";
            var t2     = r["STLvl2CD"]?.ToString()   ?? "";
            var ssId   = r["SaleSupID"]?.ToString()  ?? "";
            var ssName = r["SaleSupName"]?.ToString() ?? ssId;
            var dId    = r["DistributorID"] is DBNull ? "" : r["DistributorID"].ToString()!;
            var dName  = r["DistributorName"]?.ToString() ?? dId;
            var rId    = r["RouteCD"]?.ToString()    ?? "";
            var rName  = r["RouteName"]?.ToString()  ?? rId;
            var timeObj= r["LastSyncTime"];

            if (lat != 0 || lng != 0)
            {
                if (lat >= -90 && lat <= 90 && lng >= -180 && lng <= 180)
                    locations.Add(new SalesmanLocation(
                        UserName  : smId,
                        Checktime : timeObj is DBNull ? date : Convert.ToDateTime(timeObj),
                        Lattitude : lat,
                        Longtitude: lng));
            }

            if (!string.IsNullOrEmpty(smId))
                smGroups[smId] = new SmGroupKeys(t0, t1, t2, ssId, dId, rId);

            if (!string.IsNullOrEmpty(t0)) { if (!lvl0.ContainsKey(t0)) lvl0[t0]=(t0,[]); lvl0[t0].Sms.Add(smId); }
            if (!string.IsNullOrEmpty(t1)) { if (!lvl1.ContainsKey(t1)) lvl1[t1]=(t1,t0,[]); lvl1[t1].Sms.Add(smId); }
            if (!string.IsNullOrEmpty(t2)) { if (!lvl2.ContainsKey(t2)) lvl2[t2]=(t2,t1,[]); lvl2[t2].Sms.Add(smId); }
            if (!string.IsNullOrEmpty(ssId))  { if (!ss.ContainsKey(ssId))   ss[ssId]  =(ssName,t2,[]);  ss[ssId].Sms.Add(smId); }
            if (!string.IsNullOrEmpty(dId))   { if (!dist.ContainsKey(dId))  dist[dId] =(dName,ssId,[]); dist[dId].Sms.Add(smId); }
            if (!string.IsNullOrEmpty(rId))   { if (!route.ContainsKey(rId)) route[rId]=(rName,dId,[]);  route[rId].Sms.Add(smId); }
        }

        var nodes = new List<SmTreeNode>();
        foreach (var (k,v) in lvl0)  nodes.Add(new SmTreeNode("stlvl0",     k, v.Name, "",     v.Sms.Count));
        foreach (var (k,v) in lvl1)  nodes.Add(new SmTreeNode("stlvl1",     k, v.Name, v.P0,   v.Sms.Count));
        foreach (var (k,v) in lvl2)  nodes.Add(new SmTreeNode("stlvl2",     k, v.Name, v.P1,   v.Sms.Count));
        foreach (var (k,v) in ss)    nodes.Add(new SmTreeNode("ss",         k, v.Name, v.P2,   v.Sms.Count));
        foreach (var (k,v) in dist)  nodes.Add(new SmTreeNode("distributor",k, v.Name, v.Pss,  v.Sms.Count));
        foreach (var (k,v) in route) nodes.Add(new SmTreeNode("route",      k, v.Name, v.Pdist,v.Sms.Count));

        return new SmLocationResult
        {
            Locations = locations,
            Tree      = nodes,
            SmGroups  = smGroups
        };
    }

    public async Task<IReadOnlyList<SmTreeNode>> GetSmTreeAsync(DateTime date, CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandType    = CommandType.StoredProcedure;
        cmd.CommandText    = "pp_GetSalemanLastLocation";
        cmd.CommandTimeout = 60;
        cmd.Parameters.AddWithValue("@Username",     CurrentUser);
        cmd.Parameters.AddWithValue("@SalesupID",    "");
        cmd.Parameters.AddWithValue("@DistributorID", 0);
        cmd.Parameters.AddWithValue("@SalesmanID",   "");
        cmd.Parameters.AddWithValue("@Date",         date.Date);
        cmd.Parameters.AddWithValue("@Time",         0);

        // Dùng Dictionary để deduplicate từng level
        var lvl0  = new Dictionary<string, (string Name, HashSet<string> Sms)>();
        var lvl1  = new Dictionary<string, (string Name, string P0, HashSet<string> Sms)>();
        var lvl2  = new Dictionary<string, (string Name, string P1, HashSet<string> Sms)>();
        var ss    = new Dictionary<string, (string Name, string P2, HashSet<string> Sms)>();
        var dist  = new Dictionary<string, (string Name, string Pss, HashSet<string> Sms)>();
        var route = new Dictionary<string, (string Name, string Pdist, HashSet<string> Sms)>();

        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var smId    = r["SalesmanID"]?.ToString() ?? "";
            var t0      = r["STLvl0CD"]?.ToString()   ?? "";
            var t1      = r["STLvl1CD"]?.ToString()   ?? "";
            var t2      = r["STLvl2CD"]?.ToString()   ?? "";
            var ssId    = r["SaleSupID"]?.ToString()  ?? "";
            var ssName  = r["SaleSupName"]?.ToString()?? ssId;
            var dId     = r["DistributorID"] is DBNull ? "" : r["DistributorID"].ToString()!;
            var dName   = r["DistributorName"]?.ToString() ?? dId;
            var rId     = r["RouteCD"]?.ToString()    ?? "";
            var rName   = r["RouteName"]?.ToString()  ?? rId;

            if (!string.IsNullOrEmpty(t0))
            {
                if (!lvl0.ContainsKey(t0)) lvl0[t0] = (t0, new HashSet<string>());
                lvl0[t0].Sms.Add(smId);
            }
            if (!string.IsNullOrEmpty(t1))
            {
                if (!lvl1.ContainsKey(t1)) lvl1[t1] = (t1, t0, new HashSet<string>());
                lvl1[t1].Sms.Add(smId);
            }
            if (!string.IsNullOrEmpty(t2))
            {
                if (!lvl2.ContainsKey(t2)) lvl2[t2] = (t2, t1, new HashSet<string>());
                lvl2[t2].Sms.Add(smId);
            }
            if (!string.IsNullOrEmpty(ssId))
            {
                if (!ss.ContainsKey(ssId)) ss[ssId] = (ssName, t2, new HashSet<string>());
                ss[ssId].Sms.Add(smId);
            }
            if (!string.IsNullOrEmpty(dId))
            {
                if (!dist.ContainsKey(dId)) dist[dId] = (dName, ssId, new HashSet<string>());
                dist[dId].Sms.Add(smId);
            }
            if (!string.IsNullOrEmpty(rId))
            {
                if (!route.ContainsKey(rId)) route[rId] = (rName, dId, new HashSet<string>());
                route[rId].Sms.Add(smId);
            }
        }

        var nodes = new List<SmTreeNode>();
        foreach (var (k, v) in lvl0)  nodes.Add(new SmTreeNode("stlvl0",     k, v.Name, "",      v.Sms.Count));
        foreach (var (k, v) in lvl1)  nodes.Add(new SmTreeNode("stlvl1",     k, v.Name, v.P0,    v.Sms.Count));
        foreach (var (k, v) in lvl2)  nodes.Add(new SmTreeNode("stlvl2",     k, v.Name, v.P1,    v.Sms.Count));
        foreach (var (k, v) in ss)    nodes.Add(new SmTreeNode("ss",         k, v.Name, v.P2,    v.Sms.Count));
        foreach (var (k, v) in dist)  nodes.Add(new SmTreeNode("distributor",k, v.Name, v.Pss,   v.Sms.Count));
        foreach (var (k, v) in route) nodes.Add(new SmTreeNode("route",      k, v.Name, v.Pdist, v.Sms.Count));
        return nodes;
    }

    public async Task<IReadOnlyList<SalesmanLocation>> GetSalesmanLocationsByGroupAsync(
        DateTime date, string groupType, string groupId, CancellationToken ct = default)
    {
        // Gọi pp_GetSalemanLastLocation với filter đúng level
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandType    = CommandType.StoredProcedure;
        cmd.CommandText    = "pp_GetSalemanLastLocation";
        cmd.CommandTimeout = 60;
        cmd.Parameters.AddWithValue("@Username",     CurrentUser);
        cmd.Parameters.AddWithValue("@SalesupID",    groupType == "ss"          ? groupId : "");
        cmd.Parameters.AddWithValue("@DistributorID",groupType == "distributor" ? (object)int.Parse(groupId) : (object)0);
        cmd.Parameters.AddWithValue("@SalesmanID",   "");
        cmd.Parameters.AddWithValue("@Date",         date.Date);
        cmd.Parameters.AddWithValue("@Time",         0);

        var all = new List<SalesmanLocation>();
        await using var r0 = await cmd.ExecuteReaderAsync(ct);
        while (await r0.ReadAsync(ct))
        {
            var lat = r0["Latitude"]   is DBNull ? 0d : Convert.ToDouble(r0["Latitude"]);
            var lng = r0["Longtitude"] is DBNull ? 0d : Convert.ToDouble(r0["Longtitude"]);
            if (lat == 0 && lng == 0) continue;
            var timeObj = r0["LastSyncTime"];
            all.Add(new SalesmanLocation(
                UserName  : r0["SalesmanID"].ToString()!,
                Checktime : timeObj is DBNull ? date : Convert.ToDateTime(timeObj),
                Lattitude : lat,
                Longtitude: lng));
        }

        return all;

    }

    // Thu thập SalesmanID thuộc node và tất cả con cháu của nó
    private static HashSet<string> CollectSmIds(IReadOnlyList<SmTreeNode> tree, string filterType, string filterId)
    {
        // Build map parentId → children
        var children = tree.GroupBy(n => n.ParentId)
                           .ToDictionary(g => g.Key, g => g.ToList());

        var smIds   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>();
        var queue   = new Queue<(string Type, string Id)>();
        queue.Enqueue((filterType, filterId));

        while (queue.Count > 0)
        {
            var (t, id) = queue.Dequeue();
            if (!visited.Add(id)) continue;

            if (t == "route")
            {
                // Route chứa trực tiếp SM — ta không có mapping route→SM trong tree
                // nên giữ route id để filter ở caller
                smIds.Add("__route__" + id);
                continue;
            }

            // Thêm child nodes vào queue
            if (children.TryGetValue(id, out var kids))
                foreach (var k in kids)
                    queue.Enqueue((k.FilterType, k.Id));
        }
        return smIds;
    }

    public async Task<IReadOnlyList<OutlierAlert>> GetOutlierAlertsAsync(DateTime date, CancellationToken ct = default)
    {
        var result = new List<OutlierAlert>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        // ── 1. pp_ReportSMVisitSummary: visit thiếu, không đơn, đi trễ/về sớm ──
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandType    = CommandType.StoredProcedure;
            cmd.CommandText    = "pp_ReportSMVisitSummary";
            cmd.CommandTimeout = 60;
            cmd.Parameters.AddWithValue("@FromDate",         date.Date);
            cmd.Parameters.AddWithValue("@ToDate",           date.Date);
            cmd.Parameters.AddWithValue("@Level1ID",         DBNull.Value);
            cmd.Parameters.AddWithValue("@Level2ID",         DBNull.Value);
            cmd.Parameters.AddWithValue("@Level3ID",         DBNull.Value);
            cmd.Parameters.AddWithValue("@Level4ID",         DBNull.Value);
            cmd.Parameters.AddWithValue("@Level5ID",         DBNull.Value);
            cmd.Parameters.AddWithValue("@ProvinceCD",       DBNull.Value);
            cmd.Parameters.AddWithValue("@DistributorID",    DBNull.Value);
            cmd.Parameters.AddWithValue("@SaleSupCD",        DBNull.Value);
            cmd.Parameters.AddWithValue("@RouteCD",          DBNull.Value);
            cmd.Parameters.AddWithValue("@SalesmanCD",       DBNull.Value);
            cmd.Parameters.AddWithValue("@UserName",         CurrentUser);
            cmd.Parameters.AddWithValue("@FirstTimeSync",    DBNull.Value);
            cmd.Parameters.AddWithValue("@FirstTimeVisitAM", DBNull.Value);
            cmd.Parameters.AddWithValue("@FirstTimeVisitPM", DBNull.Value);
            cmd.Parameters.AddWithValue("@LastTimeVisit",    DBNull.Value);
            cmd.Parameters.AddWithValue("@OrderDistanceValid",DBNull.Value);
            cmd.Parameters.AddWithValue("@TimeVisit",        DBNull.Value);
            cmd.Parameters.AddWithValue("@STCode",           DBNull.Value);

            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var smId   = r["SalesmanCD"]?.ToString() ?? "";
                var smName = r["SalesmanName"]?.ToString() ?? smId;
                var must   = r["OutletMustVisit"] is DBNull ? 0 : Convert.ToInt32(r["OutletMustVisit"]);
                var visited= r["OutletVisited"]   is DBNull ? 0 : Convert.ToInt32(r["OutletVisited"]);
                var orders = r["OrderCount"]      is DBNull ? 0 : Convert.ToInt32(r["OrderCount"]);
                var startAM= r["FirstStartTimeAM"] is DBNull ? (DateTime?)null : Convert.ToDateTime(r["FirstStartTimeAM"]);
                var endTime= r["LastEndTime"]      is DBNull ? (DateTime?)null : Convert.ToDateTime(r["LastEndTime"]);

                // Lấy vị trí từ _locations (dùng lat/lng mặc định 0 nếu không có)
                double lat = 0, lng = 0;

                // Không visit đủ outlet (< 80% target)
                if (must > 0 && visited < must * 0.8)
                    result.Add(new OutlierAlert(smId,
                        "under_visit",
                        $"Viếng thăm thiếu: {visited}/{must} outlets ({(int)(visited*100.0/must)}%)",
                        lat, lng, date));

                // Ghé thăm nhưng 0 đơn hàng
                if (visited > 0 && orders == 0)
                    result.Add(new OutlierAlert(smId,
                        "no_order",
                        $"Ghé {visited} outlets nhưng không có đơn hàng",
                        lat, lng, date));

                // Đi trễ (bắt đầu sau 8h30)
                if (startAM.HasValue && startAM.Value.Hour > 8 && startAM.Value.Minute > 30)
                    result.Add(new OutlierAlert(smId,
                        "late_start",
                        $"Bắt đầu trễ: {startAM:HH:mm}",
                        lat, lng, startAM.Value));

                // Về sớm (kết thúc trước 16h)
                if (endTime.HasValue && endTime.Value.Hour < 16 && visited > 0)
                    result.Add(new OutlierAlert(smId,
                        "early_end",
                        $"Kết thúc sớm: {endTime:HH:mm}",
                        lat, lng, endTime.Value));
            }
        }
        catch { /* ignore */ }

        // ── 2. OrderHeader: ở quá lâu tại 1 outlet (>60 phút) ──
        try
        {
            const string longStaySql = """
                SELECT h.SalesmanID,
                       ISNULL(o.OutletName, h.OutletID) AS OutletName,
                       h.StartTime, h.EndTime,
                       DATEDIFF(minute, h.StartTime, h.EndTime) AS StayMin,
                       TRY_CAST(h.Latitude   AS float) AS Lat,
                       TRY_CAST(h.Longtitude AS float) AS Lng
                FROM   OrderHeader h
                LEFT JOIN Outlets o ON o.OutletID = h.OutletID
                WHERE  CONVERT(date, h.VisitDate) = @Date
                  AND  h.StartTime IS NOT NULL AND h.EndTime IS NOT NULL
                  AND  DATEDIFF(minute, h.StartTime, h.EndTime) > 60
                ORDER BY StayMin DESC
                """;

            await using var cmd2 = conn.CreateCommand();
            cmd2.CommandText    = longStaySql;
            cmd2.CommandTimeout = 30;
            cmd2.Parameters.AddWithValue("@Date", date.Date);

            await using var r2 = await cmd2.ExecuteReaderAsync(ct);
            while (await r2.ReadAsync(ct))
            {
                var smId     = r2["SalesmanID"].ToString()!;
                var outlet   = r2["OutletName"].ToString()!;
                var stayMin  = Convert.ToInt32(r2["StayMin"]);
                var lat      = r2["Lat"] is DBNull ? 0d : Convert.ToDouble(r2["Lat"]);
                var lng      = r2["Lng"] is DBNull ? 0d : Convert.ToDouble(r2["Lng"]);
                var startTime= r2["StartTime"] is DBNull ? date : Convert.ToDateTime(r2["StartTime"]);

                result.Add(new OutlierAlert(smId,
                    "long_stay",
                    $"Ở lâu tại {outlet}: {stayMin} phút",
                    lat, lng, startTime));
            }
        }
        catch { /* ignore */ }

        return result;
    }

    // ── Territory Performance Map ─────────────────────────────────

    public async Task<IReadOnlyList<TerritoryPolygonPoint>> GetTerritoryPolygonsAsync(CancellationToken ct = default)
    {
        var result = new List<TerritoryPolygonPoint>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Territory, RenderOrder, Lat, Lng FROM TerritoryPolygon ORDER BY Territory, RenderOrder";
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            result.Add(new TerritoryPolygonPoint(
                Territory   : r["Territory"].ToString()!,
                RenderOrder : Convert.ToInt32(r["RenderOrder"]),
                Lat         : Convert.ToDouble(r["Lat"]),
                Lng         : Convert.ToDouble(r["Lng"])));
        return result;
    }

    public async Task<IReadOnlyList<TerritoryOutlet>> GetOutletsAsync(int maxRows = 5000, CancellationToken ct = default)
    {
        var result = new List<TerritoryOutlet>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT TOP {maxRows}
                OutletName,
                ISNULL(Address,'') AS Address,
                ISNULL(Route,'')   AS Route,
                TRY_CAST(Latitude    AS float) AS Lat,
                TRY_CAST(Longtitude  AS float) AS Lng
            FROM Outlets
            WHERE TRY_CAST(Latitude   AS float) IS NOT NULL
              AND TRY_CAST(Longtitude  AS float) IS NOT NULL
              AND TRY_CAST(Latitude   AS float) <> 0
              AND TRY_CAST(Longtitude  AS float) <> 0
            """;
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            double lat = r["Lat"]  is DBNull ? 0 : Convert.ToDouble(r["Lat"]);
            double lng = r["Lng"]  is DBNull ? 0 : Convert.ToDouble(r["Lng"]);
            if (lat == 0 && lng == 0) continue;
            result.Add(new TerritoryOutlet(
                OutletName : r["OutletName"].ToString()!,
                Address    : r["Address"].ToString()!,
                Route      : r["Route"].ToString()!,
                Lat        : lat,
                Lng        : lng));
        }
        return result;
    }

    public async Task<IReadOnlyList<TerritoryKpi>> GetTerritoryKpiAsync(DateTime fromDate, CancellationToken ct = default)
    {
        var result = new List<TerritoryKpi>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandText = "pp_ReportSalesAssessment";
        cmd.Parameters.AddWithValue("@FromDate",     fromDate.Date);
        cmd.Parameters.AddWithValue("@RegionID",     DBNull.Value);
        cmd.Parameters.AddWithValue("@AreaID",       DBNull.Value);
        cmd.Parameters.AddWithValue("@ProvinceID",   DBNull.Value);
        cmd.Parameters.AddWithValue("@DistributorID",DBNull.Value);
        cmd.Parameters.AddWithValue("@SaleSupID",    DBNull.Value);
        cmd.Parameters.AddWithValue("@RouteID",      DBNull.Value);
        cmd.Parameters.AddWithValue("@SalesmanID",   DBNull.Value);
        cmd.Parameters.AddWithValue("@UserName",     CurrentUser);
        cmd.CommandTimeout = 120;

        // Aggregate by DistributorCode (proxy for territory grouping)
        var agg = new Dictionary<string, (string RegionName, string AreaName, string DistName,
                                          decimal Revenue, int Total, int Visited)>();

        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            string key  = r["DistributorCode"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(key)) continue;

            decimal amt  = r["MTDTotalAmount"] is DBNull ? 0m : Convert.ToDecimal(r["MTDTotalAmount"]);
            int    must  = r["MTDOutletMustVisit"] is DBNull ? 0 : Convert.ToInt32(r["MTDOutletMustVisit"]);
            int    visit = r["MTDOutletVisited"]   is DBNull ? 0 : Convert.ToInt32(r["MTDOutletVisited"]);

            if (agg.TryGetValue(key, out var cur))
                agg[key] = (cur.RegionName, cur.AreaName, cur.DistName,
                            cur.Revenue + amt,
                            cur.Total   + must,
                            cur.Visited + visit);
            else
                agg[key] = (
                    r["RegionName"]?.ToString()     ?? "",
                    r["AreaName"]?.ToString()       ?? "",
                    r["DistributorName"]?.ToString() ?? key,
                    amt, must, visit);
        }

        foreach (var (code, v) in agg)
        {
            decimal coverage = v.Total > 0 ? Math.Round((decimal)v.Visited / v.Total * 100, 1) : 0;
            result.Add(new TerritoryKpi(
                TerritoryCode : code,
                TerritoryName : v.DistName,
                RegionName    : v.RegionName,
                AreaName      : v.AreaName,
                Revenue       : v.Revenue,
                TotalOutlet   : v.Total,
                VisitedOutlet : v.Visited,
                CoverageRate  : coverage));
        }

        return result;
    }

    private static (DateTime From, DateTime To) BuildDateRange(int year, int? quarter, int? month)
    {
        if (month.HasValue)
        {
            var from = new DateTime(year, month.Value, 1);
            return (from, from.AddMonths(1));
        }
        if (quarter.HasValue)
        {
            int startMonth = (quarter.Value - 1) * 3 + 1;
            var from = new DateTime(year, startMonth, 1);
            return (from, from.AddMonths(3));
        }
        // Cả năm
        return (new DateTime(year, 1, 1), new DateTime(year + 1, 1, 1));
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}

// ─── Comparer for in-memory sort ────────────────────────────────
file sealed class NullSafeComparer : IComparer<object?>
{
    public static readonly NullSafeComparer Instance = new();
    public int Compare(object? x, object? y)
    {
        if (x is null && y is null) return 0;
        if (x is null) return -1;
        if (y is null) return 1;
        if (x is IComparable cx) return cx.CompareTo(y);
        return string.Compare(x.ToString(), y.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
