using System.Data;
using Microsoft.Data.SqlClient;
using BlazorReporting.Data.Models;
using BlazorReporting.Models;

namespace BlazorReporting.Data;

public sealed class DataRepository : IDataRepository
{
    private readonly string _cs;
    private readonly ILogger<DataRepository> _log;
    private readonly IConfiguration _cfg;

    public DataRepository(IConfiguration cfg, ILogger<DataRepository> log)
    {
        _cs  = cfg.GetConnectionString("DefaultConnection")
               ?? throw new InvalidOperationException("DefaultConnection not configured.");
        _cfg = cfg;
        _log = log;
    }

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
        const string sql = """
            SELECT UserName, Checktime, CONVERT(decimal(18,6) , Lattitude ) Lattitude, CONVERT(decimal(18,6) , Longtitude ) Longtitude
            FROM DMSAimSalesmanLocation
            WHERE UserName     = @UserName
              AND CONVERT(date, Checktime) = @Date
              AND CONVERT(decimal(18,6) , Lattitude )  <> 0
            AND CONVERT(decimal(18,6) , Lattitude ) <> 0
            ORDER BY Checktime ASC
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
    //   Vị trí cuối cùng trong ngày của từng salesman
    // ══════════════════════════════════════════════════════════════
    public async Task<IReadOnlyList<SalesmanLocation>> GetSalesmanLocationsAsync(
        DateTime date, CancellationToken ct = default)
    {
        const string sql = """
            SELECT UserName, Checktime, CONVERT(decimal(18,6) , Lattitude ) Lattitude, CONVERT(decimal(18,6) , Longtitude ) Longtitude
            FROM (
                SELECT *,
                    ROW_NUMBER() OVER (PARTITION BY UserName ORDER BY Checktime DESC) AS rn
                FROM DMSAimSalesmanLocation
                WHERE CONVERT(date, Checktime) = @Date
            ) t
            WHERE rn = 1
            ORDER BY UserName
            """;

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText    = sql;
        cmd.CommandTimeout = 60;
        cmd.Parameters.AddWithValue("@Date", date.Date);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<SalesmanLocation>();

        while (await reader.ReadAsync(ct))
        {
            var lat = Convert.ToDouble(reader["Lattitude"]);
            var lng = Convert.ToDouble(reader["Longtitude"]);

            // Bỏ qua toạ độ không hợp lệ
            if (lat is 0 && lng is 0) continue;
            if (lat < -90 || lat > 90 || lng < -180 || lng > 180) continue;

            results.Add(new SalesmanLocation(
                UserName  : reader["UserName"].ToString()!,
                Checktime : Convert.ToDateTime(reader["Checktime"]),
                Lattitude : lat,
                Longtitude: lng
            ));
        }

        return results;
    }

    // ══════════════════════════════════════════════════════════════
    // GetSalesmanRouteWithSalesAsync
    //   GPS route + kết hợp đơn hàng KH (match tọa độ ≤ 200m)
    // ══════════════════════════════════════════════════════════════
    public async Task<IReadOnlyList<SalesmanRoutePoint>> GetSalesmanRouteWithSalesAsync(
        string userName, DateTime date, CancellationToken ct = default)
    {
        const string gpsSql = """
            SELECT CONVERT(decimal(18,6), Lattitude)  AS Lat,
                   CONVERT(decimal(18,6), Longtitude) AS Lng,
                   Checktime
            FROM   DMSAimSalesmanLocation
            WHERE  UserName = @User
              AND  CONVERT(date, Checktime) = @Date
              AND  CONVERT(decimal(18,6), Lattitude)  <> 0
              AND  CONVERT(decimal(18,6), Longtitude) <> 0
            ORDER BY Checktime ASC
            """;

        const string orderSql = """
            SELECT h.OrderDate,
                   h.CustomerCD,
                   c.LocationName,
                   h.RouteCode,
                   h.OrderAmount,
                   CONVERT(decimal(18,6), c.Latitude)  AS Lat,
                   CONVERT(decimal(18,6), c.Longitude) AS Lng
            FROM   DMSAimOrderHeader  h
            INNER JOIN DMSAimCustomer c
                   ON  h.UserName   = c.UserName
                   AND h.CustomerCD = c.CustomerCD
            WHERE  h.UserName = @User
              AND  CONVERT(date, h.OrderDate) = @Date
              AND  c.Latitude  <> 0
              AND  c.Longitude <> 0
            ORDER BY h.OrderDate ASC
            """;

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        // Fetch GPS points
        var gpsPoints = new List<(double Lat, double Lng, DateTime Time)>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = gpsSql;
            cmd.CommandTimeout = 30;
            cmd.Parameters.AddWithValue("@User", userName);
            cmd.Parameters.AddWithValue("@Date", date.Date);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                gpsPoints.Add((Convert.ToDouble(r["Lat"]),
                               Convert.ToDouble(r["Lng"]),
                               Convert.ToDateTime(r["Checktime"])));
        }

        // Fetch customer orders
        var orders = new List<(double Lat, double Lng, CustomerVisit Visit)>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = orderSql;
            cmd.CommandTimeout = 30;
            cmd.Parameters.AddWithValue("@User", userName);
            cmd.Parameters.AddWithValue("@Date", date.Date);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var oLat = Convert.ToDouble(r["Lat"]);
                var oLng = Convert.ToDouble(r["Lng"]);
                if (oLat == 0 && oLng == 0) continue;

                var visit = new CustomerVisit(
                    CustomerCD  : r["CustomerCD"].ToString()!,
                    LocationName: r["LocationName"]?.ToString() ?? "",
                    RouteCode   : r["RouteCode"]?.ToString()    ?? "",
                    OrderAmount : Convert.ToDecimal(r["OrderAmount"]),
                    OrderDate   : Convert.ToDateTime(r["OrderDate"]));

                orders.Add((oLat, oLng, visit));
            }
        }

        // Kết hợp: mỗi GPS point tìm order gần nhất ≤ 200m
        const double ThresholdKm = 0.2;

        var result = gpsPoints.Select(gps =>
        {
            CustomerVisit? best = null;
            double bestDist = double.MaxValue;

            foreach (var (oLat, oLng, visit) in orders)
            {
                var dist = HaversineKm(gps.Lat, gps.Lng, oLat, oLng);
                if (dist <= ThresholdKm && dist < bestDist)
                {
                    bestDist = dist;
                    best     = visit;
                }
            }

            return new SalesmanRoutePoint(userName, gps.Time, gps.Lat, gps.Lng, best);
        }).ToList();

        return result;
    }

    // ══════════════════════════════════════════════════════════════
    // GetDailySalesBySmAsync — tổng doanh số theo SM trong ngày
    // ══════════════════════════════════════════════════════════════
    public async Task<IReadOnlyDictionary<string, decimal>> GetDailySalesBySmAsync(
        DateTime date, CancellationToken ct = default)
    {
        const string sql = """
            SELECT UserName, SUM(OrderAmount) AS TotalAmount
            FROM   DMSAimOrderHeader
            WHERE  CONVERT(date, OrderDate) = @Date
            GROUP  BY UserName
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
        // groupBy: "route" | "sm" | "channel" | "province"
        var (groupCol, labelAlias) = groupBy switch
        {
            "sm"       => ("h.UserName",                        "Salesman"),
            //"channel"  => ("ISNULL(h.ChannelCode,'(Khác)')",   "Kênh"),
            "province" => ("ISNULL(p.ProvinceCode,'(Khác)')",  "Tỉnh/TP"),
            _          => ("ISNULL(h.RouteCode,'(Khác)')",     "Tuyến")
        };

        var sql = $"""
            SELECT
                {groupCol}                     AS Label,
                SUM(h.OrderAmount)             AS TotalAmount,
                COUNT(*)                       AS OrderCount,
                COUNT(DISTINCT h.UserName)     AS SmCount
            FROM  DMSAimOrderHeader h
            LEFT JOIN DMSAimCustomer c
                   ON h.UserName   = c.UserName
                  AND h.CustomerCD = c.CustomerCD
            LEFT JOIN (
                       select distinct ProvinceID, Descr ProvinceCode from DMSAimProvince
            )  p ON c.ProvinceID = p.ProvinceID
            WHERE CONVERT(date, h.OrderDate) = @Date
            GROUP BY {groupCol}
            ORDER BY TotalAmount DESC
            """;

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText    = sql;
        cmd.CommandTimeout = 30;
        cmd.Parameters.AddWithValue("@Date", date.Date);

        var result = new List<SalesAreaItem>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            result.Add(new SalesAreaItem(
                Label      : r["Label"]?.ToString() ?? "(Khác)",
                TotalAmount: Convert.ToDecimal(r["TotalAmount"]),
                OrderCount : Convert.ToInt32(r["OrderCount"]),
                SmCount    : Convert.ToInt32(r["SmCount"])));

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
            "sm"       => ("h.UserName",                              ""),
            "channel"  => ("ISNULL(h.ChannelCode,'(Khác)')",         ""),
            "province" => ("ISNULL(p.ProvinceCode,'(Khác)')",        ""),
            _          => ("ISNULL(h.RouteCode,'(Khác)')",           "")
        };

        var sql = $"""
            SELECT DISTINCT h.UserName
            FROM  DMSAimOrderHeader h
            LEFT JOIN DMSAimCustomer c
                   ON h.UserName   = c.UserName
                  AND h.CustomerCD = c.CustomerCD
            LEFT JOIN (SELECT DISTINCT ProvinceID, Descr ProvinceCode FROM DMSAimProvince) p
                   ON c.ProvinceID = p.ProvinceID
            WHERE CONVERT(date, h.OrderDate) = @Date
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
        // groupBy = "province" → dùng tên tỉnh (join DMSAimProvince qua ProvinceID)
        //         = "route" | others → dùng AreaLabel sẵn có trong DMSOrder
        bool byProvince = groupBy == "province";

        var (dateFrom, dateTo) = BuildDateRange(year, quarter, month);

        var areaExpr = byProvince
            ? "ISNULL(pv.Descr, N'(Khác)')"
            : "ISNULL(o.AreaLabel, N'(Khác)')";

        var joinProvince = byProvince
            ? "LEFT JOIN (SELECT DISTINCT ProvinceID, Descr FROM DMSAimProvince WITH (NOLOCK)) pv ON pv.ProvinceID = o.ProvinceID"
            : "";

        var sql = $"""
            ;WITH TopProds AS (
                SELECT TOP (@TopN) InventoryCD
                FROM   DMSOrder WITH (NOLOCK)
                WHERE  OrderDate >= @DateFrom AND OrderDate < @DateTo
                GROUP  BY InventoryCD
                ORDER  BY SUM(TotalQty) DESC
            )
            SELECT
                o.InventoryCD,
                MAX(ISNULL(o.InventoryName, o.InventoryCD)) AS InventoryName,
                {areaExpr}                                  AS AreaLabel,
                SUM(o.TotalQty)                             AS TotalQty,
                SUM(o.TotalAmount)                          AS TotalAmount,
                SUM(o.OrderCount)                           AS OrderCount
            FROM   DMSOrder o WITH (NOLOCK)
            INNER  JOIN TopProds tp ON o.InventoryCD = tp.InventoryCD
            {joinProvince}
            WHERE  o.OrderDate >= @DateFrom AND o.OrderDate < @DateTo
            GROUP  BY o.InventoryCD, {areaExpr}
            ORDER  BY TotalQty DESC
            """;

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText    = sql;
        cmd.CommandTimeout = 30;
        cmd.Parameters.AddWithValue("@DateFrom", dateFrom);
        cmd.Parameters.AddWithValue("@DateTo",   dateTo);
        cmd.Parameters.AddWithValue("@TopN",     topN);

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

        var trendFrom = DateTime.Today.AddMonths(-historyMonths);

        var sql = $"""
            SELECT
                d.InventoryCD,
                MAX(ISNULL(inv.InventoryName, d.InventoryCD))         AS InventoryName,
                YEAR(h.OrderDate)                                      AS Yr,
                MONTH(h.OrderDate)                                     AS Mo,
                SUM(d.OrderQty)                                        AS TotalQty,
                SUM(CAST(d.OrderQty AS DECIMAL(18,4)) * d.UnitPrice)  AS TotalAmount
            FROM DMSAimOrderDetail  d WITH (NOLOCK)
            INNER JOIN DMSAimOrderHeader h WITH (NOLOCK)
                   ON  d.UserName       = h.UserName
                   AND d.OrderCode      = h.OrderCode
                   AND d.DistributorCode= h.DistributorCD
            LEFT  JOIN DMSAimInventoryItem inv WITH (NOLOCK)
                   ON  inv.InventoryCD  = d.InventoryCD
            WHERE d.InventoryCD IN ({inClause})
              AND h.OrderDate >= @TrendFrom
            GROUP BY d.InventoryCD, YEAR(h.OrderDate), MONTH(h.OrderDate)
            ORDER BY d.InventoryCD, Yr, Mo
            """;

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText    = sql;
        cmd.CommandTimeout = 30;
        cmd.Parameters.AddWithValue("@TrendFrom", trendFrom.Date);
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
                FROM   DMSOrder WITH (NOLOCK)
                WHERE  OrderDate >= @FromDate
                  AND  OrderDate <  @ToDate
                GROUP  BY InventoryCD
                ORDER  BY SUM(TotalQty) DESC
            )
            SELECT
                ISNULL(o.AreaLabel, N'(Khác)')              AS ProvinceCode,
                o.InventoryCD,
                MAX(ISNULL(o.InventoryName, o.InventoryCD)) AS InventoryName,
                YEAR(o.OrderDate)                           AS Yr,
                MONTH(o.OrderDate)                          AS Mo,
                SUM(o.TotalQty)                             AS TotalQty,
                SUM(o.TotalAmount)                          AS TotalAmount,
                SUM(o.OrderCount)                           AS OrderCount
            FROM   DMSOrder o WITH (NOLOCK)
            INNER  JOIN TopProds tp ON o.InventoryCD = tp.InventoryCD
            WHERE  o.OrderDate >= @FromDate
              AND  o.OrderDate <  @ToDate
            GROUP  BY ISNULL(o.AreaLabel, N'(Khác)'),
                      o.InventoryCD,
                      YEAR(o.OrderDate),
                      MONTH(o.OrderDate)
            ORDER  BY ProvinceCode, o.InventoryCD, Yr, Mo
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
