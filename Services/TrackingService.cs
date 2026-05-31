using Dapper;
using Microsoft.Data.SqlClient;

namespace BlazorReporting.Services;

public sealed class SMLocationRow
{
    public string SalesmanID     { get; set; } = "";
    public string SalesmanName   { get; set; } = "";
    public string Phone          { get; set; } = "";
    public string DistributorCode{ get; set; } = "";
    public string DistributorName{ get; set; } = "";
    public string RouteCD        { get; set; } = "";
    public string RouteName      { get; set; } = "";
    public string SaleSupID      { get; set; } = "";
    public string SaleSupName    { get; set; } = "";
    public string SFLvl0Name     { get; set; } = "";
    public string SFLvl1Name     { get; set; } = "";
    // GPS vị trí hiện tại
    public decimal? Latitude     { get; set; }
    public decimal? Longtitude   { get; set; }
    // GPS lần sync đầu
    public DateTime? FirstSyncTime    { get; set; }
    public decimal? FirstLatitudeSync { get; set; }
    public decimal? FirstLongtitudeSync { get; set; }
    // GPS lần sync cuối
    public DateTime? LastSyncTime     { get; set; }
    public decimal? LastLatitudeSync  { get; set; }
    public decimal? LastLongtitudeSync{ get; set; }
    public decimal  TotalDistance     { get; set; }
    public string   TimeVisit         { get; set; } = "";
    public string   TimeMove          { get; set; } = "";
    public int      IsValid           { get; set; }
    // Outlet đang ở
    public string   OutletID          { get; set; } = "";
    public string   OutletName        { get; set; } = "";
    public decimal  Distance          { get; set; }
    public int      VisitTime         { get; set; }
}

public sealed class TrackingService
{
    private readonly string _cs;

    public TrackingService(IConfiguration cfg)
        => _cs = cfg.GetConnectionString("DefaultConnection")!;

    private SqlConnection Conn() => new(_cs);

    // ── SM Last Location ─────────────────────────────────────────────
    // Bám sát DMS2.0: TrackingController.MovementMonitoring → pp_GetSalemanLastLocation

    public async Task<List<SMLocationRow>> GetSMLastLocationAsync(
        string username, DateTime date,
        string salesupId = "", int distributorId = 0, string salesmanId = "")
    {
        await using var conn = Conn();
        var rows = await conn.QueryAsync<SMLocationRow>(
            "EXEC pp_GetSalemanLastLocation @Username, @SalesupID, @DistributorID, @SalesmanID, @Date",
            new
            {
                Username      = username,
                SalesupID     = salesupId,
                DistributorID = distributorId,
                SalesmanID    = salesmanId,
                Date          = date.Date
            },
            commandTimeout: 30);
        return rows.ToList();
    }

    // ── Visit Tracking (lộ trình theo giờ) ──────────────────────────

    public async Task<List<SMLocationRow>> GetVisitTrackingAsync(
        string routeCD, int distributorId, DateTime date, string username)
    {
        await using var conn = Conn();
        var rows = await conn.QueryAsync<SMLocationRow>(
            "EXEC pp_GetVisitSMTracking @RouteCD, @DistributorID, @VisitDate, @UserName",
            new { RouteCD=routeCD, DistributorID=distributorId, VisitDate=date.Date, UserName=username },
            commandTimeout: 30);
        return rows.ToList();
    }
}
