using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

namespace BlazorReporting.Services;

public sealed class SalesAssessmentRow
{
    public string RegionID       { get; set; } = "";
    public string RegionName     { get; set; } = "";
    public string AreaID         { get; set; } = "";
    public string AreaName       { get; set; } = "";
    public int    DistributorID  { get; set; }
    public string DistributorName{ get; set; } = "";
    public string SaleSupName    { get; set; } = "";
    public string RouteName      { get; set; } = "";
    public string SalesmanName   { get; set; } = "";
    public DateTime? VisitDate   { get; set; }
    public int IsMCP             { get; set; }
    public int OutletMustVisit   { get; set; }
    public int OutletVisited     { get; set; }
    public int OrderCount        { get; set; }
    public decimal TotalAmount   { get; set; }
    public decimal MTDTotalAmount{ get; set; }
    public int MTDOutletMustVisit{ get; set; }
    public int MTDOutletVisited  { get; set; }
    public int MTDOrderCount     { get; set; }
    public DateTime? FirstSyncTime { get; set; }
    public decimal PercentWorkedDay{ get; set; }
    public string SalesmanID      { get; set; } = "";
    public decimal Visit_MCP      { get; set; }
    public decimal SO_MCP         { get; set; }
}

public sealed class DashboardKpi
{
    public decimal TargetMonth       { get; set; }
    public decimal AchievedMonth     { get; set; }
    public decimal AchievedMonthPct  { get; set; }
    public decimal RevenueToday      { get; set; }
    public int TotalSM               { get; set; }
    public int TotalSMHasSync        { get; set; }
    public int TotalSMNotSync        { get; set; }
    public int TotalSMHasVisit       { get; set; }
    public int TotalSMNotVisit       { get; set; }
    public int TotalSMHasOrder       { get; set; }
    public int TotalVisitPlan        { get; set; }
    public int TotalOutletVisited    { get; set; }
    public int TotalOrder            { get; set; }
    public decimal TotalAmount       { get; set; }
    public decimal MTDTotalAmount    { get; set; }
}

// Result set 1 của pp_GetDashboardKPI
file sealed class KpiAggRow
{
    public int TotalSM            { get; set; }
    public int TotalSMHasSync     { get; set; }
    public int TotalSMHasVisit    { get; set; }
    public int TotalSMHasOrder    { get; set; }
    public int TotalVisitPlan     { get; set; }
    public int TotalOutletVisited { get; set; }
    public int TotalOrder         { get; set; }
    public decimal RevenueToday   { get; set; }
    public decimal AchievedMonth  { get; set; }
}

// Result set 2 của pp_GetDashboardKPI
file sealed class TargetAggRow
{
    public decimal TargetMonth { get; set; }
}


// Dải level (0-20%, 20-40%...) từ CustomColorSetting
public sealed class LevelBand
{
    public string SettingName { get; set; } = "";
    public decimal ValueFrom  { get; set; }
    public decimal ValueTo    { get; set; }
    public string Color       { get; set; } = "";
}

// Kết quả pie chart level
public sealed class LevelSlice
{
    public string Label { get; set; } = "";
    public int Count    { get; set; }
    public string Color { get; set; } = "";
}

// Kết quả line chart visit/order theo ngày
public sealed class DayChartPoint
{
    public string Day           { get; set; } = ""; // "01".."31"
    public int OutletMustVisit  { get; set; }
    public int OutletVisited    { get; set; }
    public int OrderCount       { get; set; }
    public decimal VisitMCP     { get; set; } // %
    public decimal SOMCP        { get; set; } // %
}

public sealed class CustomColorSettingRow
{
    public int ID                { get; set; }
    public string SettingName    { get; set; } = "";
    public decimal? ValueFrom    { get; set; }
    public decimal? ValueTo      { get; set; }
    public string Color          { get; set; } = "";
    public string Type           { get; set; } = "";
    public int? NumberIndex      { get; set; }
}

public sealed class DashboardService
{
    private readonly string _cs;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan _cacheTtl = TimeSpan.FromMinutes(5);

    public DashboardService(IConfiguration cfg, IMemoryCache cache)
    {
        _cs    = cfg.GetConnectionString("DefaultConnection")!;
        _cache = cache;
    }

    private SqlConnection Conn() => new(_cs);

    // ── KPI tổng hợp — 1 SP, 2 result sets, cache 5 phút ────────────

    public async Task<DashboardKpi> GetKpiAsync(DateTime date, string username)
    {
        var key = $"dash:kpi:v4:{date:yyyyMMdd}:{username}";
        if (_cache.TryGetValue(key, out DashboardKpi? cached) && cached is not null)
            return cached;

        await using var conn = Conn();
        await conn.OpenAsync();

        using var multi = await conn.QueryMultipleAsync(
            "EXEC pp_GetDashboardKPI @Date, @UserName",
            new { Date = date.Date, UserName = username },
            commandTimeout: 30);

        var agg    = await multi.ReadFirstOrDefaultAsync<KpiAggRow>() ?? new KpiAggRow();
        var target = await multi.ReadFirstOrDefaultAsync<TargetAggRow>() ?? new TargetAggRow();

        var kpi = new DashboardKpi
        {
            TargetMonth       = target.TargetMonth,
            AchievedMonth     = agg.AchievedMonth,
            AchievedMonthPct  = target.TargetMonth > 0 ? agg.AchievedMonth * 100m / target.TargetMonth : 0,
            RevenueToday      = agg.RevenueToday,
            TotalSM           = agg.TotalSM,
            TotalSMHasSync    = agg.TotalSMHasSync,
            TotalSMNotSync    = agg.TotalSM - agg.TotalSMHasSync,
            TotalSMHasVisit   = agg.TotalSMHasVisit,
            TotalSMNotVisit   = agg.TotalSMHasSync - agg.TotalSMHasVisit,
            TotalSMHasOrder   = agg.TotalSMHasOrder,
            TotalVisitPlan    = agg.TotalVisitPlan,
            TotalOutletVisited= agg.TotalOutletVisited,
            TotalOrder        = agg.TotalOrder,
            MTDTotalAmount    = agg.AchievedMonth,
        };

        _cache.Set(key, kpi, _cacheTtl);
        return kpi;
    }

    // ── Detail table — chỉ lấy columns cần thiết, cache 5 phút ──────

    public async Task<List<SalesAssessmentRow>> GetSalesAssessmentAsync(
        DateTime date, string username,
        string regionId = "", string areaId = "", string distributorId = "0",
        string saleSupId = "", string routeId = "", string salesmanId = "")
    {
        var key = $"dash:detail:{date:yyyyMMdd}:{username}:{regionId}:{areaId}:{distributorId}:{saleSupId}:{routeId}:{salesmanId}";
        if (_cache.TryGetValue(key, out List<SalesAssessmentRow>? cached) && cached is not null)
            return cached;

        await using var conn = Conn();
        var rows = await conn.QueryAsync<SalesAssessmentRow>(
            "EXEC pp_ReportSalesAssessment @FromDate, @RegionID, @AreaID, @ProvinceID, @DistributorID, @SaleSupID, @RouteID, @SalesmanID, @UserName",
            new
            {
                FromDate      = date,
                RegionID      = regionId,
                AreaID        = areaId,
                ProvinceID    = "",
                DistributorID = int.TryParse(distributorId, out var d) ? d : 0,
                SaleSupID     = saleSupId,
                RouteID       = routeId,
                SalesmanID    = salesmanId,
                UserName      = username
            },
            commandTimeout: 60);

        var list = rows.ToList();
        _cache.Set(key, list, _cacheTtl);
        return list;
    }

    // CalcKpi chỉ còn dùng cho chart aggregation từ detail data
    public DashboardKpi CalcKpi(List<SalesAssessmentRow> data, DashboardKpi? baseKpi = null)
    {
        if (baseKpi is not null) return baseKpi;

        // Fallback: tính từ detail data nếu không có baseKpi
        int mcpCount = 0, visitPlan = 0, visited = 0, orders = 0;
        decimal revenueToday = 0, mtd = 0;
        var smIds = new HashSet<string>();
        var smSync = new HashSet<string>();
        var smVisit = new HashSet<string>();
        var smOrder = new HashSet<string>();

        foreach (var r in data)
        {
            smIds.Add(r.SalesmanID);
            if (r.FirstSyncTime.HasValue)  smSync.Add(r.SalesmanID);
            if (r.OutletVisited > 0)       smVisit.Add(r.SalesmanID);
            if (r.OrderCount > 0)          smOrder.Add(r.SalesmanID);
            if (r.IsMCP == 1)
            {
                visitPlan    += r.OutletMustVisit;
                visited      += r.OutletVisited;
                orders       += r.OrderCount;
                revenueToday += r.TotalAmount;
            }
            mtd += r.MTDTotalAmount;
        }

        return new DashboardKpi
        {
            TotalSM           = smIds.Count,
            TotalSMHasSync    = smSync.Count,
            TotalSMNotSync    = smIds.Count - smSync.Count,
            TotalSMHasVisit   = smVisit.Count,
            TotalSMNotVisit   = smSync.Count - smVisit.Count,
            TotalSMHasOrder   = smOrder.Count,
            TotalVisitPlan    = visitPlan,
            TotalOutletVisited= visited,
            TotalOrder        = orders,
            RevenueToday      = revenueToday,
            MTDTotalAmount    = mtd,
            AchievedMonth     = mtd,
        };
    }

    // ── Chart VT trong tháng — tính trực tiếp từ SalesAssessmentRow ──
    // Không gọi pp_GetReportVisitInMonth (150s) — SalesAssessmentRow có đủ cột
    // Bám sát logic GetChartDataInMonthVisit() DMS2.0: group by VisitDate, IsMCP=1
    public List<DayChartPoint> CalcDayChart(List<SalesAssessmentRow> data)
    {
        return data
            .Where(x => x.IsMCP == 1 && x.OutletMustVisit > 0 && x.VisitDate.HasValue)
            .GroupBy(x => x.VisitDate!.Value.Date)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                int plan    = g.Sum(x => x.OutletMustVisit);
                int visited = g.Sum(x => x.OutletVisited);
                int orders  = g.Sum(x => x.OrderCount);
                return new DayChartPoint
                {
                    Day             = g.Key.ToString("dd"),
                    OutletMustVisit = plan,
                    OutletVisited   = visited,
                    OrderCount      = orders,
                    VisitMCP        = plan > 0 ? Math.Round((decimal)visited * 100 / plan, 1) : 0,
                    SOMCP           = plan > 0 ? Math.Round((decimal)orders  * 100 / plan, 1) : 0,
                };
            }).ToList();
    }

    // ── Pie chart Visit/Order Level — tính từ BLSalesKPI + CustomColorSetting ──
    // Bám sát logic GetReportVisitLevel/GetReportOrderLevel DMS2.0
    // Không gọi pp_ReportOrderIndexLevel (140s) — tính trực tiếp từ data đã có

    // Dải level từ CustomSetting (ReportOrderIndexLevel*Min/Max)
    // Bám sát pp_ReportOrderIndexLevel — dùng chung cho cả Visit và Order
    public async Task<List<LevelBand>> GetLevelBandsAsync()
    {
        const string key = "dash:bands:level:v2";
        if (_cache.TryGetValue(key, out List<LevelBand>? cached) && cached is not null)
            return cached;

        await using var conn = Conn();
        // Tái tạo logic SP: group SettingCode theo prefix, lấy Min/Max
        var rows = await conn.QueryAsync<(string Code, string Name, decimal Value)>(
            """
            SELECT SettingCode, SettingName, CONVERT(DECIMAL(18,2), SettingValue) AS Value
            FROM CustomSetting
            WHERE SettingCode LIKE 'ReportOrderIndexLevel%'
            ORDER BY SettingCode
            """);

        var bands = rows
            .GroupBy(r => LEFT6(r.Code))   // Level1, Level2...
            .Select(g => new LevelBand
            {
                SettingName = g.Key,
                ValueFrom   = g.FirstOrDefault(x => x.Name.EndsWith("Min")).Value,
                ValueTo     = g.FirstOrDefault(x => x.Name.EndsWith("Max")).Value,
                Color       = ""
            })
            .OrderBy(b => b.ValueFrom)
            .ToList();

        // Gán màu từ CustomColorSetting nếu có (Type='Order')
        var colors = await conn.QueryAsync<(string Name, string Color)>(
            "SELECT SettingName, Color FROM CustomColorSetting WHERE [Type]='Order' ORDER BY ValueFrom");
        var colorMap = colors.ToList();
        for (int i = 0; i < bands.Count && i < colorMap.Count; i++)
        {
            var c = colorMap[i].Color?.Trim() ?? "";
            bands[i].Color = c.StartsWith('#') ? c : "#" + c;
        }

        _cache.Set(key, bands, TimeSpan.FromHours(1));
        return bands;
    }

    private static string LEFT6(string code)
    {
        // "ReportOrderIndexLevel1Min" → "Level1"
        var idx = code.IndexOf("Level", StringComparison.Ordinal);
        if (idx < 0) return code;
        var rest = code[(idx + 5)..]; // "1Min" or "1Max"
        var num = new string(rest.TakeWhile(char.IsDigit).ToArray());
        return "Level " + num;
    }

    // Phân loại TDV vào dải level — bám sát pp_ReportOrderIndexLevel DMS2.0
    // Dùng MTDVisit_MCP (Visit_MCP tháng) hoặc MTDSO_MCP (SO_MCP tháng)
    // IsValue = 1 nếu SM nằm trong dải đó — đúng logic gốc
    public List<LevelSlice> CalcLevelPie(List<SalesAssessmentRow> data, List<LevelBand> bands, string metricType)
    {
        // Lấy % MTD của từng SM (group by SalesmanID, IsMCP=1, lấy row có giá trị lớn nhất)
        var smPcts = data
            .Where(x => x.IsMCP == 1)
            .GroupBy(x => x.SalesmanID)
            .Select(g =>
            {
                // MTDVisit_MCP và MTDSO_MCP là % MTD tháng — dùng giá trị max trong group
                decimal pct = metricType == "Visited"
                    ? g.Max(x => x.Visit_MCP)   // Visit_MCP = MTDVisit_MCP trong SalesAssessmentRow
                    : g.Max(x => x.SO_MCP);      // SO_MCP = MTDSO_MCP
                return pct;
            }).ToList();

        return bands.Select(band =>
        {
            int count = smPcts.Count(pct =>
                pct >= band.ValueFrom && pct < band.ValueTo);
            return new LevelSlice
            {
                Label = $"{(int)band.ValueFrom}-{(int)band.ValueTo}%",
                Count = count,
                Color = string.IsNullOrEmpty(band.Color) ? "#94a3b8"
                        : band.Color.StartsWith('#') ? band.Color : "#" + band.Color,
            };
        }).ToList();
    }

    // ── Custom Color Setting ─────────────────────────────────────────

    public async Task<List<CustomColorSettingRow>> GetColorSettingsAsync()
    {
        await using var conn = Conn();
        var rows = await conn.QueryAsync<CustomColorSettingRow>(
            "SELECT ID, SettingName, ValueFrom, ValueTo, Color, [Type], NumberIndex FROM CustomColorSetting ORDER BY NumberIndex, ID");
        return rows.ToList();
    }

    public async Task<CustomColorSettingRow?> AddColorSettingAsync(CustomColorSettingRow s)
    {
        await using var conn = Conn();
        var id = await conn.QueryFirstAsync<int>(
            """
            INSERT INTO CustomColorSetting (SettingName, ValueFrom, ValueTo, Color, [Type])
            OUTPUT INSERTED.ID
            VALUES (@SettingName, @ValueFrom, @ValueTo, @Color, @Type)
            """, s);
        s.ID = id;
        return s;
    }

    public async Task<bool> UpdateColorSettingAsync(CustomColorSettingRow s)
    {
        await using var conn = Conn();
        var n = await conn.ExecuteAsync(
            "UPDATE CustomColorSetting SET SettingName=@SettingName, ValueFrom=@ValueFrom, ValueTo=@ValueTo, Color=@Color, [Type]=@Type WHERE ID=@ID", s);
        return n > 0;
    }

    public async Task<bool> DeleteColorSettingAsync(int id)
    {
        await using var conn = Conn();
        var n = await conn.ExecuteAsync("DELETE FROM CustomColorSetting WHERE ID=@ID", new { ID = id });
        return n > 0;
    }
}
