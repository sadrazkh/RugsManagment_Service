using RugsManagment.Application.DTOs.Pricing;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Tests;

/// <summary>
/// موتور هزینه — پرریسک‌ترین منطق سامانه، چون خروجی‌اش مستقیم روی پول کارگاه اثر دارد.
/// </summary>
public class CostCalculationServiceTests
{
    private readonly CostCalculationService _sut = new();

    private static Rug Rug(decimal width = 2m, decimal length = 3m) =>
        new() { WidthMeters = width, LengthMeters = length };

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
    public void PerSquareFoot_ConvertsAreaToFeet()
    {
        // ۶ م² ≈ ۶۴٫۵۸ فوت²
        var cost = _sut.ComputeBase(2m, 3m, StepPricingModel.PerSquareFoot, 1_000m, null);
        Assert.Equal(Math.Round(6m * 10.7639m * 1_000m, 2), cost);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(2, 0)]
    [InlineData(0, 0)]
    public void ZeroDimension_ProducesZeroForAreaBasedModels(decimal width, decimal length)
        => Assert.Equal(0m, _sut.ComputeBase(width, length, StepPricingModel.PerSquareMeter, 100_000m, null));

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
    public void ManualOverride_WinsOverEveryOtherSource()
    {
        var cost = _sut.CalculateStepCost(
            Rug(), StepType(rate: 100_000m),
            overrideModel: StepPricingModel.PerSquareMeter, overrideRate: 999_999m,
            manualOverride: 42_000m);

        Assert.Equal(42_000m, cost);
    }

    [Fact]
    public void StepInstanceRate_WinsOverTemplateOverrideAndDefault()
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

    [Fact]
    public void TemplateOverride_WinsOverStepTypeDefault()
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
}
