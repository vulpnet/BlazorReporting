using Microsoft.ML;
using Microsoft.ML.Transforms.TimeSeries;
using BlazorReporting.Data;

namespace BlazorReporting.Services;

// ══════════════════════════════════════════════════════════════
//  SalesForecastService  —  ML.NET SSA Time Series Forecasting
// ══════════════════════════════════════════════════════════════

public sealed class SalesForecastService
{
    private readonly MLContext _ml = new(seed: 42);

    public ForecastResult Forecast(
        IReadOnlyList<ProductTrendPoint> history,
        int horizon = 3)
    {
        var sorted = history
            .OrderBy(p => p.Year).ThenBy(p => p.Month)
            .ToList();
        if (sorted.Count == 0)
            return ForecastResult.Empty(horizon);

        var qtyVals    = sorted.Select(p => (float)p.TotalQty).ToArray();
        var amountVals = sorted.Select(p => (float)p.TotalAmount).ToArray();

        // Chạy SSA cho cả qty và amount
        var (qtySeries,    qtySSA)    = RunForecast(qtyVals,    horizon);
        var (amountSeries, amountSSA) = RunForecast(amountVals, horizon);

        // Dùng SSA nếu cả hai đều thành công
        bool isSSA = qtySSA && amountSSA;

        var lastY = sorted[^1].Year;
        var lastM = sorted[^1].Month;
        var labels = Enumerable.Range(1, horizon).Select(step =>
        {
            var mo = lastM + step;
            var yr = lastY + (mo - 1) / 12;
            mo = ((mo - 1) % 12) + 1;
            return $"T{mo}/{yr}";
        }).ToList();

        return new ForecastResult(labels, qtySeries, amountSeries, isSSA,
            DataPoints: sorted.Count,
            WindowSize: isSSA ? Math.Max(2, Math.Min(sorted.Count / 2, 12)) : 0);
    }

    // ── SSA attempt ───────────────────────────────────────────
    private (ForecastSeries Series, bool UsedSSA) RunForecast(float[] values, int horizon)
    {
        int n = values.Length;
        if (n < 4)
            return (LinearFallback(values, horizon), false);

        try
        {
            int windowSize = Math.Max(2, Math.Min(n / 2, 12));

            var dataView = _ml.Data.LoadFromEnumerable(
                values.Select(v => new SalesPoint { Value = v }));

            var pipeline = _ml.Forecasting.ForecastBySsa(
                outputColumnName          : nameof(SsaOutput.Forecast),
                inputColumnName           : nameof(SalesPoint.Value),
                windowSize                : windowSize,
                seriesLength              : n,
                trainSize                 : n,
                horizon                   : horizon,
                confidenceLevel           : 0.90f,
                confidenceLowerBoundColumn: nameof(SsaOutput.Lower),
                confidenceUpperBoundColumn: nameof(SsaOutput.Upper));

            var engine = pipeline.Fit(dataView)
                                 .CreateTimeSeriesEngine<SalesPoint, SsaOutput>(_ml);
            var output = engine.Predict();

            // Reject if NaN / Inf
            if (output.Forecast.Any(v => !float.IsFinite(v)))
                return (LinearFallback(values, horizon), false);

            return (new ForecastSeries(
                output.Forecast.Select(v => MathF.Max(0f, v)).ToArray(),
                output.Lower.Select(v => MathF.Max(0f, v)).ToArray(),
                output.Upper.Select(v => MathF.Max(0f, v)).ToArray()), true);
        }
        catch
        {
            return (LinearFallback(values, horizon), false);
        }
    }

    // ── Linear Regression fallback ────────────────────────────
    private static ForecastSeries LinearFallback(float[] values, int horizon)
    {
        int n = values.Length;
        double sx = 0, sy = 0, sxy = 0, sx2 = 0;
        for (int i = 0; i < n; i++)
        { sx += i; sy += values[i]; sxy += i * values[i]; sx2 += (double)i * i; }
        double d = n * sx2 - sx * sx;
        double s = d == 0 ? 0 : (n * sxy - sx * sy) / d;
        double b = (sy - s * sx) / n;
        var pred = Enumerable.Range(n, horizon)
                             .Select(i => (float)Math.Max(0, b + s * i)).ToArray();
        return new ForecastSeries(
            pred,
            pred.Select(v => v * 0.80f).ToArray(),
            pred.Select(v => v * 1.20f).ToArray());
    }

    private sealed class SalesPoint { public float Value { get; set; } }
    private sealed class SsaOutput
    {
        public float[] Forecast { get; set; } = [];
        public float[] Lower    { get; set; } = [];
        public float[] Upper    { get; set; } = [];
    }
}

// ── Result records ────────────────────────────────────────────

public sealed record ForecastResult(
    IReadOnlyList<string> Labels,
    ForecastSeries        Qty,
    ForecastSeries        Amount,
    bool                  IsSSA,        // true = ML.NET SSA | false = Linear fallback
    int                   DataPoints,   // số điểm lịch sử dùng train
    int                   WindowSize)   // window SSA (0 nếu fallback)
{
    public string AlgorithmLabel => IsSSA
        ? $"ML.NET SSA (window={WindowSize}, n={DataPoints})"
        : $"Linear Regression (n={DataPoints} — cần ≥4 điểm để dùng SSA)";

    public string AlgorithmBadgeClass => IsSSA ? "badge-ssa" : "badge-linear";
    public string AlgorithmIcon       => IsSSA ? "bi-robot" : "bi-graph-up";

    public static ForecastResult Empty(int horizon)
    {
        var z = new float[horizon];
        return new ForecastResult(
            Enumerable.Range(1, horizon).Select(i => $"+{i}T").ToList(),
            new ForecastSeries(z, z, z),
            new ForecastSeries(z, z, z),
            IsSSA: false, DataPoints: 0, WindowSize: 0);
    }
}

public sealed record ForecastSeries(float[] Predicted, float[] Lower, float[] Upper);
