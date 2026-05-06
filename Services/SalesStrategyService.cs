using Microsoft.ML;
using Microsoft.ML.Transforms.TimeSeries;
using BlazorReporting.Data;

namespace BlazorReporting.Services;

// ══════════════════════════════════════════════════════════════
//  SalesStrategyService
//  Train ML.NET SSA cho từng cặp (Tỉnh × Sản phẩm),
//  kết hợp yếu tố mùa vụ + khí hậu + tồn kho để
//  đưa ra khuyến nghị Nhập / Xuất / Duy trì.
// ══════════════════════════════════════════════════════════════

public sealed class SalesStrategyService
{
    private readonly MLContext            _ml      = new(seed: 42);
    private readonly SeasonalFactorService _seasonal = new();

    // ── Phân tích toàn bộ ────────────────────────────────────
    public async Task<StrategyResult> AnalyzeAsync(
        IReadOnlyList<ProvinceSalesHistory> history,
        IProgress<(int Done, int Total)>? progress = null,
        CancellationToken ct = default)
    {
        if (history.Count == 0)
            return StrategyResult.Empty;

        var groups = history
            .GroupBy(h => (h.ProvinceCode, h.InventoryCD, h.InventoryName))
            .ToList();

        var results   = new System.Collections.Concurrent.ConcurrentBag<ProvinceProductStrategy>();
        int processed = 0;
        int total     = groups.Count;

        await Parallel.ForEachAsync(groups,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount - 1),
                CancellationToken      = ct
            },
            (group, _) =>
            {
                var (province, cd, name) = group.Key;
                var pts = group
                    .OrderBy(h => h.Year).ThenBy(h => h.Month)
                    .ToArray();

                var strategy = ComputeStrategy(province, cd, name, pts);
                results.Add(strategy);

                var done = Interlocked.Increment(ref processed);
                progress?.Report((done, total));
                return ValueTask.CompletedTask;
            });

        var list = results
            .OrderBy(r => r.ProvinceCode)
            .ThenByDescending(r => Math.Abs(r.AdjustedGrowthRate))
            .ToList();

        return new StrategyResult(list);
    }

    // ── Tính chiến lược cho 1 cặp Tỉnh × SP ─────────────────
    private ProvinceProductStrategy ComputeStrategy(
        string province, string cd, string name,
        ProvinceSalesHistory[] pts)
    {
        int n = pts.Length;
        var qtyVals = pts.Select(p => (float)p.TotalQty).ToArray();
        var amtVals = pts.Select(p => (float)p.TotalAmount).ToArray();

        // ── SSA forecast ──────────────────────────────────────
        bool usedSSA;
        float[] predQty, predAmt;
        if (n >= 4)
        {
            var (qPred, qSSA) = TrySSA(qtyVals, 3);
            var (aPred, aSSA) = TrySSA(amtVals, 3);
            predQty = qPred; predAmt = aPred;
            usedSSA = qSSA && aSSA;
        }
        else
        {
            predQty = LinearPredict(qtyVals, 3);
            predAmt = LinearPredict(amtVals, 3);
            usedSSA = false;
        }

        // ── Trung bình thực tế & dự đoán ─────────────────────
        int lookback   = Math.Min(3, n);
        float avgQty3M = qtyVals.TakeLast(lookback).Average();
        float avgAmt3M = amtVals.TakeLast(lookback).Average();
        float predQtyAvg = predQty.Average();
        float predAmtAvg = predAmt.Average();
        float growthRate  = avgQty3M == 0 ? 0f
            : (predQtyAvg - avgQty3M) / avgQty3M;

        // ── Yếu tố mùa vụ + khí hậu + tồn kho ───────────────
        var lastPt  = pts[^1];
        var factors = _seasonal.Analyze(province, name,
                          lastPt.Year, lastPt.Month,
                          qtyVals.Select(v => (long)v).ToArray());

        float seasonalMult = factors.AvgSeasonalFactor;
        var   invSig       = factors.InventorySignal;

        // Điều chỉnh tăng trưởng bằng hệ số mùa vụ
        // (chỉ áp dụng 40% trọng số để SSA vẫn là chủ đạo)
        float adjustedGrowth = growthRate + (seasonalMult - 1.0f) * 0.4f;

        // Điều chỉnh thêm từ tín hiệu tồn kho
        adjustedGrowth += invSig switch
        {
            InventorySignal.SurgeBuying     => +0.08f,   // mua gom → cần nhập gấp
            InventorySignal.PossibleStockout=> +0.10f,   // có thể hết hàng → ưu tiên nhập
            InventorySignal.SharpDecline    => -0.08f,   // giảm mạnh → đẩy hàng
            _ => 0f
        };

        // ── Hành động cuối ───────────────────────────────────
        var (action, label, icon, color, badgeClass) = adjustedGrowth switch
        {
            > 0.20f  => ("import",   "Nhập nhiều",  "bi-arrow-up-circle-fill",         "#15803d", "strategy-import-strong"),
            > 0.07f  => ("import",   "Nhập thêm",   "bi-arrow-up-right-circle-fill",   "#16a34a", "strategy-import"),
            < -0.20f => ("export",   "Xuất gấp",    "bi-arrow-down-circle-fill",       "#b91c1c", "strategy-export-strong"),
            < -0.07f => ("export",   "Xuất bớt",    "bi-arrow-down-right-circle-fill", "#dc2626", "strategy-export"),
            _        => ("maintain", "Duy trì",      "bi-dash-circle-fill",             "#6b7280", "strategy-maintain")
        };

        // ── Lý do chi tiết ────────────────────────────────────
        var reasonParts = new List<string>();

        // SSA/Linear trend
        reasonParts.Add(growthRate > 0.05f
            ? $"Xu hướng bán tăng {growthRate:+0.0%;-0.0%}"
            : growthRate < -0.05f
            ? $"Xu hướng bán giảm {growthRate:+0.0%;-0.0%}"
            : "Doanh số ổn định");

        // Seasonal
        if (MathF.Abs(seasonalMult - 1f) > 0.08f)
            reasonParts.Add(seasonalMult > 1f
                ? $"mùa vụ thuận lợi (×{seasonalMult:0.0})"
                : $"mùa vụ kém thuận lợi (×{seasonalMult:0.0})");

        // Inventory signal
        reasonParts.Add(invSig switch
        {
            InventorySignal.SurgeBuying      => "⚡ khách đang mua gom số lượng lớn",
            InventorySignal.PossibleStockout => "⚠️ có dấu hiệu hết hàng định kỳ",
            InventorySignal.SharpDecline     => "📉 tốc độ bán giảm đột ngột",
            InventorySignal.Increasing       => "📈 đơn hàng đang tăng dần",
            InventorySignal.Slowing          => "🐌 đơn hàng đang chậm lại",
            _ => ""
        });

        // Climate
        if (!string.IsNullOrEmpty(factors.ClimateContext))
            reasonParts.Add(factors.ClimateContext);

        // Upcoming events
        var nextEvents = factors.MonthFactors
            .SelectMany(m => m.Events).Distinct().Take(2).ToList();
        reasonParts.AddRange(nextEvents);

        string reason = string.Join(" · ", reasonParts.Where(r => !string.IsNullOrEmpty(r)));

        // ── Độ tin cậy ───────────────────────────────────────
        float confidence = Math.Min(1f, n / 12f)
            * (usedSSA ? 1f : 0.65f)
            * (invSig == InventorySignal.Unknown ? 0.9f : 1f);

        // ── Peak month ────────────────────────────────────────
        var peakPt = pts.MaxBy(p => p.TotalQty);
        string seasonNote = peakPt != null
            ? $"Tháng cao điểm: T{peakPt.Month} ({peakPt.TotalQty:N0} SP)"
            : "";

        return new ProvinceProductStrategy(
            ProvinceCode      : province,
            InventoryCD       : cd,
            InventoryName     : name,
            AvgQty3M          : (long)avgQty3M,
            AvgAmount3M       : (decimal)avgAmt3M,
            PredQty3M         : (long)predQtyAvg,
            PredAmount3M      : (decimal)predAmtAvg,
            PredQtyMonths     : predQty.Select(v => (long)v).ToArray(),
            GrowthRate        : growthRate,
            AdjustedGrowthRate: adjustedGrowth,
            SeasonalMultiplier: seasonalMult,
            Action            : action,
            ActionLabel       : label,
            ActionIcon        : icon,
            ActionColor       : color,
            BadgeClass        : badgeClass,
            Confidence        : confidence,
            DataPoints        : n,
            UsedSSA           : usedSSA,
            Reason            : reason,
            SeasonNote        : seasonNote,
            Region            : factors.Region,
            ProductCategory   : factors.Category,
            ClimateContext    : factors.ClimateContext,
            InventorySignal   : invSig,
            UpcomingEvents    : factors.MonthFactors
                                       .SelectMany(m => m.Events)
                                       .Distinct().Take(3).ToList(),
            MonthFactors      : factors.MonthFactors);
    }

    // ── ML.NET SSA ────────────────────────────────────────────
    private (float[] Predicted, bool UsedSSA) TrySSA(float[] values, int horizon)
    {
        try
        {
            int ws     = Math.Max(2, Math.Min(values.Length / 2, 12));
            var dv     = _ml.Data.LoadFromEnumerable(values.Select(v => new Dp { V = v }));
            var pipe   = _ml.Forecasting.ForecastBySsa(
                             outputColumnName: "F", inputColumnName: "V",
                             windowSize: ws, seriesLength: values.Length,
                             trainSize: values.Length, horizon: horizon,
                             confidenceLevel: 0.90f,
                             confidenceLowerBoundColumn: "Lo",
                             confidenceUpperBoundColumn: "Hi");
            var engine = pipe.Fit(dv).CreateTimeSeriesEngine<Dp, Fo>(_ml);
            var out_   = engine.Predict();
            if (out_.F.Any(v => !float.IsFinite(v)))
                return (LinearPredict(values, horizon), false);
            return (out_.F.Select(v => MathF.Max(0f, v)).ToArray(), true);
        }
        catch { return (LinearPredict(values, horizon), false); }
    }

    private static float[] LinearPredict(float[] values, int horizon)
    {
        int n = values.Length;
        if (n == 0) return new float[horizon];
        if (n == 1) return Enumerable.Repeat(values[0], horizon).ToArray();
        double sx = 0, sy = 0, sxy = 0, sx2 = 0;
        for (int i = 0; i < n; i++)
        { sx += i; sy += values[i]; sxy += i * values[i]; sx2 += (double)i * i; }
        double d = n * sx2 - sx * sx;
        double s = d == 0 ? 0 : (n * sxy - sx * sy) / d;
        double b = (sy - s * sx) / n;
        return Enumerable.Range(n, horizon)
                         .Select(i => (float)Math.Max(0, b + s * i)).ToArray();
    }

    private class Dp { public float V { get; set; } }
    private class Fo { public float[] F { get; set; } = []; public float[] Lo { get; set; } = []; public float[] Hi { get; set; } = []; }
}

// ── Result records ────────────────────────────────────────────

public sealed record ProvinceProductStrategy(
    string              ProvinceCode,
    string              InventoryCD,
    string              InventoryName,
    long                AvgQty3M,
    decimal             AvgAmount3M,
    long                PredQty3M,
    decimal             PredAmount3M,
    long[]              PredQtyMonths,
    float               GrowthRate,           // Tăng trưởng thuần SSA
    float               AdjustedGrowthRate,   // Sau điều chỉnh mùa vụ + tồn kho
    float               SeasonalMultiplier,   // Hệ số mùa vụ
    string              Action,
    string              ActionLabel,
    string              ActionIcon,
    string              ActionColor,
    string              BadgeClass,
    float               Confidence,
    int                 DataPoints,
    bool                UsedSSA,
    string              Reason,
    string              SeasonNote,
    // ── Yếu tố môi trường ────
    string              Region,
    string              ProductCategory,
    string              ClimateContext,
    InventorySignal     InventorySignal,
    List<string>        UpcomingEvents,
    List<MonthSeasonFactor> MonthFactors);

public sealed class StrategyResult
{
    public static readonly StrategyResult Empty = new([]);
    public IReadOnlyList<ProvinceProductStrategy> All { get; }

    public int TotalProvinces => All.Select(r => r.ProvinceCode).Distinct().Count();
    public int TotalProducts  => All.Select(r => r.InventoryCD).Distinct().Count();
    public int ImportCount    => All.Count(r => r.Action == "import");
    public int ExportCount    => All.Count(r => r.Action == "export");
    public int MaintainCount  => All.Count(r => r.Action == "maintain");

    public IReadOnlyList<ProvinceProductStrategy> TopImport =>
        All.Where(r => r.Action == "import")
           .OrderByDescending(r => r.AdjustedGrowthRate).Take(20).ToList();
    public IReadOnlyList<ProvinceProductStrategy> TopExport =>
        All.Where(r => r.Action == "export")
           .OrderBy(r => r.AdjustedGrowthRate).Take(20).ToList();

    public IReadOnlyList<ProvinceSummary> ProvinceSummaries =>
        All.GroupBy(r => r.ProvinceCode)
           .Select(g => new ProvinceSummary(
               g.Key,
               g.Count(r => r.Action == "import"),
               g.Count(r => r.Action == "export"),
               g.Count(r => r.Action == "maintain"),
               g.Sum(r => r.PredAmount3M)))
           .OrderByDescending(p => p.PredAmount3M).ToList();

    public StrategyResult(IReadOnlyList<ProvinceProductStrategy> all) => All = all;
}

public sealed record ProvinceSummary(
    string  ProvinceCode,
    int     ImportCount,
    int     ExportCount,
    int     MaintainCount,
    decimal PredAmount3M);
