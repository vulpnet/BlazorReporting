using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

namespace BlazorReporting.Services;

public sealed class MCPReportRow
{
    public string RegionID       { get; set; } = "";
    public string RegionName     { get; set; } = "";
    public string AreaID         { get; set; } = "";
    public string AreName        { get; set; } = "";
    public int    DistributorID  { get; set; }
    public string DistributorCode{ get; set; } = "";
    public string DistributorName{ get; set; } = "";
    public string RouteCD        { get; set; } = "";
    public string RouteName      { get; set; } = "";
    public string SaleSupCode    { get; set; } = "";
    public string SaleSupName    { get; set; } = "";
    public string SalesmanCode   { get; set; } = "";
    public string SalesmanName   { get; set; } = "";
    public int    CusomerInRoute { get; set; }
    public int    Covered        { get; set; }
}

public sealed class OutletMCPRow
{
    public string RouteCD           { get; set; } = "";
    public string CustomerID        { get; set; } = "";
    public string CustomerLocationID{ get; set; } = "";
    public int    DistributorID     { get; set; }
    public int    Monday            { get; set; }
    public int    Tuesday           { get; set; }
    public int    Wednesday         { get; set; }
    public int    Thursday          { get; set; }
    public int    Friday            { get; set; }
    public int    Saturday          { get; set; }
    public int    Sunday            { get; set; }
    public int    Frequency         { get; set; }
    public string Status            { get; set; } = "";
    public DateTime? LastVisitDate  { get; set; }
}

public sealed class OutletRow
{
    public string OutletID     { get; set; } = "";
    public string OutletName   { get; set; } = "";
    public string Region       { get; set; } = "";
    public string Route        { get; set; } = "";
    public string Status       { get; set; } = "";
    public string Address      { get; set; } = "";
    public string City         { get; set; } = "";
    public decimal? Longtitude { get; set; }
    public decimal? Latitude   { get; set; }
    public int    DistributorID{ get; set; }
}

public sealed class MCPService
{
    private readonly string _cs;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan _ttl = TimeSpan.FromMinutes(10);

    public MCPService(IConfiguration cfg, IMemoryCache cache)
    {
        _cs    = cfg.GetConnectionString("DefaultConnection")!;
        _cache = cache;
    }

    private SqlConnection Conn() => new(_cs);

    // ── Report Customer MCP ──────────────────────────────────────────

    public async Task<List<MCPReportRow>> GetReportMCPAsync(
        string stLvl0 = "", string stLvl1 = "", string stLvl2 = "",
        string stLvl3 = "", string stLvl4 = "", int distributorId = 0, string stCode = "")
    {
        await using var conn = Conn();
        var rows = await conn.QueryAsync<MCPReportRow>(
            "EXEC pp_ReportCustomerMCP @STLvl0CD, @STLvl1CD, @STLvl2CD, @STLvl3CD, @STLvl4CD, @DistributorID, @STCode",
            new { STLvl0CD=stLvl0, STLvl1CD=stLvl1, STLvl2CD=stLvl2, STLvl3CD=stLvl3,
                  STLvl4CD=stLvl4, DistributorID=distributorId, STCode=stCode },
            commandTimeout: 60);
        return rows.ToList();
    }

    // ── Outlets in Route (MCP) ───────────────────────────────────────

    public async Task<List<OutletMCPRow>> GetOutletMCPAsync(string routeCD, string listOutlet = "", int typeAll = 1)
    {
        await using var conn = Conn();
        var rows = await conn.QueryAsync<OutletMCPRow>(
            "EXEC pp_GetOutletMCPBy @RouteCD, @ListOutlet, @TypeAll",
            new { RouteCD=routeCD, ListOutlet=listOutlet, TypeAll=typeAll },
            commandTimeout: 30);
        return rows.ToList();
    }

    // ── Outlets search ───────────────────────────────────────────────

    public async Task<List<OutletRow>> SearchOutletsAsync(string keyword, int limit = 50)
    {
        await using var conn = Conn();
        var rows = await conn.QueryAsync<OutletRow>(
            """
            SELECT TOP (@Limit) OutletID, OutletName, Region, Route, [Status], Address, City, Longtitude, Latitude, DistributorID
            FROM Outlets WITH(NOLOCK)
            WHERE (@Keyword='' OR OutletName LIKE '%'+@Keyword+'%' OR OutletID LIKE '%'+@Keyword+'%')
            ORDER BY OutletName
            """,
            new { Keyword=keyword, Limit=limit });
        return rows.ToList();
    }

    // ── Distributors ─────────────────────────────────────────────────

    public async Task<List<DistributorRow>> GetDistributorsAsync()
    {
        const string key = "mcp:distributors";
        if (_cache.TryGetValue(key, out List<DistributorRow>? cached) && cached is not null)
            return cached;
        await using var conn = Conn();
        var rows = await conn.QueryAsync<DistributorRow>(
            "SELECT DistributorID, DistributorCode, DistributorName FROM Distributor ORDER BY DistributorName");
        var list = rows.ToList();
        _cache.Set(key, list, _ttl);
        return list;
    }

    // ── MCP Detail (DMSMCPDetail) ────────────────────────────────────

    public async Task<List<OutletMCPRow>> GetMCPDetailByRouteAsync(string routeCD)
    {
        await using var conn = Conn();
        var rows = await conn.QueryAsync<OutletMCPRow>(
            """
            SELECT RouteCD, CustomerID, CustomerLocationID, DistributorID,
                   Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday,
                   Frequency, [Status], LastVisitDate
            FROM DMSMCPDetail WITH(NOLOCK)
            WHERE RouteCD = @RouteCD
            ORDER BY CustomerID
            """,
            new { RouteCD = routeCD });
        return rows.ToList();
    }
}
