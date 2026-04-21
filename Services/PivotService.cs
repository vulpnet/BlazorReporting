using BlazorReporting.Models;

namespace BlazorReporting.Services;

public sealed class PivotService : IPivotService
{
    public PivotResult Build(IReadOnlyList<Dictionary<string, object?>> data, PivotConfig config)
    {
        var valueDefs = config.ValidValues;
        if (!config.IsValid || data.Count == 0 || valueDefs.Count == 0)
            return PivotResult.Empty;

        // buckets[rowKey][colKey][label] = list of raw numbers
        var buckets = new Dictionary<string, Dictionary<string, Dictionary<string, List<double>>>>(
            StringComparer.OrdinalIgnoreCase);
        var colSet = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in data)
        {
            var rk = GetKey(row, config.RowField);
            var ck = GetKey(row, config.ColumnField);
            colSet.Add(ck);

            if (!buckets.TryGetValue(rk, out var colMap))
                buckets[rk] = colMap = new(StringComparer.OrdinalIgnoreCase);
            if (!colMap.TryGetValue(ck, out var valMap))
                colMap[ck] = valMap = new(StringComparer.OrdinalIgnoreCase);

            foreach (var vd in valueDefs)
            {
                var num = ToDouble(row.TryGetValue(vd.Field, out var v) ? v : null);
                if (!valMap.TryGetValue(vd.Label, out var list))
                    valMap[vd.Label] = list = [];
                list.Add(num);
            }
        }

        var rowKeys  = buckets.Keys.OrderBy(k => k).ToList();
        var colKeys  = colSet.ToList();

        var resultData = new Dictionary<string, Dictionary<string, Dictionary<string, object?>>>(
            StringComparer.OrdinalIgnoreCase);
        var rowTotals = new Dictionary<string, Dictionary<string, object?>>(
            StringComparer.OrdinalIgnoreCase);
        var colTotals = new Dictionary<string, Dictionary<string, object?>>(
            StringComparer.OrdinalIgnoreCase);
        var grandTotals = valueDefs.ToDictionary(
            vd => vd.Label, _ => (object?)0.0, StringComparer.OrdinalIgnoreCase);

        foreach (var rk in rowKeys)
        {
            resultData[rk] = new(StringComparer.OrdinalIgnoreCase);
            rowTotals[rk]  = valueDefs.ToDictionary(
                vd => vd.Label, _ => (object?)0.0, StringComparer.OrdinalIgnoreCase);

            foreach (var ck in colKeys)
            {
                resultData[rk][ck] = new(StringComparer.OrdinalIgnoreCase);

                if (!colTotals.ContainsKey(ck))
                    colTotals[ck] = valueDefs.ToDictionary(
                        vd => vd.Label, _ => (object?)0.0, StringComparer.OrdinalIgnoreCase);

                var hasData = buckets[rk].TryGetValue(ck, out var valMap);

                foreach (var vd in valueDefs)
                {
                    if (hasData && valMap!.TryGetValue(vd.Label, out var nums))
                    {
                        var agg = Aggregate(nums, vd.Aggregation);
                        resultData[rk][ck][vd.Label] = agg;
                        rowTotals[rk][vd.Label]  = (double)rowTotals[rk][vd.Label]!  + agg;
                        colTotals[ck][vd.Label]  = (double)colTotals[ck][vd.Label]!  + agg;
                        grandTotals[vd.Label]    = (double)grandTotals[vd.Label]!     + agg;
                    }
                    else
                    {
                        resultData[rk][ck][vd.Label] = null;
                    }
                }
            }
        }

        return new PivotResult
        {
            RowKeys    = rowKeys,
            ColumnKeys = colKeys,
            ValueDefs  = valueDefs,
            Data       = resultData,
            RowTotals  = rowTotals,
            ColTotals  = colTotals,
            GrandTotals = grandTotals
        };
    }

    private static string GetKey(Dictionary<string, object?> row, string field) =>
        row.TryGetValue(field, out var v) && v != null ? v.ToString()! : "(blank)";

    private static double Aggregate(List<double> vals, AggregationType agg) => agg switch
    {
        AggregationType.Sum   => vals.Sum(),
        AggregationType.Count => vals.Count,
        AggregationType.Avg   => vals.Average(),
        AggregationType.Min   => vals.Min(),
        AggregationType.Max   => vals.Max(),
        _                     => vals.Sum()
    };

    private static double ToDouble(object? v)
    {
        if (v is null)    return 0;
        if (v is double d)  return d;
        if (v is decimal dc) return (double)dc;
        if (v is int i)     return i;
        if (v is long l)    return l;
        if (v is float f)   return f;
        return double.TryParse(v.ToString(), out var p) ? p : 0;
    }
}
