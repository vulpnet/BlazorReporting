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
    // Synchronous specific
    public decimal LongtitudeBase  { get; set; }
    public decimal LatitudeBase    { get; set; }
    public decimal LongtitudeSync  { get; set; }
    public decimal LatitudeSync    { get; set; }
    public decimal DistanceWithBase{ get; set; }
    public int    ValidDistance    { get; set; }
    public DateTime? SystemSyncDate{ get; set; }
    public string SyncType         { get; set; } = "";
    public int    IsTimeValid      { get; set; }
    public TimeSpan? TimeDisparity { get; set; }
    public string SalesSupCD       { get; set; } = "";
    // WorkWith / ReviewWorkWith specific
    public string WWID             { get; set; } = "";
    public string WWName           { get; set; } = "";
    public string WWTitle          { get; set; } = "";
    public string RouteID          { get; set; } = "";
    public string SalesManID       { get; set; } = "";
    public string SalesManName     { get; set; } = "";
    public int    SMWorkAM         { get; set; }
    public int    SMWorkPM         { get; set; }
    public int    OrderSuccessAM   { get; set; }
    public int    OrderSuccessPM   { get; set; }
    public int    GPSInvalid       { get; set; }
    public int    APMInvalid       { get; set; }
    public int    VisitValidAM     { get; set; }
    public int    VisitValidPM     { get; set; }
    public int    VisitValid       { get; set; }
    public string DistributorCode  { get; set; } = "";
    public string VisitDateStr     { get; set; } = "";
    // Outlet GPS Invalid specific
    public string LocationCD     { get; set; } = "";
    public string OutletCD       { get; set; } = "";
    public bool   IsActive       { get; set; }
    public string City           { get; set; } = "";
    // Eval Report specific
    public string EvalState          { get; set; } = "";
    public string MarkingAssign      { get; set; } = "";
    public int    TotalImg           { get; set; }
    public int    ImagMarking        { get; set; }
    public int    ImgApproved        { get; set; }
    public int    ImgPassProgram     { get; set; }
    public int    ImgRejected        { get; set; }
    public int    ImgReMarking       { get; set; }
    public int    TotalOulet         { get; set; }
    public int    OuletHasMarking    { get; set; }
    // Review Order specific
    public string OrderCode          { get; set; } = "";
    public string OutletCD3          { get; set; } = "";
    public int    DeliveryStatus     { get; set; }
    public DateTime? OrderDate       { get; set; }
    public decimal TotalOrder        { get; set; }
    // Digital Content specific
    public string FileName           { get; set; } = "";
    public DateTime? StartTime       { get; set; }
    public DateTime? EndTime         { get; set; }
    // PDA / IMEI specific
    public string IMEI               { get; set; } = "";
    public DateTime? ActiveDate      { get; set; }
    public DateTime? CreateDatetime  { get; set; }
    // UserActionLogTerritory specific
    public string TerritoryCD        { get; set; } = "";
    public string TerritoryName      { get; set; } = "";
    public string ActionType         { get; set; } = "";
    public DateTime? ActionDate      { get; set; }
    // KPI Summary (pp_GetBLSalesKPI) specific
    public int    MustVisit      { get; set; }
    public int    Visited        { get; set; }
    public decimal VisitRate     { get; set; }
    public int    HasOrderCount  { get; set; }
    public decimal OrderRate     { get; set; }
    public decimal Revenue       { get; set; }
    public string ProgramName2   { get; set; } = "";
    // Image Program specific
    public string ImageFile      { get; set; } = "";
    public string PlanogramName  { get; set; } = "";
    public DateTime? SyncDate    { get; set; }
    public string MerchandiseReason { get; set; } = "";
    // Visit Reason specific
    public string VisitNotOrder  { get; set; } = "";
    public decimal VisitNotOrderRate { get; set; }
    // SalesEffective / SummarySales specific
    public decimal TargetRevenue     { get; set; }
    public int    MTDOutletMustVisit { get; set; }
    public int    MTDOutletVisited   { get; set; }
    public int    MTDOrderCount      { get; set; }
    // Visit Distance / Sales Fundamentals specific
    public string OutletCD2          { get; set; } = "";
    public decimal VisitDistance     { get; set; }
    public decimal TotalDistance     { get; set; }
    public int    VisitCount         { get; set; }
    // New/Update Outlet specific
    public string Address            { get; set; } = "";
    public string OutletType         { get; set; } = "";
    public string Channel            { get; set; } = "";
    public string ApprovalStatus     { get; set; } = "";
    public DateTime? CreatedDate     { get; set; }
    // Adoption specific
    public int    NewOutlet          { get; set; }
    public int    AdoptionOutlet     { get; set; }
    public decimal AdoptionRate      { get; set; }
    // Golden Store specific
    public string GoldenStoreLevel   { get; set; } = "";
    public int    GoldenScore        { get; set; }
    // General Manager specific
    public decimal SalesActMTD       { get; set; }
    public decimal SalesObj          { get; set; }
    public int    OutletHasOrder     { get; set; }
    // InDay DSR specific
    public int    OutletMustVisitMTD { get; set; }
    public int    OutletVisitedMTD   { get; set; }
    public int    OutletHasOrderMTD  { get; set; }
    public int    CountVisitDateMTD  { get; set; }
    public string SalesmanID         { get; set; } = "";
    // Visit Reason specific
    public int    VisitNotOrderCount { get; set; }
    public string ReasonCode         { get; set; } = "";
    public string ReasonName         { get; set; } = "";
    public int    ReasonCount        { get; set; }
}

// ── Service ─────────────────────────────────────────────────────────

public sealed class ReportService
{
    private readonly string _cs;

    public ReportService(IConfiguration cfg)
        => _cs = cfg.GetConnectionString("DefaultConnection")!;

    private SqlConnection Conn() => new(_cs);

    // ── Report Visit KPI Summary (Tab 1) ─────────────────────────────

    public Task<List<GenericReportRow>> GetReportVisitKpiAsync(
        DateTime date, string username,
        string st0="", string st1="", int distId=0, string sup="", string route="", string stCode="")
        => ExecAsync("EXEC pp_GetBLSalesKPI @VisitDate,@STLvl0CD,@STLvl1CD,@STLvl2CD,@STLvl3CD,@STLvl4CD,@DistributorID,@SaleSupCD,@RouteCD,@UserName,@STCode",
            new{VisitDate=date,STLvl0CD=st0,STLvl1CD=st1,STLvl2CD="",STLvl3CD="",STLvl4CD="",DistributorID=distId,SaleSupCD=sup,RouteCD=route,UserName=username,STCode=stCode});

    // ── Report Visit Image Program (Tab 3) ────────────────────────────

    public Task<List<GenericReportRow>> GetReportVisitImageAsync(
        DateTime date, string username,
        string st0="", string st1="", int distId=0, string sup="", string route="", string stCode="")
        => ExecAsync("EXEC pp_ReportImageProgram @VisitDate,@STLvl0CD,@STLvl1CD,@STLvl2CD,@STLvl3CD,@STLvl4CD,@DistributorID,@SaleSupCD,@RouteCD,@UserName,@STCode",
            new{VisitDate=date,STLvl0CD=st0,STLvl1CD=st1,STLvl2CD="",STLvl3CD="",STLvl4CD="",DistributorID=distId,SaleSupCD=sup,RouteCD=route,UserName=username,STCode=stCode});

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

    // ── 16. Synchronous (đồng bộ) ────────────────────────────────────

    public Task<List<GenericReportRow>> GetReportSynchronousAsync(
        DateTime from, DateTime to, string username,
        string st0="", string st1="", int distId=0, string sup="", string stCode="", TimeSpan? timeRegular=null)
        => ExecAsync("EXEC pp_ReportSyschronous @FromDate,@ToDate,@STLvl0CD,@STLvl1CD,@STLvl2CD,@STLvl3CD,@STLvl4CD,@DistributorID,@SaleSupCD,@TimeRegular,@UserName,@STCode",
            new{FromDate=from,ToDate=to,STLvl0CD=st0,STLvl1CD=st1,STLvl2CD="",STLvl3CD="",STLvl4CD="",DistributorID=distId,SaleSupCD=sup,TimeRegular=timeRegular??new TimeSpan(8,0,0),UserName=username,STCode=stCode});

    // ── 17. Review WorkWith ───────────────────────────────────────────

    public Task<List<GenericReportRow>> GetReportReviewWorkWithAsync(
        DateTime from, string username, string regionId="", string areaId="", int distId=0)
        => ExecAsync("EXEC pp_ReportReviewWorkWith @FromDate,@ToDate,@RegionID,@AreaID,@DistributorID,@SaleSupID,@SalesmanID,@UserName",
            new{FromDate=from,ToDate=DateTime.Now,RegionID=regionId,AreaID=areaId,DistributorID=distId,SaleSupID="",SalesmanID="",UserName=username});

    // ── 18. WorkWith ──────────────────────────────────────────────────

    public Task<List<GenericReportRow>> GetReportWorkWithAsync(
        DateTime from, string username, string regionId="", string areaId="", int distId=0, string supId="")
        => ExecAsync("EXEC pp_ReportWorkWith @FromDate,@RegionID,@AreaID,@DistributorID,@SaleSupID,@SalesmanID,@UserName,@Type",
            new{FromDate=from,RegionID=regionId,AreaID=areaId,DistributorID=distId,SaleSupID=supId,SalesmanID="",UserName=username,Type=3});

    // ── 19. Reason Visit ──────────────────────────────────────────────

    public Task<List<GenericReportRow>> GetReportReasonVisitAsync(
        DateTime from, DateTime to, string username,
        string st0="", string st1="", int distId=0, string sup="", string route="", string stCode="")
        => ExecAsync("EXEC pp_ReportVisitReason @FromDate,@ToDate,@Level1ID,@Level2ID,@Level3ID,@Level4ID,@Level5ID,@DistributorID,@SaleSupCD,@RouteCD,@UserName,@STCode",
            new{FromDate=from,ToDate=to,Level1ID=st0,Level2ID=st1,Level3ID="",Level4ID="",Level5ID="",DistributorID=distId,SaleSupCD=sup,RouteCD=route,UserName=username,STCode=stCode});

    // ── 20. Visit Distance ────────────────────────────────────────────

    public Task<List<GenericReportRow>> GetReportVisitDistanceAsync(
        DateTime from, DateTime to, string username, int distId=0, string sup="", string salesmanId="")
        => ExecAsync("EXEC pp_GetOutletVisitDistance @FromDate,@ToDate,@DistributorID,@SaleSupID,@SalesmanID,@UserName",
            new{FromDate=from,ToDate=to,DistributorID=distId,SaleSupID=sup,SalesmanID=salesmanId,UserName=username});

    // ── 21. New Outlet ────────────────────────────────────────────────

    public Task<List<GenericReportRow>> GetReportNewOutletAsync(
        DateTime from, DateTime to, string username, int distId=0, string sup="", string salesmanId="")
        => ExecAsync("EXEC BSDH_ReportNewOutlets @FromDate,@ToDate,@DistributorID,@SaleSupCode,@SalesmanID,@UserName",
            new{FromDate=from,ToDate=to,DistributorID=distId,SaleSupCode=sup,SalesmanID=salesmanId,UserName=username});

    // ── 22. Update Outlet ─────────────────────────────────────────────

    public Task<List<GenericReportRow>> GetReportUpdateOutletAsync(
        DateTime from, DateTime to, string username, int distId=0, string sup="", string salesmanId="")
        => ExecAsync("EXEC BSDH_ReportUpdateOutlets @FromDate,@ToDate,@DistributorID,@SaleSupCode,@SalesmanID,@UserName",
            new{FromDate=from,ToDate=to,DistributorID=distId,SaleSupCode=sup,SalesmanID=salesmanId,UserName=username});

    // ── 23. Adoption ──────────────────────────────────────────────────

    public Task<List<GenericReportRow>> GetReportAdoptionAsync(
        DateTime from, DateTime to, string username, int distId=0, int supId=0, string salesmanId="")
        => ExecAsync("EXEC BSDH_ReportAdoption @FromDate,@ToDate,@DistributorID,@SaleSupID,@SalesmanID,@UserName",
            new{FromDate=from,ToDate=to,DistributorID=distId,SaleSupID=supId,SalesmanID=salesmanId,UserName=username});

    // ── 24. Golden Store ──────────────────────────────────────────────

    public Task<List<GenericReportRow>> GetReportGoldenStoreAsync(
        DateTime from, string username, int distId=0, int supId=0, string salesmanId="")
        => ExecAsync("EXEC BSDH_ReportGoldenStore @FromDate,@DistributorID,@SaleSupID,@SalesmanID,@UserName",
            new{FromDate=from,DistributorID=distId,SaleSupID=supId,SalesmanID=salesmanId,UserName=username});

    // ── 25. General Manager ───────────────────────────────────────────

    public Task<List<GenericReportRow>> GetReportGeneralManagerAsync(
        DateTime from, string username, int distId=0, int supId=0)
        => ExecAsync("EXEC BSDH_ReportGeneralManager @FromDate,@DistributorID,@SaleSupID,@UserName",
            new{FromDate=from,DistributorID=distId,SaleSupID=supId,UserName=username});

    // ── 26. InDay DSR ─────────────────────────────────────────────────

    public Task<List<GenericReportRow>> GetReportInDayDSRAsync(
        DateTime from, string username, int distId=0, int supId=0, string salesmanId="")
        => ExecAsync("EXEC BSDH_ReportInDayDSR @FromDate,@DistributorID,@SaleSupID,@SalesmanID,@UserName",
            new{FromDate=from,DistributorID=distId,SaleSupID=supId,SalesmanID=salesmanId,UserName=username}, timeout:120);

    // ── 27. Sales Fundamentals (dùng chung SP với VisitDistance) ─────

    public Task<List<GenericReportRow>> GetReportSalesFundamentalsAsync(
        DateTime from, DateTime to, string username, int distId=0, string sup="", string salesmanId="")
        => ExecAsync("EXEC pp_GetOutletVisitDistance @FromDate,@ToDate,@DistributorID,@SaleSupID,@SalesmanID,@UserName",
            new{FromDate=from,ToDate=to,DistributorID=distId,SaleSupID=sup,SalesmanID=salesmanId,UserName=username});

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

    // ── Issues: Usage App ─────────────────────────────────────────────

    public Task<List<GenericReportRow>> GetReportUsageAppAsync(
        DateTime from, DateTime to, string username, int distId=0, string roleId="")
        => ExecAsync("EXEC pp_ReportSFUsageApp @FromDate,@ToDate,@DistributorID,@RoleID",
            new{FromDate=from,ToDate=to,DistributorID=distId,RoleID=roleId});

    // ── Issues: PDA Salesman Active ───────────────────────────────────

    public Task<List<GenericReportRow>> GetPDASalesmanActiveAsync(string salesmanCd="", string imei="")
        => ExecAsync("EXEC pp_PDAGetSalesmanActive @SalesmanCD,@imei",
            new{SalesmanCD=salesmanCd,imei=imei});

    public async Task ResetIMEIAsync(string salesmanCd, string createdBy)
    {
        await using var conn = new Microsoft.Data.SqlClient.SqlConnection(_cs);
        await conn.ExecuteAsync("EXEC pp_PDAResetSalesmanActive @UserName,@CreateByUser",
            new{UserName=salesmanCd,CreateByUser=createdBy});
    }

    // ── Issues: Digital Content (read from DB table) ──────────────────

    public async Task<List<GenericReportRow>> GetDigitalContentAsync()
    {
        await using var conn = new Microsoft.Data.SqlClient.SqlConnection(_cs);
        var rows = await conn.QueryAsync<GenericReportRow>(
            "SELECT FileName,Desc AS Content,StartTime=StartDate,EndTime=EndDate,UserName=CreatedByID,IsValid=CAST(Active AS INT) FROM DigitalContentFileUpload ORDER BY CreatedDateTime DESC",
            commandTimeout: 30);
        return rows.ToList();
    }

    // ── Eval Reports ──────────────────────────────────────────────────

    public Task<List<GenericReportRow>> GetReportEvaluationAsync(
        DateTime from, DateTime to, string username,
        string regionId="", string areaId="", int distId=0, string supId="",
        string routeId="", string salesmanId="", string programId="", string evalId="")
        => ExecAsync("EXEC usp_GetReportEvalBy @FromDate,@ToDate,@RegionID,@AreaID,@SaleSupID,@RouteID,@SalesmanID,@ProgramID,@EvaluationID,@Auditor",
            new{FromDate=from,ToDate=to,RegionID=regionId,AreaID=areaId,SaleSupID=supId,RouteID=routeId,SalesmanID=salesmanId,ProgramID=programId,EvaluationID=evalId,Auditor=username});

    public Task<List<GenericReportRow>> GetReportEvalReasonAsync(
        DateTime from, DateTime to, string username,
        string routeId="", string salesmanId="", string programId="", string evalId="")
        => ExecAsync("EXEC usp_GetReportReasonBySS @Role,@FromDate,@ToDate,@SaleSupID,@RouteID,@SalesmanID,@ProgramID,@EvaluationID,@Auditor",
            new{Role="Admin",FromDate=from,ToDate=to,SaleSupID="",RouteID=routeId,SalesmanID=salesmanId,ProgramID=programId,EvaluationID=evalId,Auditor=username});

    public Task<List<GenericReportRow>> GetReportEvalInventoryAsync(
        DateTime from, DateTime to, string username,
        string regionId="", string areaId="", int distId=0, string routeId="",
        string salesmanId="", string programId="", string evalId="",
        int groupInventory=0, int groupSaleteam=0)
        => ExecAsync("EXEC usp_GetReporInventoryBy @Role,@GroupInventory,@GroupSaleteam,@FromDate,@ToDate,@RegionID,@AreaID,@DistributorID,@SaleSupID,@RouteID,@SalesmanID,@ProgramID,@EvaluationID,@Auditor",
            new{Role="Admin",GroupInventory=groupInventory,GroupSaleteam=groupSaleteam,FromDate=from,ToDate=to,RegionID=regionId,AreaID=areaId,DistributorID=distId,SaleSupID="",RouteID=routeId,SalesmanID=salesmanId,ProgramID=programId,EvaluationID=evalId,Auditor=username});

    // ── Review Order Management ───────────────────────────────────────

    public Task<List<GenericReportRow>> GetReviewOrderListAsync(
        DateTime from, DateTime to, string salesmanId="", int status=2)
        => ExecAsync("EXEC pp_ReportGetListReviewOrder @OrderID,@StartDate,@EndDate,@Status,@SalesmanID",
            new{OrderID="",StartDate=from,EndDate=to,Status=status,SalesmanID=salesmanId});

    public async Task UploadDigitalContentAsync(string fileName, string desc, DateTime? startDate, DateTime? endDate, bool active, string createdBy)
    {
        await using var conn = new Microsoft.Data.SqlClient.SqlConnection(_cs);
        await conn.ExecuteAsync(
            "INSERT INTO DigitalContentFileUpload(FileName,Desc,StartDate,EndDate,Active,CreatedByID,CreatedDateTime) VALUES(@FileName,@Desc,@StartDate,@EndDate,@Active,@CreatedByID,GETDATE())",
            new{FileName=fileName,Desc=desc,StartDate=startDate,EndDate=endDate,Active=active,CreatedByID=createdBy});
    }
}
