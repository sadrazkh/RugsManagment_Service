using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Import;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;
namespace RugsManagment.Application.Features.Import;
public static class Reconciler
{
    public static IReadOnlyList<ReconciliationRowDto> Reconcile(SheetGrid sheet, IReadOnlyList<ImportRowDto> rows,
        ImportMode mode, List<ImportIssueDto> issues, IReadOnlyDictionary<string, int>? mapping = null)
    {
        if (mode == ImportMode.StockOnly) return [];
        var footers = new Dictionary<int, (int? Pieces, decimal? Area, decimal? Usd)>();
        var summaries = new Dictionary<int, (int? Pieces, decimal? Area, decimal? Usd)>();
        int? lastBale = null;
        for (var r = 1; r <= sheet.LastRow; r++)
        {
            var imported = rows.FirstOrDefault(x => x.SheetRow == r && x.Values.ContainsKey("serial"));
            if (imported?.Bale is { } b) lastBale = b;
            if ((sheet.Text(r, 1).Contains("نگله شماره", StringComparison.Ordinal) || sheet.Text(r, 1).Contains("عدل شماره", StringComparison.Ordinal)) && lastBale is { } number)
                footers[number] = ((int?)Read(sheet, r, mapping?.GetValueOrDefault("pattern", 5) ?? 5, issues),
                    Read(sheet, r, mapping?.GetValueOrDefault("width", 9) ?? 9, issues), Read(sheet, r, mapping?.GetValueOrDefault("usdPerM2", 11) ?? 11, issues));
            if (r > rows.Where(x => x.Values.ContainsKey("serial")).Select(x => x.SheetRow).DefaultIfEmpty(0).Max()
                && ImportPlanner.Number(sheet.Text(r, 4)) is { } bn && bn == decimal.Truncate(bn) && bn is >= 1 and <= 9999)
                summaries[(int)bn] = ((int?)Read(sheet, r, 5, issues), Read(sheet, r, 6, issues), Read(sheet, r, 7, issues));
        }
        return rows.Where(r => r.Action is "create" or "attach").GroupBy(r => r.Bale).Select(g =>
        {
            var qty = g.Sum(ImportPlanner.Qty); var area = g.Sum(ImportPlanner.Area); var usd = g.Sum(ImportPlanner.Usd);
            var f = footers.GetValueOrDefault(g.Key ?? 0); var s = summaries.GetValueOrDefault(g.Key ?? 0);
            var footerOk = (f.Pieces is null || f.Pieces == qty) && (f.Area is null || f.Area == area) && (f.Usd is null || f.Usd == usd);
            var summaryOk = (s.Pieces is null || s.Pieces == qty) && (s.Area is null || s.Area == area) && (s.Usd is null || s.Usd == usd);
            if (!footerOk) issues.Add(ImportMessages.Issue("W_FOOTER_MISMATCH"));
            if (!summaryOk) issues.Add(ImportMessages.Issue("W_SUMMARY_MISMATCH"));
            return new ReconciliationRowDto(g.Key, qty, f.Pieces, s.Pieces, area, f.Area, s.Area, usd, f.Usd, s.Usd, footerOk && summaryOk);
        }).OrderBy(x => x.Bale).ToArray();
    }
    private static decimal? Read(SheetGrid sheet, int row, int column, List<ImportIssueDto> issues)
    {
        var value = ImportPlanner.Number(sheet.Text(row, column));
        if (value is null) return null;
        var rounded = Num.Round(value.Value, 2);
        if (value != rounded && decimal.Abs(value.Value - rounded) <= 0.000000001m)
        { issues.Add(ImportMessages.Issue("I_VALUE_SNAPPED")); return rounded; }
        if (sheet.Cell(row, column)?.Snapped == true) issues.Add(ImportMessages.Issue("I_VALUE_SNAPPED"));
        return value;
    }
}
