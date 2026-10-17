using RugsManagment.Application.DTOs.Import;
using RugsManagment.Application.Features.Import;
using RugsManagment.Domain.Enums;
namespace RugsManagment.Tests.P8;
public sealed class ImportPlannerTests
{
    private static SheetGrid Sheet(params string[][] data)
        => new("test", data.Length + 1, 4, new[] { new[] { "شماره", "طول", "عرض", "تعداد" } }.Concat(data)
            .SelectMany((row, r) => row.Select((v, c) => new SheetCell(r + 1, c + 1, v))).ToArray(), []);
    private static ImportPreviewDto Plan(SheetGrid sheet, IReadOnlyList<int>? skippedRows = null,
        IReadOnlyDictionary<int, Dictionary<string, string?>>? overrides = null) => ImportPlanner.Plan(sheet, 1,
        new Dictionary<string, int> { ["serial"] = 1, ["length"] = 2, ["width"] = 3, ["qty"] = 4 }, new(ImportMode.StockOnly), overrides,
        skippedRows: skippedRows);
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

    [Fact]
    public void SkippingADuplicateClearsTheSurvivingRow_AndUnskippingRestoresBothBlockers()
    {
        var sheet = Sheet(["100", "2", "1", "1"], ["100", "3", "1", "1"]);
        var original = Plan(sheet);
        Assert.Equal(2, original.Counts.Errors);
        var preview = Plan(sheet, [3]);
        Assert.Equal(0, preview.Counts.Errors); Assert.Equal(1, preview.Counts.Lines); Assert.Equal(1, preview.Counts.Skip);
        Assert.Equal(2m, preview.Counts.AreaM2); Assert.Equal(1, preview.Counts.Pieces);
        Assert.Equal("create", preview.Rows[0].Action);
        Assert.DoesNotContain(preview.Rows[0].Issues, i => i.Code == "E_SERIAL_DUP_IN_FILE");
        var skipped = preview.Rows[1]; Assert.Equal("skip", skipped.Action);
        Assert.Equal("I_ROW_SKIPPED", Assert.Single(skipped.Issues).Code);
        Assert.Contains(preview.IssuesByCode, i => i.Code == "I_ROW_SKIPPED" && i.Count == 1 && i.Severity == "info");
        var restored = Plan(sheet, []);
        Assert.Equal(2, restored.Counts.Errors); Assert.Equal(0, restored.Counts.Skip);
        Assert.All(restored.Rows, r => Assert.Equal("error", r.Action));
    }

    [Fact]
    public void SkippingAnInvalidRowPreservesRawAndEditedIdentity_WithoutErrorsOrContributingTotals()
    {
        var sheet = Sheet(["1-", "bad length", "bad width", "bad quantity"], ["200", "2", "1", "1"]);
        var overrides = new Dictionary<int, Dictionary<string, string?>> { [2] = new() { ["serial"] = "still-invalid-", ["length"] = "edited length" } };
        var preview = Plan(sheet, [2], overrides);
        Assert.Equal(0, preview.Counts.Errors); Assert.Equal(1, preview.Counts.Lines); Assert.Equal(1, preview.Counts.Skip);
        var row = preview.Rows[0]; Assert.Equal(2, row.SheetRow); Assert.Equal("skip", row.Action);
        Assert.Equal("still-invalid-", row.Values["serial"]); Assert.Equal("edited length", row.Values["length"]);
        Assert.Equal("bad width", row.Values["width"]); Assert.Equal("bad quantity", row.Values["qty"]);
        Assert.Equal("I_ROW_SKIPPED", Assert.Single(row.Issues).Code);
        Assert.Equal(1, Plan(sheet, overrides: overrides).Counts.Errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1-")]
    public void InvalidOrMissingSerialRemainsVisibleEvenWithInvalidDimensionText(string serial)
    {
        var preview = Plan(Sheet([serial, "length?", "width?", "1"]));
        var row = Assert.Single(preview.Rows);
        Assert.Equal(2, row.SheetRow); Assert.Equal(1, preview.Counts.Errors);
        Assert.Contains(row.Issues, i => i.Code == "E_SERIAL_INVALID");
        Assert.Contains(row.Issues, i => i.Code == "E_DIM_MISSING");
        var skipped = Plan(Sheet([serial, "length?", "width?", "1"]), [2]);
        Assert.Equal(1, skipped.Counts.Skip); Assert.Equal(0, skipped.Counts.Errors);
    }

    [Fact]
    public void ThreeWayDuplicatesExposeEveryOtherExcelRowExactlyOnce()
    {
        var sheet = Sheet(["100", "2", "1", "1"], ["100", "2", "1", "1"], ["100", "2", "1", "1"]);
        var preview = Plan(sheet);
        Assert.Equal(3, preview.Counts.Errors);
        foreach (var row in preview.Rows)
        {
            var issue = Assert.Single(row.Issues, i => i.Code == "E_SERIAL_DUP_IN_FILE");
            Assert.Equal(new[] { 2, 3, 4 }.Where(r => r != row.SheetRow), issue.RelatedRows);
        }
        var partial = Plan(sheet, [3]);
        Assert.Equal(2, partial.Counts.Errors);
        Assert.Equal(new[] { 4 }, Assert.Single(partial.Rows[0].Issues, i => i.Code == "E_SERIAL_DUP_IN_FILE").RelatedRows);
        Assert.Equal(new[] { 2 }, Assert.Single(partial.Rows[2].Issues, i => i.Code == "E_SERIAL_DUP_IN_FILE").RelatedRows);
        Assert.Equal(0, Plan(sheet, [3, 4]).Counts.Errors);
    }

    [Fact]
    public void OverlappingRangesExposeOnlyDirectlyConflictingRows_AndReevaluateAfterSkip()
    {
        var sheet = Sheet(["1 - 2", "2", "1", "2"], ["2 - 3", "2", "1", "2"], ["3 - 4", "2", "1", "2"]);
        var preview = Plan(sheet);
        Assert.Equal(new[] { 3 }, Assert.Single(preview.Rows[0].Issues, i => i.Code == "E_SERIAL_OVERLAP_IN_FILE").RelatedRows);
        Assert.Equal(new[] { 2, 4 }, Assert.Single(preview.Rows[1].Issues, i => i.Code == "E_SERIAL_OVERLAP_IN_FILE").RelatedRows);
        Assert.Equal(new[] { 3 }, Assert.Single(preview.Rows[2].Issues, i => i.Code == "E_SERIAL_OVERLAP_IN_FILE").RelatedRows);
        var skipped = Plan(sheet, [3]);
        Assert.Equal(0, skipped.Counts.Errors); Assert.Equal(2, skipped.Counts.Lines); Assert.Equal(4, skipped.Counts.Pieces);
        Assert.DoesNotContain(skipped.Rows.SelectMany(r => r.Issues), i => i.Code == "E_SERIAL_OVERLAP_IN_FILE");
    }

    [Fact]
    public void MissingSerialAndDimensions_WithOnlyQuantity_RemainsAnExplicitDecision()
    {
        var sheet = Sheet(["", "", "", "2"]);
        var row = Assert.Single(Plan(sheet).Rows);
        Assert.Equal("error", row.Action);
        Assert.Contains(row.Issues, i => i.Code == "E_SERIAL_INVALID");
        Assert.Equal(1, Plan(sheet, [2]).Counts.Skip);
        Assert.Equal(0, Plan(sheet, [2]).Counts.Errors);
    }

    [Fact]
    public void ExplicitSkipDoesNotOfferRateSuggestionsOrSkipStructuralRows()
    {
        var sheet = new SheetGrid("test", 3, 6,
            [new(1, 1, "شماره"), new(1, 2, "طول"), new(1, 3, "عرض"), new(1, 4, "تعداد"),
             new(2, 1, "100"), new(2, 2, "2"), new(2, 3, "1"), new(2, 4, "1"), new(2, 5, "کاشان"), new(2, 6, "45"),
             new(3, 1, "200"), new(3, 2, "2"), new(3, 3, "1"), new(3, 4, "1")], []);
        var preview = ImportPlanner.Plan(sheet, 1, new Dictionary<string, int> { ["serial"] = 1, ["length"] = 2, ["width"] = 3,
            ["qty"] = 4, ["origin"] = 5, ["usdPerM2"] = 6 }, new(ImportMode.StockOnly), skippedRows: [1, 2, 9999]);
        Assert.Equal(2, preview.Rows.Count); Assert.Equal(1, preview.Counts.Skip); Assert.Empty(preview.SuggestedRates);
        Assert.Equal(0, preview.Counts.UsdTotal);
    }
}
