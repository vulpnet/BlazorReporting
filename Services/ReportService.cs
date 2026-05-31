using Dapper;
using Microsoft.Data.SqlClient;

namespace BlazorReporting.Services;

// ── pp_ReportVisit ───────────────────────────────────────────────────

public sealed class ReportVisitRow
{
    public string STLvl0Name    { get; set; } = "";
    public string STLvl1Name    { get; set; } = "";
    public string STLvl2Name    { get; set; } = "";
    public string DistributorCD { get; set; } = "";
    public string DistributorName{ get; set; } = "";
    public string RouteCD       { get; set; } = "";
    public string RouteName     { get; set; } = "";
    public string SalesSupName  { get; set; } = "";
    public string SalesmanCD    { get; set; } = "";
    public string SalesmanName  { get; set; } = "";
    public DateTime? VisitDate  { get; set; }
    public string OutletID      { get; set; } = "";
    public string OutletName    { get; set; } = "";
    public DateTime? StartTime  { get; set; }
    public DateTime? EndTime    { get; set; }
    public int    TimeVisit     { get; set; }
    public decimal TimeMove     { get; set; }
    public decimal Distance     { get; set; }
    public int    HasOrder      { get; set; }
    public decimal TotalSKU     { get; set; }
    public decimal DropSize     { get; set; }
    public decimal TotalAmount  { get; set; }
    public string strIsMCP      { get; set; } = "";
    public decimal? Latitude    { get; set; }
    public decimal? Longtitude  { get; set; }
}

// ── pp_ReportSMVisitSummary ──────────────────────────────────────────

public sealed class SMVisitSummaryRow
{
    public string STLvl0Name    { get; set; } = "";
    public string STLvl1Name    { get; set; } = "";
    public string DistributorCD { get; set; } = "";
    public string DistributorName{ get; set; } = "";
    public string RouteCD       { get; set; } = "";
    public string RouteName     { get; set; } = "";
    public string SalesSupName  { get; set; } = "";
    public string SalesmanCD    { get; set; } = "";
    public string SalesmanName  { get; set; } = "";
    public DateTime? VisitDate  { get; set; }
    public int    OutletMustVisit { get; set; }
    public int    OutletVisited   { get; set; }
    public int    OrderCount      { get; set; }
    public decimal TotalSKU      { get; set; }
    public decimal TotalAmount   { get; set; }
    public decimal LPPC          { get; set; }
    public decimal SOMCP         { get; set; }
    public decimal VisitMCP      { get; set; }
    public DateTime? FirstSyncTime { get; set; }
    public DateTime? FirstStartTimeAM { get; set; }
    public DateTime? LastEndTime  { get; set; }
    public string strIsMCP       { get; set; } = "";
}

// ── Generic row dùng cho nhiều SP ────────────────────────────────────

public sealed class GenericReportRow
{
    // Common hierarchy
    public string STLvl0Name     { get; set; } = "";
    public string STLvl1Name     { get; set; } = "";
    public string STLvl2Name     { get; set; } = "";
    public string STLvl3Name     { get; set; } = "";
    public string DistributorCD  { get; set; } = "";
    public string DistributorName{ get; set; } = "";
    public string RouteCD        { get; set; } = "";
    public string RouteName      { get; set; } = "";
    public string SalesSupName   { get; set; } = "";
    public string SalesmanCD     { get; set; } = "";
    public string SalesmanName   { get; set; } = "";
    // Date/time
    public DateTime? VisitDate   { get; set; }
    public DateTime? FromDate    { get; set; }
    public DateTime? Date        { get; set; }
    // KPI metrics
    public int    OutletMustVisit { get; set; }
    public int    OutletVisited  { get; set; }
    public int    OrderCount     { get; set; }
    public decimal TotalSKU      { get; set; }
    public decimal TotalAmount   { get; set; }
    public decimal LPPC          { get; set; }
    public decimal SOMCP         { get; set; }
    public decimal VisitMCP      { get; set; }
    public decimal MTDTotalAmount{ get; set; }
    public string strIsMCP       { get; set; } = "";
    public DateTime? FirstSyncTime { get; set; }
    // Extra
    public string OutletID       { get; set; } = "";
    public string OutletName     { get; set; } = "";
    public string Content        { get; set; } = "";
    public string Reason         { get; set; } = "";
    public string UserName       { get; set; } = "";
    public string RegionName     { get; set; } = "";
    public string AreaName       { get; set; } = "";
    public int    IsValid        { get; set; }
    public decimal Latitude      { get; set; }
    public decimal Longitude     { get; set; }
    public int    PC             { get; set; }
    public decimal TotalPC       { get; set; }
    public int    HasOrder       { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime{ get; set; }
    public decimal Distance      { get; set; }
    public string IssueID        { get; set; } = "";
    public string SalesmanCode   { get; set; } = "";
    public int    ImageCount     { get; set; }
    public int    Status         { get; set; }
    public string ProgramName    { get; set; } = "";
    public string EvaluationID   { get; set; } = "";
    public string FullName       { get; set; } = "";
    public string Email          { get; set; } = "";
    public string Phone          { get; set; } = "";
    public string RoleName       { get; set; } = "";
    public int    Click          { get; set; }
    public string Page           { get; set; } = "";
    public string Action         { get; set; } = "";
    public string SM             { get; set; } = "";  // "X" or ""
    public string ASM            { get; set; } = "";
    public string Register       { get; set; } = "";  // "Y"/"N"
    public string IsSync         { get; set; } = "";
    // PC_SM specific
    public int    PCPass         { get; set; }
    public int    PCNotPass      { get; set; }
    public decimal PC_MCP        { get; set; }
    public int    MTDPCPass      { get; set; }
    public int    MTDPCNotPass   { get; set; }
    public decimal MTDPC_MCP     { get; set; }
    public int    MCP            { get; set; }
    // Timekeeping local specific
    public int    ScheduleDateWork { get; set; }
    public int    SynchronizeDate  { get; set; }
    public int    VisitedDate      { get; set; }
    public decimal VisitedDateRate { get; set; }
    // 3G Offline specific
    public int    TimeLimit      { get; set; }
    public int    ActualTime     { get; set; }
    public int    Evaluate       { get; set; }
    // Outlet GPS Invalid specific
    public string LocationCD     { get; set; } = "";
    public string OutletCD       { get; set; } = "";
    public bool   IsActive       { get; set; }
    public string City           { get; set; } = "";
    // Visit Reason specific
    public string VisitNotOrder  { get; set; } = "";
    public decimal VisitNotOrderRate { get; set; }
    // SalesEffective / SummarySales specific
    public decimal TargetRevenue     { get; set; }
    public int    MTDOutletMustVisit { get; set; }
    public int    MTDOutletVisited   { get; set; }
    public int    MTDOrderCount      { get; set; }
}

// ── Service ─────────────────────────────────────────────────────────

public sealed class ReportService
{
    private readonly string _cs;

    public ReportService(IConfiguration cfg)
        => _cs = cfg.GetConnectionString("DefaultConnection")!;

    private SqlConnection Conn() => new(_cs);

    // ── Report Visit (chi tiết từng lần viếng thăm) ──────────────────

    public async Task<List<ReportVisitRow>> GetReportVisitAsync(
        DateTime date, string username,
        string stLvl0 = "", string stLvl1 = "", string stLvl2 = "",
        int distributorId = 0, string saleSupCD = "", string routeCD = "", string stCode = "")
    {
        await using var conn = Conn();
        var rows = await conn.QueryAsync<ReportVisitRow>(
            "EXEC pp_ReportVisit @VisitDate, @STLvl0CD, @STLvl1CD, @STLvl2CD, @STLvl3CD, @STLvl4CD, @DistributorID, @SaleSupCD, @RouteCD, @UserName, @STCode",
            new
            {
                VisitDate     = date.Date,
                STLvl0CD      = stLvl0, STLvl1CD = stLvl1, STLvl2CD = stLvl2,
                STLvl3CD      = "", STLvl4CD = "",
                DistributorID = distributorId, SaleSupCD = saleSupCD,
                RouteCD       = routeCD, UserName = username, STCode = stCode
            },
            commandTimeout: 60);
        return rows.ToList();
    }

    // ── Helper: STLvl params dùng chung ─────────────────────────────

    private static object StParams(DateTime from, DateTime to, string username,
        string st0="", string st1="", string st2="", int distId=0,
        string sup="", string route="", string stCode="") => new
        { FromDate=from, ToDate=to, STLvl0CD=st0, STLvl1CD=st1, STLvl2CD=st2,
          STLvl3CD="", STLvl4CD="", DistributorID=distId, SaleSupCD=sup,
          RouteCD=route, UserName=username, STCode=stCode };

    private static object StParamsDate(DateTime date, string username,
        string st0="", string st1="", int distId=0, string sup="", string route="", string stCode="") => new
        { VisitDate=date, STLvl0CD=st0, STLvl1CD=st1, STLvl2CD="", STLvl3CD="", STLvl4CD="",
          DistributorID=distId, SaleSupCD=sup, RouteCD=route, UserName=username, STCode=stCode };

    private async Task<List<GenericReportRow>> ExecAsync(string sql, object p, int timeout=60)
    {
        // H6-FIX: tra ve empty list thay vi throw khi timeout — tranh Blazor circuit freeze
        try
        {
            await using var conn = Conn();
            return (await conn.QueryAsync<GenericReportRow>(sql, p, commandTimeout: timeout)).ToList();
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == -2 || ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            // Log timeout nhung khong crash UI
            return [];
        }
    }

    // ── 1. Tong hop doanh so — dung SP local (pp_ReportSummarySales qua cham: 61s, 600K rows) ──

    public Task<List<GenericReportRow>> GetSummarySalesAsync(
        DateTime from, DateTime to, string username,
        string st0="", string st1="", int distId=0, string sup="", string route="", string stCode="")
        => ExecAsync("EXEC pp_GetSummarySales_Local @FromDate,@ToDate,@UserName,@STLvl0CD,@STLvl1CD,@DistributorID,@SaleSupCD,@RouteCD,@STCode",
            new{FromDate=from,ToDate=to,UserName=username,STLvl0CD=st0,STLvl1CD=st1,DistributorID=distId,SaleSupCD=sup,RouteCD=route,STCode=stCode});

    public Task<List<GenericReportRow>> GetSummarySalesDetailAsync(
        DateTime from, DateTime to, string username,
        string st0="", string st1="", int distId=0, string sup="", string route="", string stCode="")
        => ExecAsync("EXEC pp_GetSummarySales_Local @FromDate,@ToDate,@UserName,@STLvl0CD,@STLvl1CD,@DistributorID,@SaleSupCD,@RouteCD,@STCode",
            new{FromDate=from,ToDate=to,UserName=username,STLvl0CD=st0,STLvl1CD=st1,DistributorID=distId,SaleSupCD=sup,RouteCD=route,STCode=stCode});

    // ── 2. Bán hàng hiệu quả ─────────────────────────────────────────

    public Task<List<GenericReportRow>> GetSalesEffectiveAsync(
        DateTime date, string username,
        string st0="", string st1="", int distId=0, string sup="", string route="", string stCode="")
        => ExecAsync("EXEC pp_ReportSalesEffective @VisitDate,@STLvl0CD,@STLvl1CD,@STLvl2CD,@STLvl3CD,@STLvl4CD,@DistributorID,@SaleSupCD,@RouteCD,@UserName,@STCode",
            new{VisitDate=date,STLvl0CD=st0,STLvl1CD=st1,STLvl2CD="",STLvl3CD="",STLvl4CD="",DistributorID=distId,SaleSupCD=sup,RouteCD=route,UserName=username,STCode=stCode});

    // ── 3. Doanh số ngày MTD ─────────────────────────────────────────

    public Task<List<GenericReportRow>> GetSalesDailyToMTDAsync(
        DateTime from, DateTime to, string username,
        string regionId="", string areaId="", int distId=0, string sup="", string route="", string salesmanId="", int isMCP=1)
        => ExecAsync("EXEC pp_ReportSalesDailyToMTD @FromDate,@ToDate,@RegionID,@AreaID,@ProvinceID,@DistributorID,@SaleSupID,@RouteID,@SalesmanID,@IsMCP,@UserName",
            new{FromDate=from,ToDate=to,RegionID=regionId,AreaID=areaId,ProvinceID="",DistributorID=distId,SaleSupID=sup,RouteID=route,SalesmanID=salesmanId,IsMCP=isMCP,UserName=username});

    // ── 4. Ke hoach ngay — dung SP local thay pp_ReportDailyPlan (cross-DB) ──

    public Task<List<GenericReportRow>> GetDailyPlanAsync(
        DateTime date, string username,
        string regionId="", string areaId="", string distId="", string supId="", string routeId="")
        => ExecAsync("EXEC pp_GetDailyPlan_Local @VisitDate,@RegionID,@AreaID,@DistributorID,@SaleSupID,@RouteID,@UserName",
            new{VisitDate=date,RegionID=regionId,AreaID=areaId,DistributorID=distId,SaleSupID=supId,RouteID=routeId,UserName=username});

    // ── 5. PC theo TDV ───────────────────────────────────────────────

    public Task<List<GenericReportRow>> GetReportPCSMAsync(
        DateTime date, string username,
        string st0="", string st1="", int distId=0, string sup="", string route="",
        int showAll=1, int percent=0, int isGreater=1, string stCode="")
        => ExecAsync("EXEC pp_ReportPC_SM @FromDate,@STLvl0CD,@STLvl1CD,@STLvl2CD,@STLvl3CD,@STLvl4CD,@DistributorID,@SaleSupCD,@RouteCD,@ShowAll,@Percent,@IsGreater,@UserName,@STCode",
            new{FromDate=date,STLvl0CD=st0,STLvl1CD=st1,STLvl2CD="",STLvl3CD="",STLvl4CD="",DistributorID=distId,SaleSupCD=sup,RouteCD=route,ShowAll=showAll,Percent=percent,IsGreater=isGreater,UserName=username,STCode=stCode});

    // ── 6. 3G Offline ────────────────────────────────────────────────

    public Task<List<GenericReportRow>> GetReport3GOfflineAsync(
        DateTime from, DateTime to, string username,
        string st0="", string st1="", int distId=0, string sup="", string route="", int time=0, string stCode="")
        => ExecAsync("EXEC pp_Report3GOffline @FromDate,@ToDate,@STLvl0CD,@STLvl1CD,@STLvl2CD,@STLvl3CD,@STLvl4CD,@DistributorID,@SalesSupCD,@RouteCD,@UserName,@Time,@STCode",
            new{FromDate=from,ToDate=to,STLvl0CD=st0,STLvl1CD=st1,STLvl2CD="",STLvl3CD="",STLvl4CD="",DistributorID=distId,SalesSupCD=sup,RouteCD=route,UserName=username,Time=time,STCode=stCode});

    // ── 7. Outlet GPS sai ────────────────────────────────────────────

    public Task<List<GenericReportRow>> GetOutletInvalidLocationAsync(
        string username, string st0="", string st1="", int distId=0, string sup="", string stCode="")
        => ExecAsync("EXEC pp_ReportOutletInvalidLocation @STLvl0CD,@STLvl1CD,@STLvl2CD,@STLvl3CD,@STLvl4CD,@DistributorID,@SaleSupCD,@UserName,@STCode",
            new{STLvl0CD=st0,STLvl1CD=st1,STLvl2CD="",STLvl3CD="",STLvl4CD="",DistributorID=distId,SaleSupCD=sup,UserName=username,STCode=stCode});

    // ── 8. Issues Report ─────────────────────────────────────────────

    public Task<List<GenericReportRow>> GetReportIssuesAsync(
        DateTime from, DateTime to, string username,
        string st0="", string st1="", int status=0, string stCode="")
        => ExecAsync("EXEC pp_ReportIssues @FromDate,@ToDate,@STLvl0CD,@STLvl1CD,@STLvl2CD,@STLvl3CD,@STLvl4CD,@UserName,@Status,@STCode",
            new{FromDate=from,ToDate=to,STLvl0CD=st0,STLvl1CD=st1,STLvl2CD="",STLvl3CD="",STLvl4CD="",UserName=username,Status=status,STCode=stCode});

    public Task<List<GenericReportRow>> GetReportIssuesDetailAsync(
        DateTime date, string issueId="", string salesmanCode="", string outletId="")
        => ExecAsync("EXEC pp_ReportIssuesDetail @VisitDate,@IssueID,@SalesmanCode,@OutletID",
            new{VisitDate=date,IssueID=issueId,SalesmanCode=salesmanCode,OutletID=outletId});

    // ── 9. User Log ──────────────────────────────────────────────────

    // Dung SP local thay pp_ReportUserLog (cross-DB NhatNhatDMS)
    public Task<List<GenericReportRow>> GetUserLogAsync(DateTime from, DateTime to, string username, string regionId="")
        => ExecAsync("EXEC pp_GetUserLog_Local @FromDate,@ToDate,@RegionID,@UserName",
            new{FromDate=from,ToDate=to,RegionID=regionId,UserName=username});

    public Task<List<GenericReportRow>> GetUserActionLogTerritoryAsync(DateTime from, DateTime to, int userId=0)
        => ExecAsync("EXEC pp_ReportUserActionLogTerritory @FromDate,@ToDate,@UserID",
            new{FromDate=from,ToDate=to,UserID=userId});

    // ── 10. Cham cong — dung SP local thay pp_ReportTimekeeping (cross-DB) ──

    public Task<List<GenericReportRow>> GetTimekeepingAsync(
        DateTime from, DateTime to, string username,
        string regionId="", string areaId="", int distId=0, string salesmanId="")
        => ExecAsync("EXEC pp_GetTimekeeping_Local @FromDate,@ToDate,@RegionID,@AreaID,@DistributorID,@SalemanID,@UserName",
            new{FromDate=from,ToDate=to,RegionID=regionId,AreaID=areaId,DistributorID=distId,SalemanID=salesmanId,UserName=username});

    // ── 11. Lý do viếng thăm ────────────────────────────────────────

    public Task<List<GenericReportRow>> GetVisitReasonAsync(
        DateTime from, DateTime to, string username,
        string l1="", string l2="", int distId=0, string sup="", string route="", string stCode="")
        => ExecAsync("EXEC pp_ReportVisitReason @FromDate,@ToDate,@Level1ID,@Level2ID,@Level3ID,@Level4ID,@Level5ID,@DistributorID,@SaleSupID,@RouteID,@UserName,@STCode",
            new{FromDate=from,ToDate=to,Level1ID=l1,Level2ID=l2,Level3ID="",Level4ID="",Level5ID="",DistributorID=distId,SaleSupID=sup,RouteID=route,UserName=username,STCode=stCode});

    // ── 12. CH chưa VT trong MCP ─────────────────────────────────────

    public Task<List<GenericReportRow>> GetOutletNotVisitMCPAsync(
        DateTime date, string username,
        string st0="", string st1="", int distId=0, string sup="", string route="", string stCode="")
        => ExecAsync("EXEC BSDH_ReportOutletNotVisitInMCP @FromDate,@STLvl0CD,@STLvl1CD,@STLvl2CD,@STLvl3CD,@STLvl4CD,@DistributorID,@SaleSupCD,@RouteCD,@UserName,@STCode",
            new{FromDate=date,STLvl0CD=st0,STLvl1CD=st1,STLvl2CD="",STLvl3CD="",STLvl4CD="",DistributorID=distId,SaleSupCD=sup,RouteCD=route,UserName=username,STCode=stCode});

    // ── 13. Display Image ────────────────────────────────────────────

    public Task<List<GenericReportRow>> GetDisplayImageByOutletAsync(
        DateTime from, DateTime to, string username,
        int programId=0, string regionId="", string areaId="", int distId=0, string supId="", string routeId="")
        => ExecAsync("EXEC pp_ReportDisplayImageByOutlet @FromDate,@ToDate,@ProgramID,@EvaluationID,@RegionID,@AreaID,@ProvinceID,@DistributorID,@SaleSupID,@RouteID,@OutletID,@SalesmanID,@UserName",
            new{FromDate=from,ToDate=to,ProgramID=programId,EvaluationID="",RegionID=regionId,AreaID=areaId,ProvinceID="",DistributorID=distId,SaleSupID=supId,RouteID=routeId,OutletID="",SalesmanID="",UserName=username});

    // ── 14. View Image Display ───────────────────────────────────────

    public Task<List<GenericReportRow>> GetViewImageDisplayAsync(int programId, string username)
        => ExecAsync("EXEC pp_ReportViewImageDisplayOutlet @ProgramID,@UserName",
            new{ProgramID=programId,UserName=username});

    // ── 15. User Mobility ────────────────────────────────────────────

    public Task<List<GenericReportRow>> GetUserMobilityAsync(DateTime from, DateTime to, string username)
        => ExecAsync("EXEC pp_ReportUserUseMobility @FromDate,@ToDate,@UserName",
            new{FromDate=from,ToDate=to,UserName=username});

    // ── SM Visit Summary (tổng hợp KPI theo TDV) ────────────────────

    public async Task<List<SMVisitSummaryRow>> GetSMVisitSummaryAsync(
        DateTime fromDate, DateTime toDate, string username,
        string level1 = "", string level2 = "", int distributorId = 0,
        string saleSupCD = "", string routeCD = "", string salesmanCD = "", string stCode = "")
    {
        await using var conn = Conn();
        var rows = await conn.QueryAsync<SMVisitSummaryRow>(
            """
            EXEC pp_ReportSMVisitSummary @FromDate, @ToDate,
                @Level1ID, @Level2ID, @Level3ID, @Level4ID, @Level5ID,
                @ProvinceCD, @DistributorID, @SaleSupCD, @RouteCD, @SalesmanCD,
                @UserName,
                @FirstTimeSync, @FirstTimeVisitAM, @FirstTimeVisitPM, @LastTimeVisit,
                @OrderDistanceValid, @TimeVisit, @STCode
            """,
            new
            {
                FromDate = fromDate, ToDate = toDate,
                Level1ID = level1, Level2ID = level2, Level3ID = "", Level4ID = "", Level5ID = "",
                ProvinceCD = "", DistributorID = distributorId,
                SaleSupCD = saleSupCD, RouteCD = routeCD, SalesmanCD = salesmanCD,
                UserName = username,
                FirstTimeSync = (TimeOnly?)null, FirstTimeVisitAM = (TimeOnly?)null,
                FirstTimeVisitPM = (TimeOnly?)null, LastTimeVisit = (TimeOnly?)null,
                OrderDistanceValid = (decimal?)null, TimeVisit = (int?)null,
                STCode = stCode
            },
            commandTimeout: 60);
        return rows.ToList();
    }
}
