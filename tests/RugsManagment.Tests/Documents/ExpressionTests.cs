using RugsManagment.Application.Features.Documents.Composer;
using RugsManagment.Application.Features.Documents.Expressions;
using RugsManagment.Application.Features.Documents.Layout;

namespace RugsManagment.Tests.Documents;

/// <summary>
/// زبان عبارت سند (spec E.4، L.1 «Expressions»): اولویت‌ها مثل اکسل، منفی یک‌تایی، خانهٔ خالی به روش اکسل، تقسیم بر صفر
/// → خالی، <c>if/and/or/not</c>، <c>&amp;</c>، رد مقایسهٔ متن، مرزها؛ و ترجمه به فرمول اکسل با و بی ستون «تعداد».
/// </summary>
public class ExpressionTests
{
    // ── بافت‌های آزمون ──────────────────────────────────────────────────

    private sealed class Values(Dictionary<string, DocValue>? columns = null, Dictionary<string, DocValue>? fields = null) : IEvaluationContext
    {
        public DocValue Field(string key) => fields?.GetValueOrDefault(key) ?? DocValue.Blank;

        public DocValue Column(string key) => columns?.GetValueOrDefault(key) ?? DocValue.Blank;

        public DocValue Aggregate(string function, IReadOnlyList<string> columnKeys) => DocValue.Blank;
    }

    private sealed class Cells(Dictionary<string, ColumnBinding> columns, Dictionary<string, DocValue>? fields = null) : ICompileContext
    {
        public int Row => 3;

        public ColumnBinding Column(string key) => columns.TryGetValue(key, out var b) ? b : ColumnBinding.Hidden(DocValue.Blank);

        public DocValue Field(string key) => fields?.GetValueOrDefault(key) ?? DocValue.Blank;

        public string? Aggregate(string function, IReadOnlyList<string> columnKeys) => null;
    }

    private sealed class Scope(bool columns = true, bool aggregates = false) : ITypeScope
    {
        public ExprType? FieldType(string key) => key switch
        {
            "rug.origin" => ExprType.Text,
            "line.length" => ExprType.Number,
            "shipment.documentDate" => ExprType.Date,
            _ => null
        };

        public bool FieldAllowed(string key) => true;

        public ExprType? ColumnType(string key) => key switch { "name" => ExprType.Text, "a" or "b" or "length" or "width" => ExprType.Number, _ => null };

        public bool ColumnsAllowed => columns;

        public bool AggregatesAllowed => aggregates;
    }

    private static Expr Parse(string text)
    {
        var result = ExpressionParser.Parse(text);
        Assert.True(result.Ok, result.Error?.Code);
        return result.Expression!;
    }

    private static DocValue Eval(string text, Values? context = null) => Evaluator.Evaluate(Parse(text), context ?? new Values());

    private static decimal Num(string text, Values? context = null) => Eval(text, context).AsNumber();

    // ── خواندن و ارزیابی ────────────────────────────────────────────────

    [Theory]
    [InlineData("1+2*3", "7")]
    [InlineData("(1+2)*3", "9")]
    [InlineData("2-3-4", "-5")]
    [InlineData("8/4/2", "1")]
    [InlineData("-2*3", "-6")]
    [InlineData("--2", "2")]
    [InlineData("2*-3", "-6")]
    [InlineData("10-2*3+1", "5")]
    [InlineData("round(2.675, 2)", "2.68")]
    [InlineData("round(-2.675, 2)", "-2.68")]
    [InlineData("round(0.005, 2)", "0.01")]
    [InlineData("round(1234.5, -2)", "1200")]
    [InlineData("abs(-3)", "3")]
    [InlineData("min(3, 1, 2)", "1")]
    [InlineData("max(3, 1, 2)", "3")]
    [InlineData("if(1 > 2, 1, 2)", "2")]
    [InlineData("if(and(1 < 2, 2 <= 2), 1, 2)", "1")]
    [InlineData("if(or(1 = 2, 2 <> 2), 1, 2)", "2")]
    [InlineData("if(not(1 >= 2), 1, 2)", "1")]
    public void Evaluates_WithExcelPrecedenceAndAwayFromZero(string text, string expected)
        => Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), Num(text));

    [Fact]
    public void Blank_IsZeroInArithmetic_AndEmptyInConcatenation()
    {
        var context = new Values(new() { ["a"] = DocValue.Blank, ["b"] = DocValue.FromNumber(2.5m) });
        Assert.Equal(2.5m, Num("col.a + col.b", context));
        Assert.Equal("x", Eval("col.a & \"x\"", context).AsText());
        Assert.Equal("2.5 م", Eval("col.b & \" م\"", context).AsText());
        Assert.Equal("a1.5", Eval("\"a\" & 1.50").AsText());
    }

    [Fact]
    public void DivideByZero_IsBlank()
    {
        Assert.True(Eval("1/0").IsBlank);
        Assert.True(Eval("col.a / col.b", new Values(new() { ["a"] = DocValue.FromNumber(1m) })).IsBlank);
        Assert.Equal(0.5m, Num("1/2"));
    }

    [Fact]
    public void Coalesce_TakesTheSecondWhenTheFirstIsBlank()
    {
        Assert.Equal("b", Eval("coalesce(rug.origin, \"b\")").AsText());
        Assert.Equal("گبه", Eval("coalesce(rug.origin, \"b\")", new Values(fields: new() { ["rug.origin"] = DocValue.FromText("گبه") })).AsText());
    }

    [Fact]
    public void Jdot_WritesTheJalaliDate()
    {
        var context = new Values(fields: new() { ["shipment.documentDate"] = DocValue.FromDate(new DateOnly(2026, 9, 6)) });
        Assert.Equal("1405.06.15", Eval("jdot(shipment.documentDate)", context).AsText());
        Assert.Equal("1405.06.15", Evaluator.JalaliDot(new DateOnly(2026, 9, 6)));
    }

    [Fact]
    public void TextLiterals_DoubleQuoteEscape()
        => Assert.Equal("say \"hi\"", Eval("\"say \"\"hi\"\"\"").AsText());

    [Theory]
    [InlineData("1 +", "E_EXPR_SYNTAX")]
    [InlineData("(1", "E_EXPR_SYNTAX")]
    [InlineData("\"open", "E_EXPR_SYNTAX")]
    [InlineData("1.", "E_EXPR_SYNTAX")]
    [InlineData("1 # 2", "E_EXPR_SYNTAX")]
    [InlineData("", "E_EXPR_EMPTY")]
    [InlineData("foo(1)", "E_EXPR_UNKNOWN_FUNCTION")]
    [InlineData("round(1)", "E_EXPR_ARGS")]
    [InlineData("count(1)", "E_EXPR_ARGS")]
    [InlineData("origin", "E_LAYOUT_UNKNOWN_FIELD")]
    public void ParseErrors_HaveACode(string text, string code)
    {
        var result = ExpressionParser.Parse(text);
        Assert.False(result.Ok);
        Assert.Equal(code, result.Error!.Code);
    }

    [Fact]
    public void Limits_1000Characters_AndDepth32()
    {
        Assert.Equal("E_EXPR_TOO_LONG", ExpressionParser.Parse(new string('1', 1001)).Error!.Code);
        // درست ۱۰۰۰ نویسه هنوز مجاز است (متنی ۹۹۸ نویسه‌ای میان دو گیومه)؛ ۱۰۰۱ نه
        var atLimit = "\"" + new string('x', 998) + "\"";
        Assert.Equal(ExpressionLimits.MaxLength, atLimit.Length);
        Assert.True(ExpressionParser.Parse(atLimit).Ok);
        Assert.Equal("E_EXPR_TOO_LONG", ExpressionParser.Parse(atLimit + " ").Error!.Code);
        // عدد بیرون از بازهٔ decimal خطای نوشتار است، نه استثنا
        Assert.Equal("E_EXPR_SYNTAX", ExpressionParser.Parse("1+" + new string('1', 998)).Error!.Code);
        Assert.True(ExpressionParser.Parse(new string('(', 30) + "1" + new string(')', 30)).Ok);
        Assert.Equal("E_EXPR_TOO_DEEP", ExpressionParser.Parse(new string('(', 40) + "1" + new string(')', 40)).Error!.Code);
        Assert.Equal("E_EXPR_TOO_DEEP", ExpressionParser.Parse(new string('-', 40) + "1").Error!.Code);
        Assert.Equal("E_EXPR_TOO_DEEP", ExpressionParser.Parse(new string('+', 40) + "1").Error!.Code);
    }

    // ── نوع‌سنجی ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("\"a\" = \"b\"", "E_EXPR_TEXT_COMPARE")]
    [InlineData("col.name < 3", "E_EXPR_TEXT_COMPARE")]
    [InlineData("\"a\" + 1", "E_EXPR_NUMBER_EXPECTED")]
    [InlineData("if(1, 2, 3)", "E_EXPR_BOOL_EXPECTED")]
    [InlineData("if(1 > 2, 2, \"x\")", "E_EXPR_MIXED")]
    [InlineData("jdot(line.length)", "E_EXPR_DATE_EXPECTED")]
    [InlineData("shipment.documentDate & \"x\"", "E_EXPR_CONCAT_TYPE")]
    [InlineData("sum(col.a)", "E_EXPR_AGG_CONTEXT")]
    [InlineData("col.nope", "E_LAYOUT_UNKNOWN_COLUMN")]
    [InlineData("rug.nope", "E_LAYOUT_UNKNOWN_FIELD")]
    public void TypeErrors_AreRejected(string text, string code)
    {
        var result = TypeChecker.Check(Parse(text), new Scope());
        Assert.Contains(result.Errors, e => e.Code == code);
        Assert.Equal(ExprType.Error, result.Type);
    }

    [Fact]
    public void Aggregates_OnlyInFooters_AndOnlyOverColumns()
    {
        var footer = new Scope(columns: false, aggregates: true);
        Assert.True(TypeChecker.Check(Parse("sum(col.a) + 1"), footer).Ok);
        Assert.True(TypeChecker.Check(Parse("sumproduct(col.a, col.b)"), footer).Ok);
        Assert.True(TypeChecker.Check(Parse("count()"), footer).Ok);
        Assert.Contains(TypeChecker.Check(Parse("sum(1)"), footer).Errors, e => e.Code == "E_EXPR_AGG_ARG");
        Assert.Contains(TypeChecker.Check(Parse("col.a"), footer).Errors, e => e.Code == "E_EXPR_COLUMN_CONTEXT");
        Assert.Contains(TypeChecker.Check(Parse("sum(col.name)"), footer).Errors, e => e.Code == "E_EXPR_NUMBER_EXPECTED");
    }

    [Fact]
    public void Types_AreNumberTextBoolDate()
    {
        Assert.Equal(ExprType.Number, TypeChecker.Check(Parse("round(col.length*col.width,2)"), new Scope()).Type);
        Assert.Equal(ExprType.Text, TypeChecker.Check(Parse("rug.origin & 1"), new Scope()).Type);
        Assert.Equal(ExprType.Bool, TypeChecker.Check(Parse("1 < 2"), new Scope()).Type);
        Assert.Equal(ExprType.Date, TypeChecker.Check(Parse("shipment.documentDate"), new Scope()).Type);
    }

    // ── ترجمه به اکسل ───────────────────────────────────────────────────

    private static readonly Dictionary<string, ColumnBinding> NoLots = new()
    {
        ["length"] = ColumnBinding.At("H"), ["width"] = ColumnBinding.At("I"), ["area"] = ColumnBinding.At("J"),
        ["rate"] = ColumnBinding.At("K"), ["usd"] = ColumnBinding.At("L"), ["qty"] = ColumnBinding.Hidden(DocValue.FromNumber(1m))
    };

    private static readonly Dictionary<string, ColumnBinding> WithLots = new()
    {
        ["qty"] = ColumnBinding.At("H"), ["length"] = ColumnBinding.At("I"), ["width"] = ColumnBinding.At("J"),
        ["area"] = ColumnBinding.At("K"), ["rate"] = ColumnBinding.At("L"), ["usd"] = ColumnBinding.At("M")
    };

    private static CompiledFormula Compile(string text, Dictionary<string, ColumnBinding> columns, Dictionary<string, DocValue>? fields = null)
        => ExcelCompiler.Compile(Parse(text), new Cells(columns, fields));

    [Fact]
    public void Compile_Area_ShiftsWithTheQtyColumn()
    {
        Assert.Equal("ROUND(H3*I3, 2)", Compile("round(col.length*col.width,2)", NoLots).Formula);
        Assert.Equal("ROUND(I3*J3, 2)", Compile("round(col.length*col.width,2)", WithLots).Formula);
        Assert.Equal("ROUND(J3*K3, 2)", Compile("round(col.area*col.rate,2)", NoLots).Formula);
        Assert.Equal("ROUND(K3*L3, 2)", Compile("round(col.area*col.rate,2)", WithLots).Formula);
    }

    [Fact]
    public void Compile_PerLineExample_FoldsTheHiddenQty()
    {
        Assert.Equal("ROUND(I3*J3, 2)*H3", Compile("round(col.length*col.width,2)*col.qty", WithLots).Formula);
        Assert.Equal("ROUND(H3*I3, 2)", Compile("round(col.length*col.width,2)*col.qty", NoLots).Formula);
    }

    [Fact]
    public void Compile_CoalesceDivideAndQuotes()
    {
        var columns = new Dictionary<string, ColumnBinding> { ["a"] = ColumnBinding.At("H"), ["b"] = ColumnBinding.At("I") };
        Assert.Equal("IF(H3=\"\", I3, H3)", Compile("coalesce(col.a, col.b)", columns).Formula);
        Assert.Equal("IF(I3=0,\"\",H3/I3)", Compile("col.a / col.b", columns).Formula);
        Assert.Equal("H3/2", Compile("col.a / 2", columns).Formula);
        Assert.Equal("\"say \"\"hi\"\" \"&H3", Compile("\"say \"\"hi\"\" \" & col.a", columns).Formula);
        Assert.Equal("(H3+I3)*2", Compile("(col.a + col.b) * 2", columns).Formula);
        Assert.Equal("H3-(I3-1)", Compile("col.a - (col.b - 1)", columns).Formula);
        Assert.Equal("-H3", Compile("-col.a", columns).Formula);
        Assert.Equal("IF(H3>I3, H3, I3)", Compile("if(col.a > col.b, col.a, col.b)", columns).Formula);
    }

    [Fact]
    public void Compile_InlinesCatalogueFieldsAsConstants()
    {
        var columns = new Dictionary<string, ColumnBinding> { ["a"] = ColumnBinding.At("H") };
        var fields = new Dictionary<string, DocValue> { ["shipment.usdRate"] = DocValue.FromNumber(105000m) };
        Assert.Equal("ROUND(H3*105000, 0)", Compile("round(col.a * shipment.usdRate, 0)", columns, fields).Formula);
    }

    [Fact]
    public void Compile_AConstantRowOrALongLiteral_IsValueOnly()
    {
        var columns = new Dictionary<string, ColumnBinding> { ["a"] = ColumnBinding.At("H") };
        Assert.True(Compile("\"صورت \" & terms.bale", columns).ValueOnly);
        Assert.True(Compile("1 + 2", columns).ValueOnly);
        Assert.True(Compile("\"" + new string('x', 256) + "\" & col.a", columns).ValueOnly);
        Assert.False(Compile("\"" + new string('x', 255) + "\" & col.a", columns).ValueOnly);
    }

    [Fact]
    public void Compile_FooterAggregates_FollowTheQtyColumn()
    {
        var layout = SampleDocuments.Declaration();
        var main = layout.Main!;
        var spans = new[] { new RowSpan(3, 6) };

        var lots = new SheetColumnMap(main, hasLots: true, layout.LotRow);
        Assert.Equal("SUMPRODUCT(H3:H6,K3:K6)", FormulaResolver.Aggregate(ExpressionFunctions.Sum, ["area"], spans, lots));
        Assert.Equal("SUMPRODUCT(H3:H6,M3:M6)", FormulaResolver.Aggregate(ExpressionFunctions.Sum, ["usd"], spans, lots));
        Assert.Equal("SUM(H3:H6)", FormulaResolver.Aggregate(ExpressionFunctions.Count, [], spans, lots));
        Assert.Equal("SUM(H3:H6)", FormulaResolver.Aggregate(ExpressionFunctions.Sum, ["qty"], spans, lots));

        var plain = new SheetColumnMap(main, hasLots: false, layout.LotRow);
        Assert.Equal("SUM(J3:J6)", FormulaResolver.Aggregate(ExpressionFunctions.Sum, ["area"], spans, plain));
        Assert.Equal("SUM(L3:L6)", FormulaResolver.Aggregate(ExpressionFunctions.Sum, ["usd"], spans, plain));
        Assert.Equal("ROWS(B3:B6)", FormulaResolver.Aggregate(ExpressionFunctions.Count, [], spans, plain));

        // perLine: جمع ساده حتی با ستون تعداد
        var perLine = new SheetColumnMap(main, hasLots: true, LotRowModes.PerLine);
        Assert.Equal("SUM(K3:K6)", FormulaResolver.Aggregate(ExpressionFunctions.Sum, ["area"], spans, perLine));
        Assert.Equal("SUMPRODUCT(H3:H6,K3:K6)", FormulaResolver.Aggregate(ExpressionFunctions.SumProduct, ["qty", "area"], spans, perLine));
    }

    [Fact]
    public void DocNumber_General_HasNoTrailingZerosOrFloatNoise()
    {
        Assert.Equal("3.8", DocNumber.General(3.80m));
        Assert.Equal("20322.5", DocNumber.General(20322.50m));
        Assert.Equal("45", DocNumber.General(45.00m));
        Assert.Equal("0", DocNumber.General(-0.00m));
        Assert.Equal("-2.68", DocNumber.General(-2.680m));
    }
}
