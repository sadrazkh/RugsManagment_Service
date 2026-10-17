namespace RugsManagment.Application.Features.Import;
public static class BaleResolver
{
    public static (int? Number, bool Adopted) Resolve(SheetGrid sheet, int row, int column)
    {
        if (ImportPlanner.Number(sheet.Text(row, column)) is { } direct && direct == decimal.Truncate(direct) && direct is >= 1 and <= 9999)
            return ((int)direct, false);
        var merge = sheet.Merges.FirstOrDefault(m => m.FirstColumn == column && m.LastColumn == column && m.FirstRow <= row && m.LastRow >= row);
        if (merge is not null && ImportPlanner.Number(sheet.Text(merge.FirstRow, column)) is { } inherited
            && inherited == decimal.Truncate(inherited) && inherited is >= 1 and <= 9999)
            return ((int)inherited, false);
        // ردیف جاافتاده پس از جمع، به اولین نگلهٔ بعد از خودش تعلق دارد؛ از شمارهٔ جمع قبلی استفاده نمی‌شود.
        for (var next = row + 1; next <= Math.Min(sheet.LastRow, row + 30); next++)
        {
            var text = sheet.Text(next, column);
            if (ImportPlanner.Number(text) is { } found && found == decimal.Truncate(found) && found is >= 1 and <= 9999)
                return ((int)found, true);
            if (text.Contains("شماره", StringComparison.Ordinal)) break;
        }
        return (null, false);
    }
}
