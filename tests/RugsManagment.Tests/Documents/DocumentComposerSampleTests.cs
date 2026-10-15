using System.Text.Json;
using System.Text.RegularExpressions;
using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Documents.Composer;
using RugsManagment.Application.Features.Documents.Data;
using RugsManagment.Application.Features.Documents.Expressions;
using RugsManagment.Application.Features.Documents.Layout;

namespace RugsManagment.Tests.Documents;

/// <summary>
/// کل محمولهٔ نمونه (spec E.10 #2–#4، K.2/P6): ۳۶ نگله / ۱۶۱ خط / ۲۴۲ تخته / ۴۶۴٫۷۴ متر / ۲۰٬۳۲۲٫۵۰ دلار؛ ردیف دسته «برای
/// یک تخته» با ستون «تعداد»؛ برگهٔ خلاصه با پیوند به پانویس‌ها؛ نگلهٔ خالی کنار گذاشته؛ واژهٔ «عدل».
/// </summary>
public partial class DocumentComposerSampleTests
{
    private static RenderModel Full() => SampleDocuments.Compose(SampleDocuments.Shipment());

    private static RenderRow Footer(RenderSheet sheet, int bale)
        => sheet.Rows.Single(r => r.Kind == RenderRowKinds.Footer && r.Group == bale);

    private static RenderCell At(RenderSheet sheet, string address) => SampleDocuments.Cells(sheet)[address];

    [Fact]
    public void Full_HasTheSamplesTotals()
    {
        var model = Full();
        var main = model.Sheets[0];
        Assert.True(model.HasLots);
        Assert.Equal(13, main.Columns.Count);
        Assert.Equal("تعداد", main.Columns[7].Label);
        Assert.Equal(36, main.Rows.Count(r => r.Kind == RenderRowKinds.Footer));
        Assert.Equal(161, main.Rows.Count(r => r.Kind == RenderRowKinds.Data));

        var summary = model.Sheets[1];
        var totals = summary.Rows.Single(r => r.Kind == RenderRowKinds.Totals);
        var byCol = totals.Cells.ToDictionary(c => c.Col);
        Assert.Equal(242m, byCol[5].Value.Number);
        Assert.Equal(464.74m, byCol[6].Value.Number);
        Assert.Equal(20322.50m, byCol[7].Value.Number);
        Assert.Equal("SUM(E3:E38)", byCol[5].Formula);
        Assert.Equal("SUM(F3:F38)", byCol[6].Formula);
        Assert.Equal("SUM(G3:G38)", byCol[7].Formula);

        // پانویس‌ها جمعشان همان است
        Assert.Equal(242m, main.Rows.Where(r => r.Kind == RenderRowKinds.Footer).Sum(r => r.Cells.Single(c => c.Col == 5).Value.Number));
        Assert.Empty(model.Warnings); // checkAgainst روی نمونه هرگز هشدار نمی‌دهد
    }

    [Fact]
    public void Full_QtyIsOneOnSingleRugs_AndSerialsAreNumericCells()
    {
        var main = Full().Sheets[0];
        var row = SampleDocuments.RowOfSerial(main, "3778");
        var cells = row.Cells.ToDictionary(c => c.Col);
        Assert.Equal(1m, cells[8].Value.Number); // H = 1، هرگز خالی
        Assert.Equal(DocValueKind.Number, cells[2].Value.Kind);
        Assert.Equal(3778m, cells[2].Value.Number);
        Assert.All(main.Rows.Where(r => r.Kind == RenderRowKinds.Data), r =>
            Assert.Equal(DocValueKind.Number, r.Cells.Single(c => c.Col == 8).Value.Kind));
        Assert.All(main.Rows.SelectMany(r => r.Cells), c => Assert.Equal(LayoutDefaults.Format, c.Format));
    }

    [Fact]
    public void Bale29_IsOneLotRowPerPiece_WithTheFootersWeightedByQty()
    {
        var main = Full().Sheets[0];
        var data = main.Rows.Single(r => r.Kind == RenderRowKinds.Data && r.Group == 29);
        var cells = data.Cells.ToDictionary(c => c.Col);
        Assert.Equal("3911 - 3960", cells[2].Value.Text); // بازه متن می‌ماند
        Assert.Equal(50m, cells[8].Value.Number);
        Assert.Equal(0.16m, cells[11].Value.Number);
        Assert.Equal(7.2m, cells[13].Value.Number);
        Assert.Equal($"ROUND(I{data.Number}*J{data.Number}, 2)", cells[11].Formula);
        Assert.Equal($"ROUND(K{data.Number}*L{data.Number}, 2)", cells[13].Formula);

        // یک ردیف: ستون A ادغام نمی‌شود
        Assert.DoesNotContain(main.Merges, m => m.FirstCol == 1 && m.FirstRow == data.Number);
        Assert.Equal(1, cells[1].RowSpan);

        var footer = Footer(main, 29).Cells.ToDictionary(c => c.Col);
        var r = data.Number;
        Assert.Equal(50m, footer[5].Value.Number);
        Assert.Equal($"SUM(H{r}:H{r})", footer[5].Formula);
        Assert.Equal(8m, footer[10].Value.Number);
        Assert.Equal($"SUMPRODUCT(H{r}:H{r},K{r}:K{r})", footer[10].Formula);
        Assert.Equal(360m, footer[12].Value.Number);
        Assert.Equal($"SUMPRODUCT(H{r}:H{r},M{r}:M{r})", footer[12].Formula);
        Assert.Equal(2, footer[10].ColSpan); // J:K
        Assert.Equal(2, footer[12].ColSpan); // L:M
    }

    [Fact]
    public void Bale30_TwoLotRows_Footer34_6p24_280p80()
    {
        var main = Full().Sheets[0];
        var rows = main.Rows.Where(r => r.Kind == RenderRowKinds.Data && r.Group == 30).ToList();
        Assert.Equal([24m, 10m], rows.Select(r => r.Cells.Single(c => c.Col == 8).Value.Number));
        Assert.Equal([0.16m, 0.24m], rows.Select(r => r.Cells.Single(c => c.Col == 11).Value.Number));
        Assert.Equal([7.2m, 10.8m], rows.Select(r => r.Cells.Single(c => c.Col == 13).Value.Number));

        var footer = Footer(main, 30).Cells.ToDictionary(c => c.Col);
        Assert.Equal(34m, footer[5].Value.Number);
        Assert.Equal(6.24m, footer[10].Value.Number);
        Assert.Equal(280.80m, footer[12].Value.Number);
        Assert.Contains(main.Merges, m => m.FirstCol == 1 && m.LastCol == 1 && m.FirstRow == rows[0].Number && m.LastRow == rows[1].Number);
    }

    [Fact]
    public void Bale31_StartsWith4004_AndBale33sFooterSays33()
    {
        var main = Full().Sheets[0];
        var first = main.Rows.First(r => r.Kind == RenderRowKinds.Data && r.Group == 31);
        Assert.Equal(4004m, first.Cells.Single(c => c.Col == 2).Value.Number);
        Assert.Equal(33m, Footer(main, 33).Cells.Single(c => c.Col == 3).Value.Number);
    }

    [Theory]
    [InlineData("3789", "2.9")]
    [InlineData("3816", "3.8")]
    [InlineData("3853", "0.76")]
    [InlineData("3882", "0.39")]
    [InlineData("3901", "0.75")]
    [InlineData("4009", "0.4")]
    public void MidpointRows_CacheTheDecimalValue(string serial, string area)
    {
        var row = SampleDocuments.RowOfSerial(Full().Sheets[0], serial);
        var cell = row.Cells.Single(c => c.Col == 11);
        Assert.Equal(decimal.Parse(area, System.Globalization.CultureInfo.InvariantCulture), cell.Value.Number);
        Assert.Equal(area, cell.Display); // قالب General: «3.8»، نه «3.80»
    }

    [Fact]
    public void Full_PageBreaks_FallOnlyBetweenBales_AndReproduceTheMerchantsPages()
    {
        var main = Full().Sheets[0];
        var plan = main.Pages!;
        Assert.Equal(44, plan.Capacity);
        Assert.Equal([1, 11, 20, 23, 28], plan.PageFirstGroups.Take(5));
        Assert.Empty(plan.TooLongGroups);

        var footerRows = main.Rows.Where(r => r.Kind == RenderRowKinds.Footer).Select(r => r.Number).ToHashSet();
        Assert.NotEmpty(main.RowBreaksAfter);
        Assert.All(main.RowBreaksAfter, after => Assert.Contains(after, footerRows));
        Assert.Equal(43, main.RowBreaksAfter[0]); // صفحهٔ ۲ با نگلهٔ ۱۱ شروع می‌شود
    }

    [Fact]
    public void Summary_TitleLinksEqualsRowAndTotals_AreAsSpecified()
    {
        var model = Full();
        var main = model.Sheets[0];
        var summary = model.Sheets[1];
        Assert.Equal("خلاصه", summary.Name);
        Assert.False(summary.TabSelected);
        Assert.Contains(summary.Merges, m => m.A1 == "C1:H1");

        var title = At(summary, "C1");
        Assert.Equal("صورت نگله بندی و اظهار فرش صادراتی  تاریخ1405.06.15", title.Value.Text);
        Assert.Null(title.Formula);
        var titleStyle = model.Styles[title.Style];
        Assert.Equal("FFFFFF00", titleStyle.FillRgb);
        Assert.Equal(BorderStyles.None, titleStyle.Border);

        Assert.Equal(["نگله", "تعداد فرش", "متراژ", "ارزش گمرکی"], new[] { "D2", "E2", "F2", "G2" }.Select(a => At(summary, a).Value.Text));
        Assert.All(new[] { "D2", "E2", "F2", "G2" }, a => Assert.Equal(BorderStyles.Thin, model.Styles[At(summary, a).Style].Border));

        var links = summary.Rows.Where(r => r.Kind == RenderRowKinds.Summary).ToList();
        Assert.Equal(36, links.Count);
        foreach (var link in links)
        {
            var footer = Footer(main, link.Group!.Value).Number;
            var byCol = link.Cells.ToDictionary(c => c.Col);
            Assert.Equal($"'اظهار'!C{footer}", byCol[4].Formula);
            Assert.Equal($"'اظهار'!E{footer}", byCol[5].Formula);
            Assert.Equal($"'اظهار'!J{footer}", byCol[6].Formula); // با دسته: J و L
            Assert.Equal($"'اظهار'!L{footer}", byCol[7].Formula);
            Assert.All(byCol.Values, c => Assert.Equal(BorderStyles.Thin, model.Styles[c.Style].Border));
        }

        var equals = summary.Rows.Single(r => r.Kind == RenderRowKinds.EqualsRow);
        Assert.Equal(links[^1].Number + 1, equals.Number);
        Assert.All(equals.Cells, c =>
        {
            Assert.Equal("=", c.Value.Text);
            Assert.Null(c.Formula);
            Assert.False(c.QuotePrefix);
            Assert.Equal(BorderStyles.None, model.Styles[c.Style].Border);
        });

        var totals = summary.Rows.Single(r => r.Kind == RenderRowKinds.Totals);
        Assert.Equal(equals.Number + 1, totals.Number);
        var d = totals.Cells.Single(c => c.Col == 4);
        Assert.True(d.Value.IsBlank);
        Assert.Equal(BorderStyles.Thin, model.Styles[d.Style].Border);

        // همان پهنای ستون‌ها و نما، بی عنوان تکرارشونده و بی شکستن
        Assert.Equal(main.Columns.Select(c => c.Width), summary.Columns.Take(main.Columns.Count).Select(c => c.Width));
        Assert.Equal(0, summary.PrintTitleRows);
        Assert.Empty(summary.RowBreaksAfter);
    }

    [Fact]
    public void LotFree_SummaryLinks_UseIAndK()
    {
        var model = SampleDocuments.Compose(SampleDocuments.Shipment(b => b <= 10));
        var first = model.Sheets[1].Rows.First(r => r.Kind == RenderRowKinds.Summary).Cells.ToDictionary(c => c.Col);
        Assert.Equal("'اظهار'!C7", first[4].Formula);
        Assert.Equal("'اظهار'!E7", first[5].Formula);
        Assert.Equal("'اظهار'!I7", first[6].Formula);
        Assert.Equal("'اظهار'!K7", first[7].Formula);
        Assert.Equal(13.51m, first[6].Value.Number);
    }

    [Fact]
    public void EmptyBale_IsSkippedWithAWarning_AndNoRangeIsReversed()
    {
        var data = SampleDocuments.Shipment(b => b <= 3);
        data = data with { Bales = [.. data.Bales.Take(2), new DocumentBale(5, []), data.Bales[2]] };
        var model = SampleDocuments.Compose(data);

        var warning = Assert.Single(model.Warnings);
        Assert.Equal("W_EMPTY_BALE", warning.Code);
        Assert.Equal(5, warning.Group);
        Assert.Equal("نگلهٔ ۵ خالی بود و در خروجی نیامد.", warning.Message);

        Assert.DoesNotContain(model.Sheets[0].Rows, r => r.Group == 5);
        Assert.Equal(3, model.Sheets[1].Rows.Count(r => r.Kind == RenderRowKinds.Summary));
        foreach (var formula in model.Sheets.SelectMany(s => s.Rows).SelectMany(r => r.Cells).Select(c => c.Formula).OfType<string>())
        {
            foreach (Match m in RangePattern().Matches(formula))
                Assert.True(int.Parse(m.Groups[2].Value) <= int.Parse(m.Groups[4].Value), $"reversed range in {formula}");
        }
    }

    [Fact]
    public void Adl_RendersAdlEverywhere()
    {
        var model = SampleDocuments.Compose(SampleDocuments.Shipment(b => b <= 3, BaleTerms.Adl));
        var main = model.Sheets[0];
        Assert.StartsWith("صورت عدل بندی", At(main, "A1").Value.Text);
        Assert.Equal("صورت عدل بندی و اظهار فرش صادراتی    تاریخ  1405.06.15", At(main, "A1").Value.Text);
        Assert.Equal("عدل", At(main, "A2").Value.Text);
        Assert.Equal("عدل", main.Columns[0].Label);
        Assert.All(main.Rows.Where(r => r.Kind == RenderRowKinds.Footer), r => Assert.Equal("عدل شماره", r.Cells.Single(c => c.Col == 1).Value.Text));
        Assert.Equal("عدل", At(model.Sheets[1], "D2").Value.Text);
        Assert.StartsWith("صورت عدل بندی", At(model.Sheets[1], "C1").Value.Text);
        Assert.DoesNotContain(model.Sheets.SelectMany(s => s.Rows).SelectMany(r => r.Cells), c => c.Value.AsText().Contains("نگله", StringComparison.Ordinal));
    }

    [Fact]
    public void Draft_GetsTheDraftHeader_FinalHasNone()
    {
        Assert.Equal("پیش‌نویس", SampleDocuments.Compose(SampleDocuments.Shipment(b => b <= 2, isDraft: true)).Page.CenterHeader);
        Assert.Null(SampleDocuments.Compose(SampleDocuments.Shipment(b => b <= 2)).Page.CenterHeader);
    }

    [Fact]
    public void Finalized_RendersFromTheSnapshot_Identically()
    {
        // محمولهٔ قطعی از SnapshotJson ساخته می‌شود: همان رکورد پس از رفت‌وبرگشت JSON همان سند را می‌دهد
        var live = SampleDocuments.Shipment();
        var json = JsonSerializer.Serialize(live);
        var snapshot = JsonSerializer.Deserialize<DocumentData>(json)!;
        var a = SampleDocuments.Compose(live);
        var b = SampleDocuments.Compose(snapshot);
        Assert.Equal(Flatten(a), Flatten(b));
    }

    [Fact]
    public void CheckAgainst_WarnsWhenAFooterDisagreesWithTheLedger()
    {
        var data = SampleDocuments.Shipment(b => b <= 2);
        data = data with { Bales = [data.Bales[0] with { AreaM2 = 13.50m }, data.Bales[1]] };
        var warning = Assert.Single(SampleDocuments.Compose(data).Warnings);
        Assert.Equal("W_CHECK_MISMATCH", warning.Code);
        Assert.Equal(1, warning.Group);
        Assert.Contains("مساحت", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Serials_OnlyPlainDigitsBecomeNumbers_AndDataTextGetsTheQuotePrefix()
    {
        var rug = new DocumentRug(null, "0123", 1, Pattern: "=SUM(1)", Origin: "-گبه", Material: "پشم");
        var data = new DocumentData(new DocumentShipment(null, 1, null, SampleDocuments.SampleDate, null, null, null, null, null),
            [new DocumentBale(1, [
                new DocumentLine(1, rug, 2m, 1m, UsdPerM2: 45m),
                new DocumentLine(2, rug with { Serial = "1234567890123456", Pattern = "ساده", Origin = "گبه" }, 2m, 1m, UsdPerM2: 45m),
                new DocumentLine(3, rug with { Serial = "A-12", Pattern = "@x", Origin = "+1" }, 2m, 1m, UsdPerM2: 45m)])],
            [], BaleTerms.Negleh, new DocumentTenant(), false);
        var main = SampleDocuments.Compose(data).Sheets[0];
        var cells = SampleDocuments.Cells(main);
        Assert.Equal(DocValueKind.Text, cells["B3"].Value.Kind); // «0123»
        Assert.Equal(DocValueKind.Text, cells["B4"].Value.Kind); // ۱۶ رقم
        Assert.Equal(DocValueKind.Text, cells["B5"].Value.Kind);
        Assert.True(cells["E3"].QuotePrefix);
        Assert.True(cells["F3"].QuotePrefix);
        Assert.False(cells["E4"].QuotePrefix);
        Assert.True(cells["E5"].QuotePrefix);
        Assert.True(cells["F5"].QuotePrefix);
        Assert.Equal("=SUM(1)", cells["E3"].Value.Text);
        Assert.Null(cells["E3"].Formula);
    }

    [Fact]
    public void PreviewMaxGroups_TruncatesTheModel_ButFilesAreComplete()
    {
        var model = SampleDocuments.Compose(SampleDocuments.Shipment(), options: new ComposeOptions(MaxGroups: 3, Fields: SampleDocuments.Catalogue()));
        Assert.True(model.Truncated);
        Assert.Equal(3, model.Sheets[0].Rows.Count(r => r.Kind == RenderRowKinds.Footer));
        Assert.False(Full().Truncated);
    }

    private static List<string> Flatten(RenderModel model)
        => model.Sheets.SelectMany(s => s.Rows.SelectMany(r => r.Cells.Select(c => $"{s.Name}!{c.Address}|{c.Value.Kind}|{c.Value.AsText()}|{c.Formula}|{c.Style}")))
            .Concat(model.Sheets.SelectMany(s => s.Merges.Select(m => $"{s.Name}:{m.A1}")))
            .Concat(model.Sheets.SelectMany(s => s.RowBreaksAfter.Select(b => $"{s.Name}:brk{b}")))
            .ToList();

    [GeneratedRegex(@"\b([A-Z]{1,3})(\d+):([A-Z]{1,3})(\d+)\b")]
    private static partial Regex RangePattern();
}
