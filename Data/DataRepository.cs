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
        string colField,
        IReadOnlyList<PivotValueDef> valueDefs,
        CancellationToken ct = default)
    {
        var src  = $"[{Esc(config.Source)}]";
        var rCol = $"[{Esc(rowField)}]";
        var cCol = $"[{Esc(colField)}]";

        // Build aggregate columns — alias = vd.Label so PivotService can locate by name
        var aggCols = valueDefs.Select(vd =>
            $"{ToSqlAgg(vd.Aggregation)}([{Esc(vd.Field)}]) AS [{Esc(vd.Label)}]");

        var (whereClause, filterParms) = BuildWhereClause(config.Filters);

        var sql = $"""
            SELECT {rCol}, {cCol}, {string.Join(", ", aggCols)}
            FROM   {src}
            {whereClause}
            GROUP  BY {rCol}, {cCol}
            ORDER  BY {rCol}, {cCol}
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
