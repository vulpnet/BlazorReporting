using System.Text;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using BlazorReporting.Models;

namespace BlazorReporting.Services;

public sealed class ExportService : IExportService
{
    // ──────────────────────────────────────────────────────────────
    // Excel – flat data
    // ──────────────────────────────────────────────────────────────

    public Task<byte[]> ToExcelAsync(
        IReadOnlyList<Dictionary<string, object?>> data, string title)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Report");

        if (data.Count == 0) { ws.Cell(1,1).Value = "No data"; return Task.FromResult(ToBytes(wb)); }

        var cols = data[0].Keys.ToList();
        for (int c = 0; c < cols.Count; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = cols[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB");
            cell.Style.Font.FontColor = XLColor.White;
        }
        for (int r = 0; r < data.Count; r++)
            for (int c = 0; c < cols.Count; c++)
            {
                var raw = data[r].TryGetValue(cols[c], out var v) ? v : null;
                ws.Cell(r + 2, c + 1).Value = raw is null
                    ? XLCellValue.FromObject("") : XLCellValue.FromObject(raw);
            }

        ws.Columns().AdjustToContents(1, 60);
        ws.SheetView.FreezeRows(1);
        return Task.FromResult(ToBytes(wb));
    }

    // ──────────────────────────────────────────────────────────────
    // Excel – pivot  (two-row header for multi-value fields)
    // Layout:  Row\Col | ColKey1 (colspan=nVal) | ColKey2 … | Total
    //                  | Val1 Val2 | Val1 Val2  |           | Val1 Val2
    // ──────────────────────────────────────────────────────────────

    public Task<byte[]> ToExcelPivotAsync(PivotResult pivot, PivotConfig config, string title)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Pivot");

        var vds  = pivot.ValueDefs;
        int nv   = vds.Count;
        bool multi = nv > 1;

        // ── Row 1: group headers ──
        ws.Cell(1, 1).Value = $"{config.RowField} \\ {config.ColumnField}";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1e293b");
        ws.Cell(1, 1).Style.Font.FontColor = XLColor.White;
        if (multi) ws.Range(1, 1, 2, 1).Merge();

        int col = 2;
        foreach (var ck in pivot.ColumnKeys)
        {
            var hdr = ws.Cell(1, col);
            hdr.Value = ck;
            hdr.Style.Font.Bold = true;
            hdr.Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB");
            hdr.Style.Font.FontColor = XLColor.White;
            if (multi && nv > 1)
            {
                ws.Range(1, col, 1, col + nv - 1).Merge();
                hdr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
            col += nv;
        }

        // Total header
        if (config.ShowRowTotals)
        {
            var tc = ws.Cell(1, col);
            tc.Value = "Total";
            tc.Style.Font.Bold = true;
            tc.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E40AF");
            tc.Style.Font.FontColor = XLColor.White;
            if (multi && nv > 1)
            {
                ws.Range(1, col, 1, col + nv - 1).Merge();
                tc.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
        }

        // ── Row 2: value-field sub-headers (multi only) ──
        if (multi)
        {
            col = 2;
            foreach (var _ in pivot.ColumnKeys)
            {
                foreach (var vd in vds)
                {
                    var sh = ws.Cell(2, col++);
                    sh.Value = vd.Label;
                    sh.Style.Font.Bold = true;
                    sh.Style.Font.FontSize = 8;
                    sh.Style.Fill.BackgroundColor = XLColor.FromHtml("#3b82f6");
                    sh.Style.Font.FontColor = XLColor.White;
                }
            }
            if (config.ShowRowTotals)
                foreach (var vd in vds)
                {
                    var sh = ws.Cell(2, col++);
                    sh.Value = vd.Label;
                    sh.Style.Font.Bold = true;
                    sh.Style.Font.FontSize = 8;
                    sh.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E40AF");
                    sh.Style.Font.FontColor = XLColor.White;
                }
        }

        int headerRows = multi ? 2 : 1;

        // ── Data rows ──
        for (int r = 0; r < pivot.RowKeys.Count; r++)
        {
            var rk = pivot.RowKeys[r];
            int excelRow = r + headerRows + 1;
            ws.Cell(excelRow, 1).Value = rk;
            ws.Cell(excelRow, 1).Style.Font.Bold = true;

            col = 2;
            foreach (var ck in pivot.ColumnKeys)
            {
                foreach (var vd in vds)
                {
                    var v = pivot.Data.TryGetValue(rk, out var rd) &&
                            rd.TryGetValue(ck, out var cd) &&
                            cd.TryGetValue(vd.Label, out var val) ? val : null;
                    ws.Cell(excelRow, col++).Value =
                        v is null ? XLCellValue.FromObject("") : XLCellValue.FromObject(v);
                }
            }

            if (config.ShowRowTotals)
                foreach (var vd in vds)
                {
                    var v = pivot.RowTotals.TryGetValue(rk, out var rt) &&
                            rt.TryGetValue(vd.Label, out var val) ? val : null;
                    var c2 = ws.Cell(excelRow, col++);
                    c2.Value = v is null ? XLCellValue.FromObject("") : XLCellValue.FromObject(v);
                    c2.Style.Font.Bold = true;
                }
        }

        // ── Grand total row ──
        if (config.ShowGrandTotal)
        {
            int grandRow = pivot.RowKeys.Count + headerRows + 1;
            ws.Cell(grandRow, 1).Value = "Grand Total";
            ws.Cell(grandRow, 1).Style.Font.Bold = true;
            ws.Row(grandRow).Style.Fill.BackgroundColor = XLColor.FromHtml("#DBEAFE");

            col = 2;
            foreach (var ck in pivot.ColumnKeys)
                foreach (var vd in vds)
                {
                    var v = pivot.ColTotals.TryGetValue(ck, out var ct) &&
                            ct.TryGetValue(vd.Label, out var val) ? val : null;
                    var c2 = ws.Cell(grandRow, col++);
                    c2.Value = v is null ? XLCellValue.FromObject("") : XLCellValue.FromObject(v);
                    c2.Style.Font.Bold = true;
                }

            if (config.ShowRowTotals)
                foreach (var vd in vds)
                {
                    var v = pivot.GrandTotals.TryGetValue(vd.Label, out var val) ? val : null;
                    var c2 = ws.Cell(grandRow, col++);
                    c2.Value = v is null ? XLCellValue.FromObject("") : XLCellValue.FromObject(v);
                    c2.Style.Font.Bold = true;
                }
        }

        ws.Columns().AdjustToContents(1, 40);
        ws.SheetView.FreezeRows(headerRows);
        ws.SheetView.FreezeColumns(1);
        return Task.FromResult(ToBytes(wb));
    }

    // ──────────────────────────────────────────────────────────────
    // CSV – flat
    // ──────────────────────────────────────────────────────────────

    public byte[] ToCsv(IReadOnlyList<Dictionary<string, object?>> data)
    {
        if (data.Count == 0) return [];
        var cols = data[0].Keys.ToList();
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", cols.Select(Escape)));
        foreach (var row in data)
            sb.AppendLine(string.Join(",",
                cols.Select(c => Escape(row.TryGetValue(c, out var v) ? v?.ToString() : ""))));
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    // ──────────────────────────────────────────────────────────────
    // CSV – pivot  (flattened: ColKey · ValueLabel)
    // ──────────────────────────────────────────────────────────────

    public byte[] ToCsvPivot(PivotResult pivot, PivotConfig config)
    {
        var vds = pivot.ValueDefs;
        var sb  = new StringBuilder();

        // Header
        var headers = new List<string> { Escape($"{config.RowField}/{config.ColumnField}") };
        foreach (var ck in pivot.ColumnKeys)
            foreach (var vd in vds)
                headers.Add(Escape(vds.Count > 1 ? $"{ck}·{vd.Label}" : ck));
        if (config.ShowRowTotals)
            foreach (var vd in vds)
                headers.Add(Escape(vds.Count > 1 ? $"Total·{vd.Label}" : "Total"));
        sb.AppendLine(string.Join(",", headers));

        // Data rows
        foreach (var rk in pivot.RowKeys)
        {
            var cells = new List<string> { Escape(rk) };
            foreach (var ck in pivot.ColumnKeys)
                foreach (var vd in vds)
                {
                    var v = pivot.Data.TryGetValue(rk, out var rd) &&
                            rd.TryGetValue(ck, out var cd) &&
                            cd.TryGetValue(vd.Label, out var val) ? val : null;
                    cells.Add(v?.ToString() ?? "");
                }
            if (config.ShowRowTotals)
                foreach (var vd in vds)
                {
                    var v = pivot.RowTotals.TryGetValue(rk, out var rt) &&
                            rt.TryGetValue(vd.Label, out var val) ? val : null;
                    cells.Add(v?.ToString() ?? "");
                }
            sb.AppendLine(string.Join(",", cells));
        }

        // Grand total row
        if (config.ShowGrandTotal)
        {
            var grand = new List<string> { "Grand Total" };
            foreach (var ck in pivot.ColumnKeys)
                foreach (var vd in vds)
                {
                    var v = pivot.ColTotals.TryGetValue(ck, out var ct) &&
                            ct.TryGetValue(vd.Label, out var val) ? val : null;
                    grand.Add(v?.ToString() ?? "");
                }
            if (config.ShowRowTotals)
                foreach (var vd in vds)
                {
                    var v = pivot.GrandTotals.TryGetValue(vd.Label, out var val) ? val : null;
                    grand.Add(v?.ToString() ?? "");
                }
            sb.AppendLine(string.Join(",", grand));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    // ──────────────────────────────────────────────────────────────
    // PDF – flat (cap at 1 000 rows)
    // ──────────────────────────────────────────────────────────────

    public byte[] ToPdf(IReadOnlyList<Dictionary<string, object?>> data, string title)
    {
        if (data.Count == 0) return [];
        var cols = data[0].Keys.ToList();
        var rows = data.Take(1000).ToList();

        return Document.Create(c => c.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape()); page.Margin(1, Unit.Centimetre);
            page.Content().Column(col =>
            {
                col.Item().Text(title).FontSize(14).Bold();
                col.Item().Text($"Rows shown: {rows.Count} of {data.Count}").FontSize(9).Italic();
                col.Item().PaddingTop(8).Table(t =>
                {
                    t.ColumnsDefinition(cd => { foreach (var _ in cols) cd.RelativeColumn(); });
                    t.Header(h => { foreach (var c2 in cols)
                        h.Cell().Background("#2563EB").Padding(4)
                            .Text(c2).FontColor("#FFFFFF").Bold().FontSize(8); });
                    bool alt = false;
                    foreach (var row in rows)
                    {
                        var bg = alt ? "#F3F4F6" : "#FFFFFF";
                        foreach (var c2 in cols)
                            t.Cell().Background(bg).Padding(3)
                                .Text(row.TryGetValue(c2, out var v) ? v?.ToString() ?? "" : "")
                                .FontSize(7);
                        alt = !alt;
                    }
                });
            });
        })).GeneratePdf();
    }

    // ──────────────────────────────────────────────────────────────
    // PDF – pivot
    // ──────────────────────────────────────────────────────────────

    public byte[] ToPdfPivot(PivotResult pivot, PivotConfig config, string title)
    {
        var vds  = pivot.ValueDefs;
        int nv   = vds.Count;
        bool multi = nv > 1;
        int dataCols = pivot.ColumnKeys.Count * nv + (config.ShowRowTotals ? nv : 0);

        return Document.Create(c => c.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape()); page.Margin(1, Unit.Centimetre);
            page.Content().Column(col =>
            {
                col.Item().Text(title).FontSize(14).Bold();
                col.Item().Text(
                    $"Rows: {config.RowField}  |  Columns: {config.ColumnField}  |  " +
                    string.Join(", ", vds.Select(v => v.Label))).FontSize(9).Italic();

                col.Item().PaddingTop(8).Table(t =>
                {
                    t.ColumnsDefinition(cd =>
                    {
                        cd.RelativeColumn(1.8f);
                        for (int i = 0; i < dataCols; i++) cd.RelativeColumn();
                    });

                    t.Header(h =>
                    {
                        h.Cell().Background("#1e293b").Padding(3)
                            .Text($"{config.RowField}/{config.ColumnField}")
                            .FontColor("#FFFFFF").Bold().FontSize(7);

                        foreach (var ck in pivot.ColumnKeys)
                            foreach (var vd in vds)
                                h.Cell().Background("#2563EB").Padding(3)
                                    .Text(multi ? $"{ck}\n{vd.Label}" : ck)
                                    .FontColor("#FFFFFF").Bold().FontSize(6);

                        if (config.ShowRowTotals)
                            foreach (var vd in vds)
                                h.Cell().Background("#1E40AF").Padding(3)
                                    .Text(multi ? $"Total\n{vd.Label}" : "Total")
                                    .FontColor("#FFFFFF").Bold().FontSize(6);
                    });

                    bool alt = false;
                    foreach (var rk in pivot.RowKeys)
                    {
                        var bg = alt ? "#F3F4F6" : "#FFFFFF";
                        t.Cell().Background(bg).Padding(2).Text(rk).FontSize(7).Bold();

                        foreach (var ck in pivot.ColumnKeys)
                            foreach (var vd in vds)
                            {
                                var v = pivot.Data.TryGetValue(rk, out var rd) &&
                                        rd.TryGetValue(ck, out var cd) &&
                                        cd.TryGetValue(vd.Label, out var val) ? val : null;
                                t.Cell().Background(bg).Padding(2)
                                    .Text(v is null ? "-" : Fmt(v)).FontSize(7);
                            }

                        if (config.ShowRowTotals)
                            foreach (var vd in vds)
                            {
                                var v = pivot.RowTotals.TryGetValue(rk, out var rt) &&
                                        rt.TryGetValue(vd.Label, out var val) ? val : null;
                                t.Cell().Background(bg).Padding(2)
                                    .Text(v is null ? "-" : Fmt(v)).FontSize(7).Bold();
                            }
                        alt = !alt;
                    }

                    if (config.ShowGrandTotal)
                    {
                        t.Cell().Background("#DBEAFE").Padding(2)
                            .Text("Grand Total").FontSize(7).Bold();
                        foreach (var ck in pivot.ColumnKeys)
                            foreach (var vd in vds)
                            {
                                var v = pivot.ColTotals.TryGetValue(ck, out var ct) &&
                                        ct.TryGetValue(vd.Label, out var val) ? val : null;
                                t.Cell().Background("#DBEAFE").Padding(2)
                                    .Text(v is null ? "-" : Fmt(v)).FontSize(7).Bold();
                            }
                        if (config.ShowRowTotals)
                            foreach (var vd in vds)
                            {
                                var v = pivot.GrandTotals.TryGetValue(vd.Label, out var val)
                                    ? val : null;
                                t.Cell().Background("#BFDBFE").Padding(2)
                                    .Text(v is null ? "-" : Fmt(v)).FontSize(7).Bold();
                            }
                    }
                });
            });
        })).GeneratePdf();
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    private static byte[] ToBytes(XLWorkbook wb)
    {
        using var ms = new MemoryStream(); wb.SaveAs(ms); return ms.ToArray();
    }

    private static string Escape(string? s)
    {
        if (s is null) return "";
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return $"\"{s.Replace("\"", "\"\"")}\"";
        return s;
    }

    private static string Fmt(object? v) =>
        v is null ? "-"
        : double.TryParse(v.ToString(), out var d) ? d.ToString("N2") : v.ToString()!;
}
