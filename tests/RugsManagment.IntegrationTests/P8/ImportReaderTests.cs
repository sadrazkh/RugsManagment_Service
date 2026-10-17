using RugsManagment.Application.DTOs.Import;
using RugsManagment.Application.Features.Import;
using RugsManagment.Infrastructure.Features.Import;
using RugsManagment.IntegrationTests.P8.Fixtures;
namespace RugsManagment.IntegrationTests.P8;
public sealed class ImportReaderTests
{
    [Fact]
    public async Task SyntheticMerchantSheet_ReconcilesAndPreservesLots()
    {
        using var input = SampleSheetBuilder.Build();
        var sheet = Assert.Single(await new ClosedXmlWorkbookReader().ReadAsync(input));
        var header = HeaderDictionary.Detect(sheet);
        Assert.Equal(2, header);
        var mapping = HeaderDictionary.Map(sheet, header!.Value);
        Assert.Equal(12, mapping.Count);
        var preview = ImportPlanner.Plan(sheet, header.Value, mapping, new ImportOptions());
        Assert.Equal(161, preview.Counts.Lines); Assert.Equal(242, preview.Counts.Pieces); Assert.Equal(36, preview.Counts.Bales);
        Assert.Equal(464.74m, preview.Counts.AreaM2); Assert.Equal(20322.50m, preview.Counts.UsdTotal);
        Assert.Equal(0, preview.Counts.Errors);
        Assert.All(preview.Reconciliation, r => Assert.True(r.Ok, $"bale {r.Bale}"));
        foreach (var row in new[] { 187, 189, 190 })
        {
            var lot = preview.Rows.Single(r => r.SheetRow == row);
            Assert.Null(lot.Values["bg_color"]); Assert.Null(lot.Values["pattern"]);
            Assert.Equal("پشم", lot.Values["material"]); Assert.Equal("گبه", lot.Values["origin"]);
            Assert.Contains(lot.Issues, i => i.Code == "W_QTY_FROM_MATERIAL");
            Assert.Contains(lot.Issues, i => i.Code == "W_MERGED_SPLIT");
        }
        Assert.Contains(preview.Rows.Single(r => r.SheetRow == 190).Issues, i => i.Code == "W_RANGE_QTY_DIFF");
        var adopted = preview.Rows.Single(r => r.SheetRow == 192);
        Assert.Equal(31, adopted.Bale); Assert.Contains(adopted.Issues, i => i.Code == "W_ROW_ADOPTED");
        Assert.Equal(33, preview.Rows.Single(r => r.SheetRow == 210).Bale);
        Assert.Contains(preview.IssuesByCode, i => i.Code == "I_VALUE_SNAPPED");
        Assert.Equal(new DateOnly(2026, 9, 6), ImportPlanner.Title(sheet, 2).Date);
    }
}
