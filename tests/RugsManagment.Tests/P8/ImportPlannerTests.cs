using RugsManagment.Application.DTOs.Import;
using RugsManagment.Application.Features.Import;
using RugsManagment.Domain.Enums;
namespace RugsManagment.Tests.P8;
public sealed class ImportPlannerTests
{
    private static SheetGrid Sheet(params string[][] data)
        => new("test", data.Length + 1, 4, new[] { new[] { "شماره", "طول", "عرض", "تعداد" } }.Concat(data)
            .SelectMany((row, r) => row.Select((v, c) => new SheetCell(r + 1, c + 1, v))).ToArray(), []);
    private static ImportPreviewDto Plan(SheetGrid sheet) => ImportPlanner.Plan(sheet, 1,
        new Dictionary<string, int> { ["serial"] = 1, ["length"] = 2, ["width"] = 3, ["qty"] = 4 }, new(ImportMode.StockOnly));
    [Theory]
    [InlineData("3960 - 3911", "E_RANGE_REVERSED")]
    [InlineData("1 - 20000", "E_RANGE_TOO_LONG")]
    [InlineData("1-", "E_SERIAL_INVALID")]
    public void BadSerialsHaveUsefulCodes(string serial, string code)
        => Assert.Contains(Assert.Single(Plan(Sheet([serial, "2", "1", "1"])).Rows).Issues, i => i.Code == code);
    [Fact]
    public void OverlappingRangesMarkBothRowsAsErrors()
    {
        var preview = Plan(Sheet(["1 - 4", "2", "1", "4"], ["3 - 6", "2", "1", "4"]));
        Assert.Equal(2, preview.Counts.Errors); Assert.Equal(0, preview.Counts.Lines);
    }
    [Fact]
    public void PersianDigitsAndNoiseAreNormalizedAndLotsRoundPerPiece()
    {
        var preview = Plan(Sheet(["۳۹۱۱ - ۳۹۶۰", "۰٫۴۰", "۰٫۴۰", "۵۰"]));
        Assert.Equal(8m, preview.Counts.AreaM2); Assert.Equal(50, preview.Counts.Pieces);
    }
    [Fact]
    public void InvalidDimensionsDoNotEnterCounts()
        => Assert.Equal(1, Plan(Sheet(["100", "0.049", "2", "1"])).Counts.Errors);
    [Theory]
    [InlineData("100", "2")]
    [InlineData("100", "unknown")]
    [InlineData("1 - 4", "5")]
    public void InvalidQuantityIsExcludedDuringPreview(string serial, string quantity)
        => Assert.Contains(Assert.Single(Plan(Sheet([serial, "2", "1", quantity])).Rows).Issues, i => i.Code == "E_QTY_INVALID");
    [Theory]
    [InlineData("1.5")]
    [InlineData("1000000000000000000")]
    public void InvalidMergedBaleIsNotTruncatedOrOverflowed(string number)
    {
        var sheet = new SheetGrid("test", 3, 1, [new(2, 1, number)], [new(2, 3, 1, 1)]);
        Assert.Null(BaleResolver.Resolve(sheet, 3, 1).Number);
    }
}
