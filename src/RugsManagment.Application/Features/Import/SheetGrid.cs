namespace RugsManagment.Application.Features.Import;

/// <summary>مقدارهای ذخیره‌شدهٔ فایل، بدون اجرای فرمول؛ خانهٔ پوشیدهٔ ادغام عمداً خالی است.</summary>
public sealed record SheetCell(int Row, int Column, string? Text, decimal? Number = null, bool Snapped = false);
public sealed record SheetMerge(int FirstRow, int LastRow, int FirstColumn, int LastColumn);
public sealed record SheetGrid(string Name, int LastRow, int LastColumn, IReadOnlyList<SheetCell> Cells,
    IReadOnlyList<SheetMerge> Merges)
{
    private Dictionary<(int, int), SheetCell>? _index;
    public SheetCell? Cell(int row, int column)
        => (_index ??= Cells.ToDictionary(c => (c.Row, c.Column))).GetValueOrDefault((row, column));
    public string Text(int row, int column) => Cell(row, column)?.Text?.Trim() ?? "";
    public string RowText(int row) => string.Join(" ", Cells.Where(c => c.Row == row).OrderBy(c => c.Column).Select(c => c.Text));
}
