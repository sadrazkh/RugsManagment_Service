using RugsManagment.Application.Common;
namespace RugsManagment.Application.Features.Import;
public enum SheetRowKind { Unknown = 0, Spacer = 1, Title = 2, Header = 3, Data = 4, BaleFooter = 5, Summary = 6 }
public static class RowClassifier
{
    public static SheetRowKind Classify(SheetGrid sheet, int row, int serialColumn, bool summary)
    {
        var text = PersianText.Normalize(sheet.RowText(row));
        if (text.Length == 0) return SheetRowKind.Spacer;
        if (summary) return SheetRowKind.Summary;
        if (text.Contains("نگله شماره", StringComparison.Ordinal) || text.Contains("عدل شماره", StringComparison.Ordinal)) return SheetRowKind.BaleFooter;
        if (HeaderDictionary.Map(sheet, row).Count >= 5) return SheetRowKind.Header;
        if (text.Contains("صورت", StringComparison.Ordinal) && text.Contains("اظهار", StringComparison.Ordinal)) return SheetRowKind.Title;
        return sheet.Text(row, serialColumn).Length > 0 ? SheetRowKind.Data : SheetRowKind.Unknown;
    }
}
