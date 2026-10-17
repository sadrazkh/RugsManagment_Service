using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Documents.Composer;
using RugsManagment.Application.Features.Documents.Data;
using RugsManagment.Application.Features.Documents.Expressions;
using RugsManagment.Application.Fields;

namespace RugsManagment.Tests.Documents;

/// <summary>
/// بقیهٔ موتور سند: نام فایل دانلود (E.7، RFC 5987)، فهرست فرش (<c>rug-list.v1</c>، E.1)، نشانه‌های واژهٔ نگله و فیلدهای
/// کاتالوگ سند.
/// </summary>
public class DocumentEngineMiscTests
{
    [Fact]
    public void DownloadName_IsAsciiPlusRfc5987_WithTheDraftSuffixOnDrafts()
    {
        var date = new DateOnly(2026, 9, 6);
        Assert.Equal("declaration-12-2026-09-06.xlsx", DocumentFileName.Ascii("declaration", 12, date));
        Assert.Equal("اظهار-12-1405.06.15-پیش‌نویس.xlsx", DocumentFileName.Persian("اظهار", 12, date, isDraft: true));
        Assert.Equal("اظهار-12-1405.06.15.xlsx", DocumentFileName.Persian("اظهار", 12, date, isDraft: false));

        var header = DocumentFileName.ContentDisposition(
            DocumentFileName.Ascii("declaration", 12, date), DocumentFileName.Persian("اظهار", 12, date, isDraft: true));
        Assert.StartsWith("attachment; filename=\"declaration-12-2026-09-06.xlsx\"; filename*=UTF-8''", header, StringComparison.Ordinal);
        var encoded = header[(header.IndexOf("''", StringComparison.Ordinal) + 2)..];
        Assert.Matches("^[A-Za-z0-9%!#$&+\\-.^_`|~]+$", encoded);
        Assert.Equal("اظهار-12-1405.06.15-پیش‌نویس.xlsx", Uri.UnescapeDataString(encoded));
        Assert.Contains("%E2%80%8C", encoded, StringComparison.Ordinal); // نیم‌فاصله گم نمی‌شود
    }

    [Fact]
    public void RugList_ComposesPerPiece_WithAWeightedGrandTotal()
    {
        var rugs = new[]
        {
            new DocumentRug(null, "3778", 1, Pattern: "ساده", Origin: "گبه", IntakeLength: 2.08m, IntakeWidth: 2.1m, Location: "در انبار"),
            new DocumentRug(null, "3911 - 3960", 50, Origin: "گبه", IntakeLength: 0.4m, IntakeWidth: 0.4m, Location: "در انبار"),
            new DocumentRug(null, "3779", 1, Pattern: "ساده", Origin: "گبه", IntakeLength: 2.3m, IntakeWidth: 1.71m, FinalLength: 2.3m, FinalWidth: 1.65m)
        };
        var data = new DocumentData(null, [], rugs.Select((r, i) => new DocumentLine(i + 1, r, r.Length, r.Width)).ToList(),
            BaleTerms.Negleh, new DocumentTenant(), IsDraft: false);
        var model = DocumentComposer.Compose(SampleDocuments.RugList(), data, new ComposeOptions(Fields: SampleDocuments.Catalogue()));
        var main = model.Sheets.Single();
        var cells = SampleDocuments.Cells(main);

        Assert.True(model.HasLots);
        Assert.Equal(["شماره", "تعداد", "نقشه", "محل بافت", "اندازه", "مساحت", "جای فرش"], main.Columns.Select(c => c.Label));
        Assert.Equal("فهرست فرش", cells["A1"].Value.Text);
        Assert.Contains(main.Merges, m => m.A1 == "A1:G1");
        Assert.Equal(50m, cells["B4"].Value.Number);
        Assert.Equal(0.16m, cells["F4"].Value.Number);
        Assert.Equal("2.3×1.65", cells["E5"].Value.Text); // اندازهٔ نهایی بر اندازهٔ اول مقدم است
        Assert.Equal(3.8m, cells["F5"].Value.Number);

        var total = main.Rows.Single(r => r.Kind == RenderRowKinds.GrandTotal).Cells.ToDictionary(c => c.Col);
        Assert.Equal("جمع", total[1].Value.Text);
        Assert.Equal(52m, total[3].Value.Number);
        Assert.Equal("SUM(B3:B5)", total[3].Formula);
        Assert.Equal(4.37m + 8m + 3.8m, total[6].Value.Number);
        Assert.Equal("SUMPRODUCT(B3:B5,F3:F5)", total[6].Formula);
        Assert.Empty(main.RowBreaksAfter);
    }

    [Fact]
    public void RugList_WithoutLots_HidesQty_AndCountsRows()
    {
        var rugs = new[] { new DocumentRug(null, "1", 1, IntakeLength: 1m, IntakeWidth: 1m), new DocumentRug(null, "2", 1, IntakeLength: 2m, IntakeWidth: 1m) };
        var data = new DocumentData(null, [], rugs.Select((r, i) => new DocumentLine(i + 1, r, r.Length, r.Width)).ToList(),
            BaleTerms.Negleh, new DocumentTenant(), IsDraft: false);
        var main = DocumentComposer.Compose(SampleDocuments.RugList(), data, new ComposeOptions(Fields: SampleDocuments.Catalogue())).Sheets.Single();
        Assert.Equal(6, main.Columns.Count);
        var total = main.Rows.Single(r => r.Kind == RenderRowKinds.GrandTotal).Cells.ToDictionary(c => c.Col);
        Assert.Equal("ROWS(A3:A4)", total[2].Formula);
        Assert.Equal(2m, total[2].Value.Number);
        Assert.Equal("SUM(E3:E4)", total[5].Formula);
        Assert.Equal(3m, total[5].Value.Number);
    }

    [Fact]
    public void Tokens_FollowTheBaleTerm()
    {
        Assert.Equal("نگله شماره", DocumentFields.ReplaceTokens("{terms.baleFooter}", BaleTerms.Negleh));
        Assert.Equal("عدل · عدل‌ها · عدل · عدل شماره", DocumentFields.ReplaceTokens("{terms.bale} · {terms.bales} · {terms.baleOf} · {terms.baleFooter}", BaleTerms.Adl));
        Assert.Equal("بی نشانه", DocumentFields.ReplaceTokens("بی نشانه", BaleTerms.Adl));
    }

    [Fact]
    public void Builtin_CoversTheCatalogueKeysTheDefaultsUse()
    {
        var keys = DocumentFields.Catalogue().Select(f => f.Key).ToHashSet();
        foreach (var key in new[]
                 {
                     "rug.serial", "rug.qty", "rug.pattern", "rug.origin", "rug.material", "rug.intakeLength", "rug.intakeWidth",
                     "line.length", "line.width", "line.area", "line.usdPerM2", "line.pieceUsd", "line.rateSource",
                     "bale.number", "bale.pieces", "bale.area", "bale.usd",
                     "shipment.number", "shipment.title", "shipment.documentDate", "shipment.destination", "shipment.buyerName",
                     "shipment.declarationNo", "shipment.customsOffice", "shipment.usdRate",
                     "tenant.exporterName", "tenant.exporterCode", "tenant.exporterAddress", "tenant.exporterPhone", "tenant.customsOffice",
                     "terms.bale", "terms.bales", "terms.baleOf", "terms.baleFooter"
                 })
            Assert.Contains(key, keys);

        // دلار گمرکی و نرخ دلار پول نیستند (جدول E.3)؛ فقط مبلغ‌های تومانی فرش پول‌اند
        Assert.All(DocumentFields.Builtin(), f => Assert.False(f.Money));
        Assert.Contains(DocumentFields.Builtin(BaleTerms.Adl), f => f.Key == "bale.number" && f.Label == "شمارهٔ عدل");
        Assert.Equal(ExprType.Number, DocumentFields.TypeOf(DocumentFields.Builtin().Single(f => f.Key == "bale.area")));
        Assert.Equal(ExprType.Date, DocumentFields.TypeOf(DocumentFields.Builtin().Single(f => f.Key == "shipment.documentDate")));
    }

    [Fact]
    public void FieldValues_ComeFromTheRecordGraph()
    {
        var rug = new DocumentRug(null, "3816", 1, IntakeLength: 2.3m, IntakeWidth: 1.7m, FinalLength: 2.3m, FinalWidth: 1.65m,
            Custom: new Dictionary<string, string?> { ["bg_color"] = "آبی", ["knots"] = "40" },
            Money: new Dictionary<string, decimal?> { ["rug.totalCost"] = 1_000m });
        var line = new DocumentLine(1, rug, rug.Length, rug.Width, UsdPerM2: 45m);
        var bale = new DocumentBale(13, [line]);
        var data = new DocumentData(new DocumentShipment(null, 7, "x", new DateOnly(2026, 9, 6), null, null, null, null, 105_000m), [bale], [],
            BaleTerms.Adl, new DocumentTenant("کارگاه", "صادرکننده", "۱۲۳", "بازار", "۰۲۱", "گمرک تهران"), false);
        var knots = new FieldDefDto("rug.custom.knots", "گره", FieldScope.Rug, FieldDataType.Int, "0", false, false, true);

        Assert.Equal(3.8m, DocumentFields.Value("line.area", data, bale, line, null).Number);
        Assert.Equal(171m, DocumentFields.Value("line.pieceUsd", data, bale, line, null).Number);
        Assert.Equal(3.8m, DocumentFields.Value("bale.area", data, bale, null, null).Number);
        Assert.Equal(13m, DocumentFields.Value("bale.number", data, bale, null, null).Number);
        Assert.Equal("آبی", DocumentFields.Value("rug.custom.bg_color", data, bale, line, null).Text);
        Assert.Equal(40m, DocumentFields.Value("rug.custom.knots", data, bale, line, knots).Number);
        Assert.Equal(1_000m, DocumentFields.Value("rug.totalCost", data, bale, line, null).Number);
        Assert.Equal("عدل شماره", DocumentFields.Value("terms.baleFooter", data, null, null, null).Text);
        Assert.Equal(new DateOnly(2026, 9, 6), DocumentFields.Value("shipment.documentDate", data, null, null, null).Date);
        Assert.Equal("صادرکننده", DocumentFields.Value("tenant.exporterName", data, null, null, null).Text);
        Assert.Equal("۱۲۳", DocumentFields.Value("tenant.exporterCode", data, null, null, null).Text);
        Assert.Equal("بازار", DocumentFields.Value("tenant.exporterAddress", data, null, null, null).Text);
        Assert.Equal("۰۲۱", DocumentFields.Value("tenant.exporterPhone", data, null, null, null).Text);
        Assert.Equal("گمرک تهران", DocumentFields.Value("tenant.customsOffice", data, null, null, null).Text);
        Assert.Equal(105_000m, DocumentFields.Value("shipment.usdRate", data, null, null, null).Number);
        Assert.Equal("کارگاه", DocumentFields.Value("tenant.name", data, null, null, null).Text);
        Assert.True(DocumentFields.Value("rug.serial", data, bale, null, null).IsBlank); // بی خط
    }
}
