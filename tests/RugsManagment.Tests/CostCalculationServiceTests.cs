using System.Globalization;
using System.Text.Json;
using RugsManagment.Application.DTOs.Pricing;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Tests;

/// <summary>
/// قیمت مرحله — پرریسک‌ترین منطق سامانه، چون خروجی‌اش مستقیم روی پول کارگاه اثر دارد.
///
/// از فاز ۲ (spec D.3 «Step pricing on lots»، L.1 «Existing»): قیمت اول برای یک تخته با گرد کردن نصفه-دور-از-صفر در
/// MoneyScale و بعد × تعداد، روی عکس اندازهٔ قیمت‌گذاری (<c>Priced*</c>)؛ ضریب دقیق فوت مربع ۱۰٫۷۶۳۹۱۰۴. مبلغ دستی برای کل
/// ردیف است و × تعداد نمی‌شود. همهٔ بردارهای <c>stepPrice</c> فایل مشترک هم از راه همین سرویس اجرا می‌شوند تا معلوم شود سرویس
/// همان RugCostMath را به کار می‌برد.
/// </summary>
public class CostCalculationServiceTests
{
    private readonly CostCalculationService _sut = new();

    private static Rug Rug(decimal width = 2m, decimal length = 3m, int quantity = 1)
    {
        var rug = new Rug();
        rug.SetIntakeSize(length, width);
        rug.SetQuantity(quantity);
        return rug;
    }

    private static ProcessStepType StepType(
        StepPricingModel model = StepPricingModel.PerSquareMeter, decimal rate = 100_000m) =>
        new() { DefaultPricingModel = model, DefaultUnitRate = rate };

    // ── فرمول‌های پایه ──

    [Fact]
    public void PerSquareMeter_MultipliesRateByArea()
    {
        // ۲×۳ = ۶ متر مربع × ۱۰۰٬۰۰۰
        var cost = _sut.ComputeBase(2m, 3m, StepPricingModel.PerSquareMeter, 100_000m, null);
        Assert.Equal(600_000m, cost);
    }

    [Fact]
    public void Fixed_IgnoresDimensions()
    {
        var small = _sut.ComputeBase(1m, 1m, StepPricingModel.Fixed, 250_000m, null);
        var large = _sut.ComputeBase(10m, 10m, StepPricingModel.Fixed, 250_000m, null);

        Assert.Equal(250_000m, small);
        Assert.Equal(small, large);
    }

    [Fact]
    public void PerLength_UsesLengthOnly()
        => Assert.Equal(150_000m, _sut.ComputeBase(2m, 3m, StepPricingModel.PerLength, 50_000m, null));

    [Fact]
    public void PerWidth_UsesWidthOnly()
        => Assert.Equal(100_000m, _sut.ComputeBase(2m, 3m, StepPricingModel.PerWidth, 50_000m, null));

    [Fact]
    public void PerSquareFoot_UsesTheExactFactor_AndRoundsAtTheMoneyScale()
    {
        // ۶ م² × ۱۰٫۷۶۳۹۱۰۴ × ۱٬۰۰۰ = ۶۴٬۵۸۳٫۴۶۲۴ → ۶۴٬۵۸۳ (رقم اعشار ۰) و ۶۴٬۵۸۳٫۴۶ (رقم اعشار ۲)؛ ضریب تقریبی ۱۰٫۷۶۳۹ قدیمی ۶۴٬۵۸۳٫۴ می‌داد
        Assert.Equal(64_583m, _sut.ComputeBase(2m, 3m, StepPricingModel.PerSquareFoot, 1_000m, null));
        Assert.Equal(64_583.46m, _sut.ComputeBase(2m, 3m, StepPricingModel.PerSquareFoot, 1_000m, null, moneyScale: 2));
    }

    [Fact]
    public void Midpoints_RoundAwayFromZero_NeverToEven()
    {
        // ۱۲٫۵ × ۱ م² = ۱۲٫۵ → ۱۳ (بانکی ۱۲ می‌داد)؛ ۲٫۵ × ۱ → ۳ (بانکی ۲)
        Assert.Equal(13m, _sut.ComputeBase(1m, 1m, StepPricingModel.PerSquareMeter, 12.5m, null));
        Assert.Equal(3m, _sut.ComputeBase(1m, 1m, StepPricingModel.Fixed, 2.5m, null));
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(2, 0)]
    [InlineData(0, 0)]
    public void ZeroDimension_ProducesZeroForAreaBasedModels(decimal width, decimal length)
        => Assert.Equal(0m, _sut.ComputeBase(width, length, StepPricingModel.PerSquareMeter, 100_000m, null));

    // ── قاعدهٔ دسته ──

    [Fact]
    public void Lot_RoundsPerPiece_ThenMultipliesByQuantity()
    {
        // ۴۵٫۵۵ × ۰٫۱۶ = ۷٫۲۸۸ → ۷ برای هر تخته → ۳۵۰ برای ۵۰ تخته (نه ROUND(۳۶۴٫۴۰) = ۳۶۴)
        Assert.Equal(350m, _sut.ComputeBase(0.40m, 0.40m, StepPricingModel.PerSquareMeter, 45.55m, null, quantity: 50));
        Assert.Equal(364.50m, _sut.ComputeBase(0.40m, 0.40m, StepPricingModel.PerSquareMeter, 45.55m, null, quantity: 50, moneyScale: 2));
    }

    public static TheoryData<string> StepPriceVectorIds()
    {
        var data = new TheoryData<string>();
        using var document = Fixtures.MathVectors();
        foreach (var v in document.RootElement.GetProperty("costVectors").EnumerateArray())
        {
            if (v.GetProperty("op").GetString() == "stepPrice")
                data.Add(v.GetProperty("id").GetString()!);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(StepPriceVectorIds))]
    public void SharedStepPriceVectors_ThroughTheService(string id)
    {
        using var document = Fixtures.MathVectors();
        var v = document.RootElement.GetProperty("costVectors").EnumerateArray()
            .Single(e => e.GetProperty("id").GetString() == id);

        var model = Enum.Parse<StepPricingModel>(v.GetProperty("model").GetString()!);
        string? json = null;
        if (v.TryGetProperty("items", out var items))
        {
            json = JsonSerializer.Serialize(new
            {
                items = items.EnumerateArray().Select(i => new
                {
                    model = (int)Enum.Parse<StepPricingModel>(i.GetProperty("model").GetString()!),
                    rate = D(i.GetProperty("rate").GetString()!)
                })
            });
        }

        var line = _sut.ComputeBase(
            D(v.GetProperty("width").GetString()!), D(v.GetProperty("length").GetString()!), model,
            D(v.GetProperty("rate").GetString()!), json, v.GetProperty("qty").GetInt32(), v.GetProperty("scale").GetInt32());

        Assert.Equal(D(v.GetProperty("line").GetString()!), line);
    }

    // ── حالت ترکیبی ──

    [Fact]
    public void Combined_SumsAllItems()
    {
        // ثابت ۵۰٬۰۰۰ + متری ۱۰٬۰۰۰ × ۶ م² = ۱۱۰٬۰۰۰
        const string json = """
            {"items":[{"model":0,"rate":50000},{"model":1,"rate":10000}]}
            """;

        Assert.Equal(110_000m, _sut.ComputeBase(2m, 3m, StepPricingModel.Combined, 0m, json));
    }

    [Fact]
    public void Combined_WithBrokenJson_ReturnsZeroInsteadOfThrowing()
    {
        // پیکربندی خراب نباید ثبت مرحله را بشکند
        var cost = _sut.ComputeBase(2m, 3m, StepPricingModel.Combined, 0m, "{ این JSON نیست");
        Assert.Equal(0m, cost);
    }

    [Fact]
    public void Combined_WithEmptyItems_ReturnsZero()
        => Assert.Equal(0m, _sut.ComputeBase(2m, 3m, StepPricingModel.Combined, 0m, """{"items":[]}"""));

    // ── اولویت مقادیر ──

    [Fact]
    public void ManualOverride_WinsOverEveryOtherSource_AndIsNeverMultipliedByQuantity()
    {
        var cost = _sut.CalculateStepCost(
            Rug(quantity: 50), StepType(rate: 100_000m),
            overrideModel: StepPricingModel.PerSquareMeter, overrideRate: 999_999m,
            manualOverride: 42_000m);

        Assert.Equal(42_000m, cost);
    }

    [Fact]
    public void StepInstanceRate_WinsOverBuildOverrideAndDefault()
    {
        var step = new RugWorkflowStep
        {
            AppliedPricingModel = StepPricingModel.Fixed,
            AppliedUnitRate = 77_000m
        };

        var cost = _sut.CalculateStepCost(
            Rug(), StepType(rate: 100_000m),
            overrideModel: StepPricingModel.PerSquareMeter, overrideRate: 5_000m,
            manualOverride: null, stepInstance: step);

        Assert.Equal(77_000m, cost);
    }

    /// <summary>
    /// جایگزینیِ روش و نرخ هنگام ساختن مسیر دستی (<c>CustomWorkflowStepRequest.Pricing</c>) بر پیش‌فرض نوع مرحله مقدم است.
    /// (آزمون قدیمی «جایگزین قالب» گمراه‌کننده بود: موتور هرگز جایگزین ردیف‌های قالب را به این سرویس نمی‌دهد؛ L.1.)
    /// </summary>
    [Fact]
    public void BuildTimeOverride_WinsOverStepTypeDefault()
    {
        var cost = _sut.CalculateStepCost(
            Rug(), StepType(StepPricingModel.PerSquareMeter, 100_000m),
            overrideModel: StepPricingModel.Fixed, overrideRate: 33_000m,
            manualOverride: null);

        Assert.Equal(33_000m, cost);
    }

    [Fact]
    public void NoOverrides_FallsBackToStepTypeDefault()
    {
        var cost = _sut.CalculateStepCost(
            Rug(), StepType(StepPricingModel.PerSquareMeter, 20_000m),
            overrideModel: null, overrideRate: null, manualOverride: null);

        Assert.Equal(120_000m, cost); // ۶ م² × ۲۰٬۰۰۰
    }

    [Fact]
    public void PricedSnapshot_IsUsedInsteadOfTheCurrentSize()
    {
        // فرش بعد از شستشو کوچک شد (نهایی ۲×۲)، ولی شستشو روی عکس ۲×۳ و ۱ تخته قیمت خورده بود
        var rug = Rug();
        rug.SetFinalSize(2m, 2m, DateTimeOffset.UtcNow, null);
        var step = new RugWorkflowStep();
        step.SnapshotPricingSize(3m, 2m, 1);

        var cost = _sut.CalculateStepCost(rug, StepType(StepPricingModel.PerSquareMeter, 20_000m), null, null, null, step);

        Assert.Equal(120_000m, cost);
    }

    [Fact]
    public void WithoutSnapshot_TheCurrentEffectiveSizeAndQuantityAreUsed()
    {
        var rug = Rug(0.40m, 0.40m, quantity: 50);

        var cost = _sut.CalculateStepCost(rug, StepType(StepPricingModel.PerSquareMeter, 85_000m), null, null, null);

        Assert.Equal(680_000m, cost); // ۱۳٬۶۰۰ × ۵۰ (D.7 #4)
    }

    // ── پیش‌نمایش و تخفیف ──

    [Fact]
    public void Preview_AppliesAdjustmentToTotal()
    {
        var result = _sut.Preview(new PricingPreviewRequest(
            2m, 3m, StepPricingModel.PerSquareMeter, 100_000m, null, null, Adjustment: -100_000m));

        Assert.Equal(600_000m, result.Base);
        Assert.Equal(500_000m, result.Total);
    }

    [Fact]
    public void Preview_NeverReturnsNegativeTotal()
    {
        // تخفیفی بزرگ‌تر از خود هزینه نباید مبلغ منفی بسازد
        var result = _sut.Preview(new PricingPreviewRequest(
            2m, 3m, StepPricingModel.PerSquareMeter, 10_000m, null, null, Adjustment: -999_999m));

        Assert.Equal(0m, result.Total);
    }

    [Fact]
    public void Preview_ManualCost_ShortCircuitsFormula()
    {
        var result = _sut.Preview(new PricingPreviewRequest(
            2m, 3m, StepPricingModel.PerSquareMeter, 100_000m, null, ManualCost: 15_000m, Adjustment: 0m));

        Assert.Equal(15_000m, result.Base);
        Assert.Equal("مبلغ دستی", result.Formula);
    }

    [Fact]
    public void Preview_Combined_ListsEachComponentSeparately()
    {
        const string json = """
            {"items":[{"model":0,"rate":50000},{"model":1,"rate":10000}]}
            """;

        var result = _sut.Preview(new PricingPreviewRequest(
            2m, 3m, StepPricingModel.Combined, null, json, null, Adjustment: 0m));

        Assert.Equal(2, result.Components.Count);
        Assert.Equal(110_000m, result.Total);
    }

    [Fact]
    public void Preview_AndCalculate_AgreeOnTheSameInput()
    {
        // اگر این دو از هم جدا شوند، کاربر عددی می‌بیند که با ثبت‌شده فرق دارد
        var preview = _sut.Preview(new PricingPreviewRequest(
            2.5m, 3.5m, StepPricingModel.PerSquareMeter, 80_000m, null, null, Adjustment: 0m));

        var calculated = _sut.CalculateStepCost(
            Rug(2.5m, 3.5m), StepType(StepPricingModel.PerSquareMeter, 80_000m),
            null, null, null);

        Assert.Equal(calculated, preview.Total);
    }

    private static decimal D(string value) => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
}
