using System.Text.Json.Nodes;
using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Documents.Layout;

namespace RugsManagment.Tests.Documents;

/// <summary>
/// اعتبارسنج قالب (spec E.2 «LayoutValidator rules»، L.1): هر قاعده با کد، متن فارسی و مسیر JSON. هر آزمون قالب پیش‌فرض
/// اظهار را می‌گیرد، یک جایش را خراب می‌کند و می‌بیند همان خطا در همان مسیر می‌آید.
/// </summary>
public class LayoutValidatorTests
{
    private static JsonObject Declaration() => JsonNode.Parse(LayoutJson.Default("shipment-declaration").Json)!.AsObject();

    private static JsonObject MainSheet(JsonObject layout) => layout["sheets"]![0]!.AsObject();

    private static JsonArray Columns(JsonObject layout) => MainSheet(layout)["columns"]!.AsArray();

    private static JsonArray Footer(JsonObject layout) => MainSheet(layout)["group"]!["footer"]!.AsArray();

    private static LayoutValidationResult Validate(JsonObject layout) => LayoutValidator.Validate(layout.ToJsonString(), SampleDocuments.Catalogue());

    private static LayoutIssue Single(LayoutValidationResult result, string code, string path)
    {
        var issue = result.Errors.FirstOrDefault(e => e.Code == code && e.Path == path);
        Assert.True(issue is not null, $"expected {code} at {path}; got: {string.Join(" | ", result.Errors.Select(e => $"{e.Code} {e.Path}"))}");
        // متن فارسی از Messages، نه خود کد
        Assert.Equal(Messages.Find(code)!.Code, code);
        Assert.DoesNotContain(code, issue!.Message, StringComparison.Ordinal);
        Assert.Matches("[؀-ۿ]", issue.Message);
        return issue;
    }

    [Fact]
    public void Defaults_Validate()
    {
        foreach (var template in LayoutJson.Defaults())
        {
            var result = LayoutValidator.Validate(template.Json, SampleDocuments.Catalogue());
            Assert.True(result.Ok, $"{template.SeedKey}.v{template.Version}: {string.Join(" | ", result.Errors.Select(e => $"{e.Code} {e.Path} {e.Message}"))}");
            Assert.Equal(template.SeedKey == "shipment-cost", result.UsesMoneyFields);
        }

        Assert.Contains(LayoutJson.Defaults(), d => d is { SeedKey: "shipment-declaration", Version: 1 });
        Assert.Contains(LayoutJson.Defaults(), d => d is { SeedKey: "rug-list", Version: 1 });
    }

    [Fact]
    public void Default_RoundTrips_ThroughTheSerializer()
    {
        var layout = SampleDocuments.Declaration();
        var again = LayoutJson.Parse(LayoutJson.Serialize(layout)).Layout!;
        Assert.True(LayoutValidator.Validate(again, SampleDocuments.Catalogue()).Ok);
        Assert.Equal(13, again.Main!.Columns.Count);
        Assert.Equal(9, again.Styles.Count);
    }

    [Fact]
    public void InvalidJson_AndUnknownMembers_AreRejected()
    {
        Assert.Equal("E_LAYOUT_JSON", LayoutValidator.Validate("{ not json", SampleDocuments.Catalogue()).Errors.Single().Code);
        var layout = Declaration();
        MainSheet(layout)["title"]!["fromcol"] = "bale"; // غلط تایپی بی‌صدا نادیده نمی‌ماند
        Assert.Equal("E_LAYOUT_JSON", Validate(layout).Errors.Single().Code);
    }

    [Fact]
    public void DuplicateColumnKey()
    {
        var layout = Declaration();
        Columns(layout)[1]!["key"] = "bale";
        Single(Validate(layout), "E_LAYOUT_DUPLICATE_KEY", "$.sheets[0].columns[1].key");
    }

    [Fact]
    public void DuplicateFooterId()
    {
        var layout = Declaration();
        Footer(layout)[2]!["id"] = "label";
        Single(Validate(layout), "E_LAYOUT_DUPLICATE_FOOTER", "$.sheets[0].group.footer[2].id");
    }

    [Fact]
    public void DuplicateStyleName()
    {
        var json = LayoutJson.Default("shipment-declaration").Json.Replace(
            "\"plain\":", "\"title\": { \"font\": \"B Nazanin\", \"size\": 11, \"border\": \"none\", \"hAlign\": \"center\", \"vAlign\": \"center\" },\n    \"plain\":",
            StringComparison.Ordinal);
        var result = LayoutValidator.Validate(json, SampleDocuments.Catalogue());
        Single(result, "E_LAYOUT_DUPLICATE_STYLE", "$.styles.title");
    }

    [Fact]
    public void UnknownField()
    {
        var layout = Declaration();
        Columns(layout)[4]!["field"] = "rug.nope";
        Single(Validate(layout), "E_LAYOUT_UNKNOWN_FIELD", "$.sheets[0].columns[4].field");
    }

    [Fact]
    public void UnknownColumnInAnExpression()
    {
        var layout = Declaration();
        Columns(layout)[10]!["expr"] = "round(col.length*col.nope,2)";
        Single(Validate(layout), "E_LAYOUT_UNKNOWN_COLUMN", "$.sheets[0].columns[10].expr");
    }

    [Fact]
    public void UnknownStyle()
    {
        var layout = Declaration();
        Columns(layout)[3]!["style"] = "bold";
        Single(Validate(layout), "E_LAYOUT_UNKNOWN_STYLE", "$.sheets[0].columns[3].style");
    }

    [Fact]
    public void UnknownOf()
    {
        var layout = Declaration();
        Footer(layout)[5]!["of"] = "sqm";
        Single(Validate(layout), "E_LAYOUT_UNKNOWN_COLUMN", "$.sheets[0].group.footer[5].of");
    }

    [Fact]
    public void UnknownFooterLinkInTheSummary()
    {
        var layout = Declaration();
        layout["sheets"]![1]!["columns"]![2]!["from"] = "footer:sqm";
        var result = Validate(layout);
        Single(result, "E_LAYOUT_UNKNOWN_FOOTER", "$.sheets[1].columns[2].from");
    }

    [Fact]
    public void CheckAgainst_UnknownFooter_AndANonAggregatableLedgerField()
    {
        var layout = Declaration();
        MainSheet(layout)["checkAgainst"]![0]!["footerId"] = "nope";
        MainSheet(layout)["checkAgainst"]![1]!["ledgerField"] = "shipment.number";
        var result = Validate(layout);
        Single(result, "E_LAYOUT_UNKNOWN_FOOTER", "$.sheets[0].checkAgainst[0].footerId");
        Single(result, "E_LAYOUT_LEDGER_FIELD", "$.sheets[0].checkAgainst[1].ledgerField");
    }

    [Fact]
    public void Cycle_IsRejected()
    {
        var layout = Declaration();
        Columns(layout)[10]!["expr"] = "round(col.usd/2,2)"; // area ← usd ← area
        var issue = Single(Validate(layout), "E_LAYOUT_CYCLE", "$.sheets[0].columns[10].expr");
        Assert.Contains("area", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SelfReference_IsACycle()
    {
        var layout = Declaration();
        Columns(layout)[10]!["expr"] = "col.area + 1";
        Single(Validate(layout), "E_LAYOUT_CYCLE", "$.sheets[0].columns[10].expr");
    }

    [Fact]
    public void MainTitle_MustUseColumnKeys_ButTheSummaryTitleUsesLetters()
    {
        var layout = Declaration();
        MainSheet(layout)["title"]!["fromCol"] = "A";
        MainSheet(layout)["title"]!["toCol"] = "nope";
        var result = Validate(layout);
        Single(result, "E_LAYOUT_LETTER_COL", "$.sheets[0].title.fromCol");
        Single(result, "E_LAYOUT_UNKNOWN_COLUMN", "$.sheets[0].title.toCol");
        // errata E8: C تا H در برگهٔ خلاصه درست است
        Assert.DoesNotContain(result.Errors, e => e.Path.StartsWith("$.sheets[1].title", StringComparison.Ordinal));
    }

    [Fact]
    public void GroupKeyColumn_MustBeAKey()
    {
        var layout = Declaration();
        MainSheet(layout)["group"]!["keyColumn"] = "A";
        Single(Validate(layout), "E_LAYOUT_LETTER_COL", "$.sheets[0].group.keyColumn");
    }

    [Fact]
    public void FooterCol_WrittenAsALetter_IsRejected()
    {
        var layout = Declaration();
        Footer(layout)[3]!["col"] = "E";
        var issue = Single(Validate(layout), "E_LAYOUT_LETTER_COL", "$.sheets[0].group.footer[3].col");
        Assert.Contains("کلید ستون", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AColumnKey_ThatLooksLikeALetter_IsRejected()
    {
        // وگرنه پانویسِ col: "E" هم کلید می‌شد هم حرف، و قاعدهٔ «col هرگز حرف نیست» دور زده می‌شد
        var layout = Declaration();
        Columns(layout)[4]!["key"] = "E";
        Footer(layout)[3]!["col"] = "E";
        Single(Validate(layout), "E_LAYOUT_LETTER_COL", "$.sheets[0].columns[4].key");
    }

    [Fact]
    public void FooterSpan_MustFitTheVisibleColumns()
    {
        var layout = Declaration();
        Footer(layout)[6]!["span"] = 3; // L:N بی دسته بیرون می‌زند
        Single(Validate(layout), "E_LAYOUT_SPAN", "$.sheets[0].group.footer[6].span");
    }

    [Fact]
    public void FooterCells_MustNotOverlap()
    {
        var layout = Declaration();
        Footer(layout)[5]!["span"] = 3; // I:K روی K:L
        Single(Validate(layout), "E_LAYOUT_OVERLAP", "$.sheets[0].group.footer[6].col");
    }

    [Fact]
    public void FooterOnAColumnThatHides_IsRejected()
    {
        var layout = Declaration();
        Footer(layout)[4]!["col"] = "qty";
        Single(Validate(layout), "E_LAYOUT_HIDDEN_ANCHOR", "$.sheets[0].group.footer[4].col");
    }

    [Fact]
    public void PerPiece_WithoutAQtyColumn_IsRejected()
    {
        var layout = Declaration();
        Columns(layout).RemoveAt(7);
        var issue = Single(Validate(layout), "E_LAYOUT_QTY_REQUIRED", "$.sheets[0].columns");
        Assert.Contains("تعداد", issue.Message, StringComparison.Ordinal);

        var never = Declaration();
        Columns(never)[7]!["visibleWhen"] = "never";
        Single(Validate(never), "E_LAYOUT_QTY_REQUIRED", "$.sheets[0].columns");

        var perLine = Declaration();
        perLine["lotRow"] = "perLine";
        Columns(perLine).RemoveAt(7);
        Assert.DoesNotContain(Validate(perLine).Errors, e => e.Code == "E_LAYOUT_QTY_REQUIRED");
    }

    [Fact]
    public void SummaryTotals_MustBeSummaryColumns()
    {
        var layout = Declaration();
        layout["sheets"]![1]!["totals"]!["sum"]![0] = "unit";
        Single(Validate(layout), "E_LAYOUT_TOTALS", "$.sheets[1].totals.sum[0]");
    }

    [Theory]
    [InlineData("border", "dotted", ".border")]
    [InlineData("colorRgb", "767171", ".colorRgb")]
    [InlineData("fillRgb", "yellow", ".fillRgb")]
    [InlineData("hAlign", "middle", ".hAlign")]
    public void StyleValues_ComeFromTheEnums(string member, string value, string suffix)
    {
        var layout = Declaration();
        layout["styles"]!["title"]![member] = value;
        Single(Validate(layout), "E_LAYOUT_STYLE_VALUE", "$.styles.title" + suffix);
    }

    [Fact]
    public void AtMost30Columns()
    {
        var layout = Declaration();
        for (var i = 0; i < 18; i++)
            Columns(layout).Add(new JsonObject { ["key"] = $"extra{i}", ["label"] = "x", ["width"] = 5, ["field"] = "rug.notes" });
        Single(Validate(layout), "E_LAYOUT_TOO_MANY_COLUMNS", "$.sheets[0].columns");
    }

    [Fact]
    public void AMoneyField_SetsUsesMoneyFields()
    {
        var layout = Declaration();
        Columns(layout).Add(new JsonObject { ["key"] = "cost", ["label"] = "بها", ["width"] = 10, ["field"] = "rug.purchaseCost" });
        var result = Validate(layout);
        Assert.True(result.Ok, string.Join(" | ", result.Errors.Select(e => $"{e.Code} {e.Path}")));
        Assert.True(result.UsesMoneyFields);

        var viaExpr = Declaration();
        Columns(viaExpr).Add(new JsonObject { ["key"] = "cost", ["label"] = "بها", ["width"] = 10, ["expr"] = "rug.totalCost * 2" });
        Assert.True(Validate(viaExpr).UsesMoneyFields);
    }

    [Fact]
    public void ABoolColumn_IsRejected()
    {
        var layout = Declaration();
        Columns(layout)[10]!["expr"] = "col.length > col.width";
        Single(Validate(layout), "E_LAYOUT_COLUMN_TYPE", "$.sheets[0].columns[10].expr");
    }

    [Fact]
    public void AColumn_NeedsExactlyOneSource()
    {
        var layout = Declaration();
        Columns(layout)[4]!["expr"] = "1";
        Single(Validate(layout), "E_LAYOUT_COLUMN_SOURCE", "$.sheets[0].columns[4]");
    }

    [Fact]
    public void ExpressionErrors_CarryTheJsonPathAndPosition()
    {
        var layout = Declaration();
        Columns(layout)[12]!["expr"] = "round(col.area*,2)";
        var issue = Single(Validate(layout), "E_EXPR_SYNTAX", "$.sheets[0].columns[12].expr");
        Assert.Contains("۱۶", issue.Message, StringComparison.Ordinal); // نزدیک نویسهٔ ۱۶
    }

    [Fact]
    public void FieldsOutOfScope_AreRejected()
    {
        var layout = Declaration();
        MainSheet(layout)["title"]!["expr"] = "rug.origin";
        Single(Validate(layout), "E_EXPR_FIELD_SCOPE", "$.sheets[0].title.expr");
    }

    [Fact]
    public void Sheets_ExactlyOneMain_AndValidNames()
    {
        var layout = Declaration();
        layout["sheets"]![1]!["role"] = "main";
        Single(Validate(layout), "E_LAYOUT_SHEETS", "$.sheets");

        var names = Declaration();
        names["sheets"]![1]!["name"] = "اظهار";
        Single(Validate(names), "E_LAYOUT_SHEET_NAME", "$.sheets[1].name");

        var chars = Declaration();
        MainSheet(chars)["name"] = "a/b";
        Single(Validate(chars), "E_LAYOUT_SHEET_NAME", "$.sheets[0].name");
    }

    [Fact]
    public void ColumnWidth_MustBePositive()
    {
        var layout = Declaration();
        Columns(layout)[0]!["width"] = 0;
        Single(Validate(layout), "E_LAYOUT_WIDTH", "$.sheets[0].columns[0].width");
    }
}
