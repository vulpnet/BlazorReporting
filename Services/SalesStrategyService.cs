using Microsoft.ML;
using Microsoft.ML.Transforms.TimeSeries;
using BlazorReporting.Data;

namespace BlazorReporting.Services;

// ══════════════════════════════════════════════════════════════
//  SalesStrategyService
//  Train ML.NET SSA cho từng cặp (Tỉnh × Sản phẩm),
//  đưa ra khuyến nghị Nhập / Xuất / Duy trì hàng tồn kho.
// ══════════════════════════════════════════════════════════════

public sealed class SalesStrategyService
{
    private readonly MLContext _ml = new(seed: 42);

    // ── Phân tích toàn bộ ────────────────────────────────────
    /// <summary>
    /// Nhận dữ liệu lịch sử từ DB, train SSA cho từng cặp Tỉnh×SP,
    /// trả về danh sách chiến lược có gắn nhãn hành động.
    /// </summary>
    public async Task<StrategyResult> AnalyzeAsync(
        IReadOnlyList<ProvinceSalesHistory> history,
        IProgress<(int Done, int Total)>? progress = null,
        CancellationToken ct = default)
    {
        if (history.Count == 0)
            return StrategyResult.Empty;

        // Nhóm theo (Tỉnh, SP)
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
            .ThenByDescending(r => Math.Abs(r.GrowthRate))
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

        // Forecast 3 tháng tới
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

        // Trung bình 3 tháng thực tế gần nhất
        int lookback   = Math.Min(3, n);
        float avgQty3M = qtyVals.TakeLast(lookback).Average();
        float avgAmt3M = amtVals.TakeLast(lookback).Average();

        // Trung bình 3 tháng dự đoán
        float predQtyAvg = predQty.Average();
        float predAmtAvg = predAmt.Average();

        // Tốc độ tăng trưởng
        float growthRate = avgQty3M == 0 ? 0f
            : (predQtyAvg - avgQty3M) / avgQty3M;

        // Độ tin cậy: càng nhiều dữ liệu + SSA → càng cao
        float confidence = Math.Min(1f, n / 12f) * (usedSSA ? 1f : 0.65f);

        // Xác định hành động
        var (action, label, icon, color, badgeClass, reason) = growthRate switch
        {
            > 0.20f  => ("import",   "Nhập nhiều",  "bi-arrow-up-circle-fill",          "#15803d", "strategy-import-strong",
                         $"Nhu cầu tăng mạnh {growthRate:+0.0%;-0.0%} — tăng tồn kho ngay để tránh thiếu hàng"),
            > 0.07f  => ("import",   "Nhập thêm",   "bi-arrow-up-right-circle-fill",    "#16a34a", "strategy-import",
                         $"Xu hướng tăng {growthRate:+0.0%;-0.0%} — cân nhắc nhập thêm để đáp ứng nhu cầu"),
            < -0.20f => ("export",   "Xuất gấp",    "bi-arrow-down-circle-fill",        "#b91c1c", "strategy-export-strong",
                         $"Nhu cầu giảm mạnh {growthRate:+0.0%;-0.0%} — cần xuất/chuyển hàng sang khu vực khác ngay"),
            < -0.07f => ("export",   "Xuất bớt",    "bi-arrow-down-right-circle-fill",  "#dc2626", "strategy-export",
                         $"Xu hướng giảm {growthRate:+0.0%;-0.0%} — nên giảm tồn kho từ từ, tránh ứ đọng vốn"),
            _        => ("maintain", "Duy trì",      "bi-dash-circle-fill",              "#6b7280", "strategy-maintain",
                         $"Ổn định ({growthRate:+0.0%;-0.0%}) — duy trì mức tồn kho hiện tại")
        };

        // Tháng peak: tháng có doanh số cao nhất trong lịch sử
        var peakMonth = pts.MaxBy(p => p.TotalQty);
        string seasonNote = peakMonth != null
            ? $"Tháng cao điểm: T{peakMonth.Month} ({peakMonth.TotalQty:N0} SP)"
            : "";

        return new ProvinceProductStrategy(
            ProvinceCode : province,
            InventoryCD  : cd,
            InventoryName: name,
            AvgQty3M     : (long)avgQty3M,
            AvgAmount3M  : (decimal)avgAmt3M,
            PredQty3M    : (long)predQtyAvg,
            PredAmount3M : (decimal)predAmtAvg,
            PredQtyMonths: predQty.Select(v => (long)v).ToArray(),
            GrowthRate   : growthRate,
            Action       : action,
            ActionLabel  : label,
            ActionIcon   : icon,
            ActionColor  : color,
            BadgeClass   : badgeClass,
            Confidence   : confidence,
            DataPoints   : n,
            UsedSSA      : usedSSA,
            Reason       : reason,
            SeasonNote   : seasonNote);
    }

    // ── ML.NET SSA ────────────────────────────────────────────
    private (float[] Predicted, bool UsedSSA) TrySSA(float[] values, int horizon)
    {
        try
        {
            int ws      = Math.Max(2, Math.Min(values.Length / 2, 12));
            var dv      = _ml.Data.LoadFromEnumerable(values.Select(v => new Dp { V = v }));
            var pipe    = _ml.Forecasting.ForecastBySsa(
                              outputColumnName          : "F",
                              inputColumnName           : "V",
                              windowSize                : ws,
                              seriesLength              : values.Length,
                              trainSize                 : values.Length,
                              horizon                   : horizon,
                              confidenceLevel           : 0.90f,
                              confidenceLowerBoundColumn: "Lo",
                              confidenceUpperBoundColumn: "Hi");
            var engine  = pipe.Fit(dv).CreateTimeSeriesEngine<Dp, Fo>(_ml);
            var out_    = engine.Predict();
            if (out_.F.Any(v => !float.IsFinite(v)))
                return (LinearPredict(values, horizon), false);
            return (out_.F.Select(v => MathF.Max(0f, v)).ToArray(), true);
        }
        catch { return (LinearPredict(values, horizon), false); }
    }

    // ── Linear fallback ───────────────────────────────────────
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

    private class Dp { public float V  { get; set; } }
    private class Fo { public float[] F  { get; set; } = []; public float[] Lo { get; set; } = []; public float[] Hi { get; set; } = []; }
}

// ── Result types ──────────────────────────────────────────────

public sealed record ProvinceProductStrategy(
    string   ProvinceCode,
    string   InventoryCD,
    string   InventoryName,
    long     AvgQty3M,          // Trung bình 3 tháng thực tế
    decimal  AvgAmount3M,
    long     PredQty3M,         // Trung bình 3 tháng dự đoán
    decimal  PredAmount3M,
    long[]   PredQtyMonths,     // 3 giá trị dự đoán chi tiết
    float    GrowthRate,        // Tỉ lệ tăng trưởng dự đoán
    string   Action,            // import | export | maintain
    string   ActionLabel,       // Nhập thêm | Xuất bớt | Duy trì …
    string   ActionIcon,
    string   ActionColor,
    string   BadgeClass,
    float    Confidence,        // 0–1
    int      DataPoints,        // Số tháng dữ liệu train
    bool     UsedSSA,
    string   Reason,            // Lý do gợi ý
    string   SeasonNote);       // Ghi chú mùa vụ

public sealed class StrategyResult
{
    public static readonly StrategyResult Empty = new([]);

    public IReadOnlyList<ProvinceProductStrategy> All { get; }

    // Thống kê nhanh
    public int TotalProvinces  => All.Select(r => r.ProvinceCode).Distinct().Count();
    public int TotalProducts   => All.Select(r => r.InventoryCD).Distinct().Count();
    public int ImportCount     => All.Count(r => r.Action == "import");
    public int ExportCount     => All.Count(r => r.Action == "export");
    public int MaintainCount   => All.Count(r => r.Action == "maintain");

    // Top theo hành động
    public IReadOnlyList<ProvinceProductStrategy> TopImport  =>
        All.Where(r => r.Action == "import")
           .OrderByDescending(r => r.GrowthRate).Take(20).ToList();
    public IReadOnlyList<ProvinceProductStrategy> TopExport  =>
        All.Where(r => r.Action == "export")
           .OrderBy(r => r.GrowthRate).Take(20).ToList();

    // Danh sách tỉnh duy nhất + tóm tắt
    public IReadOnlyList<ProvinceSummary> ProvinceSummaries =>
        All.GroupBy(r => r.ProvinceCode)
           .Select(g => new ProvinceSummary(
               g.Key,
               g.Count(r => r.Action == "import"),
               g.Count(r => r.Action == "export"),
               g.Count(r => r.Action == "maintain"),
               g.Sum(r => r.PredAmount3M)))
           .OrderByDescending(p => p.PredAmount3M)
           .ToList();

    public StrategyResult(IReadOnlyList<ProvinceProductStrategy> all) => All = all;
}

public sealed record ProvinceSummary(
    string  ProvinceCode,
    int     ImportCount,
    int     ExportCount,
    int     MaintainCount,
    decimal PredAmount3M);
