using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Common;
using RugsManagment.Application.DTOs.Dashboard;
using RugsManagment.Application.DTOs.Providers;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.DTOs.Workflows;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Tests;

/// <summary>
/// «انباردار هیچ مبلغ تومانی نمی‌بیند» (spec A.5) و قاعدهٔ نوشتن پولی (erratum E5): عضو null همیشه مجاز
/// است و مقدار ذخیره‌شده را نگه می‌دارد؛ فقط مبلغِ پرشده از غیر Business ۴۰۳ است.
/// </summary>
public class MoneyRedactionTests
{
    internal sealed class Visibility(bool canSeeMoney) : IMoneyVisibility
    {
        public bool CanSeeMoney => canSeeMoney;
    }

    internal static readonly IMoneyVisibility Operator = new Visibility(false);
    internal static readonly IMoneyVisibility Merchant = new Visibility(true);

    internal static RugWorkflowStepDto Step() => new(
        Guid.NewGuid(), Guid.NewGuid(), "wash", "شستشو", "Wash", "droplet", 0, false, WorkflowStepStatus.Completed,
        null, null, null, null, 1_200_000m, 1_000_000m, "PerSquareMeter", 200_000m, """{"items":[]}""", null, null,
        Adjustment: 200_000m, CompletedByName: "x", ManualCostOverride: 1_200_000m);

    internal static RugDto Rug() => new(
        Guid.NewGuid(), "RUG-202609-0001", "t", "o", "p", "m", null, 2m, 3m, 6m, 5_000_000m, 9_000_000m,
        RugStatus.InProgress, null, null, null, null, null, "شستشو", 0, [Step()],
        new RugCostSummaryDto(1_200_000m, 5_000_000m, 6_200_000m, 9_000_000m, 2_800_000m), """{"bg_color":"red"}""");

    [Fact]
    public void Rug_ForOperator_HasNoMoneyAnywhere()
    {
        var redacted = Operator.Redact(Rug());

        Assert.Null(redacted.PurchaseCost);
        Assert.Null(redacted.TargetSalePrice);
        Assert.Null(redacted.Costs);
        var step = Assert.Single(redacted.WorkflowSteps);
        Assert.Null(step.EffectiveCost);
        Assert.Null(step.CalculatedCost);
        Assert.Null(step.AppliedUnitRate);
        Assert.Null(step.ManualCostOverride);
        Assert.Null(step.Adjustment);
        Assert.Null(step.PricingConfigJson);

        // کار انبار دست نمی‌خورد
        Assert.Equal(6m, redacted.AreaSquareMeters);
        Assert.Equal("شستشو", step.StepNameFa);
        Assert.Equal("""{"bg_color":"red"}""", redacted.MetadataJson);
    }

    [Fact]
    public void Rug_ForMerchant_IsTheSameInstance()
    {
        var rug = Rug();
        Assert.Same(rug, Merchant.Redact(rug));
    }

    [Fact]
    public void ListPage_ForOperator_HasNoInvestment()
    {
        var item = new RugListItemDto(Guid.NewGuid(), "S", null, null, null, 2m, 3m, 6m, RugStatus.Draft, null, null,
            null, null, null, 7_000_000m, 0, 0, DateTimeOffset.UtcNow);
        var page = new PagedResult<RugListItemDto>([item], 1, 1, 25);

        Assert.Null(Assert.Single(Operator.Redact(page).Items).TotalInvestment);
        Assert.Equal(7_000_000m, Assert.Single(Merchant.Redact(page).Items).TotalInvestment);
    }

    [Theory]
    [InlineData(RugSortBy.TotalCost, false, RugSortBy.CreatedAt)]
    [InlineData(RugSortBy.TotalCost, true, RugSortBy.TotalCost)]
    [InlineData(RugSortBy.Area, false, RugSortBy.Area)]
    public void SortByCost_FallsBackToCreatedAt_ForNonBusiness(RugSortBy requested, bool canSeeMoney, RugSortBy expected)
        => Assert.Equal(expected, new Visibility(canSeeMoney).GuardSort(new RugQuery { SortBy = requested }).SortBy);

    [Fact]
    public void Dashboard_ForOperator_HasOnlyCounts()
    {
        var stats = new DashboardStatsDto(5, 2, 1, 1, 10m, 20m, 30m, 40m, 1, 2, 50m, 60m, 70m,
            [new RecentRugDto(Guid.NewGuid(), "S", null, "Draft", null, 80m)], [new StepDistributionDto("x", 1)]);

        var redacted = Operator.Redact(stats);

        Assert.Equal((5, 2, 1, 1), (redacted.TotalRugs, redacted.InProgress, redacted.ReadyForSale, redacted.Sold));
        Assert.All(
            new[] { redacted.TotalInvestment, redacted.PipelineValue, redacted.ProfitEstimate, redacted.ReadyForSaleValue,
                redacted.ActualSalesTotal, redacted.ActualProfitTotal, redacted.OutstandingReceivable },
            value => Assert.Null(value));
        Assert.Null(Assert.Single(redacted.RecentRugs).TotalInvestment);
    }

    [Fact]
    public void StepTypes_Templates_Providers_LoseTheirRates()
    {
        var type = new ProcessStepTypeDto(Guid.NewGuid(), "wash", "شستشو", "Wash", "droplet", 1,
            StepPricingModel.PerSquareMeter, 85_000m, null);
        Assert.Null(Operator.Redact(type).DefaultUnitRate);
        Assert.Equal(85_000m, Merchant.Redact(type).DefaultUnitRate);

        var template = new WorkflowTemplateDto(Guid.NewGuid(), "t", null, true, true,
            [new WorkflowTemplateStepDto(Guid.NewGuid(), Guid.NewGuid(), "wash", "شستشو", 0, false, null, StepPricingModel.Fixed, 5m)]);
        Assert.Null(Assert.Single(Operator.Redact(template).Steps).OverrideUnitRate);

        var provider = new ServiceProviderDetailDto(Guid.NewGuid(), "p", null, null, null, null, true,
            [new ProviderRateDto(Guid.NewGuid(), Guid.NewGuid(), "شستشو", StepPricingModel.PerSquareMeter, 90_000m, null)]);
        Assert.Null(Assert.Single(Operator.Redact(provider).Rates).UnitRate);
        Assert.Equal(StepPricingModel.PerSquareMeter, Assert.Single(Operator.Redact(provider).Rates).PricingModel);
    }

    [Fact]
    public void WriteRule_NullMembersPass_ForEveryone()
    {
        Operator.EnsureMayWriteMoney(null, null);
        Operator.EnsureMayWriteStepPricing(new AdvanceRugStepRequest(null, null, null, null, null, null, "note",
            ClearManualOverride: false));
    }

    /// <summary>
    /// پیکربندی ترکیبیِ خالی یا فاصله از انباردار هم ۴۰۳ است: موتور گردش کار هر مقدار غیر null را «پاک کن»
    /// می‌خواند (یافتهٔ بازبینی فاز ۱: «pricingConfigJson»: "" از نگهبان رد می‌شد و قیمت ترکیبی را پاک می‌کرد).
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void WriteRule_BlankPricingConfig_FromOperator_Is403(string blank)
    {
        var ex = Assert.Throws<UnauthorizedAccessException>(() => Operator.EnsureMayWriteStepPricing(
            new AdvanceRugStepRequest(null, null, null, null, blank, null, null)));
        Assert.Equal(Messages.MoneyWriteForbidden.Text, ex.Message);

        // بازرگان همچنان می‌تواند پیکربندی را با رشتهٔ خالی پاک کند
        Merchant.EnsureMayWriteStepPricing(new AdvanceRugStepRequest(null, null, null, null, blank, null, null));
    }

    [Fact]
    public void WriteRule_NonNullMoney_FromOperator_Is403WithPersianMessage()
    {
        var ex = Assert.Throws<UnauthorizedAccessException>(() => Operator.EnsureMayWriteMoney(null, 5_000_000m));
        Assert.Equal(Messages.MoneyWriteForbidden.Text, ex.Message);

        Assert.Throws<UnauthorizedAccessException>(() => Operator.EnsureMayWriteStepPricing(
            new AdvanceRugStepRequest(null, null, StepPricingModel.Fixed, null, null, null, null)));
        Assert.Throws<UnauthorizedAccessException>(() => Operator.EnsureMayWriteStepPricing(
            new AdvanceRugStepRequest(null, null, null, null, null, null, null, Adjustment: -10m)));
        Assert.Throws<UnauthorizedAccessException>(() => Operator.EnsureMayWriteStepPricing(
            new AdvanceRugStepRequest(null, null, null, null, null, null, null, ClearManualOverride: true)));
        Assert.Throws<UnauthorizedAccessException>(() => Operator.EnsureMayWriteStepPricing(
            new AdvanceRugStepRequest(null, null, null, null, """{"items":[]}""", null, null)));
    }

    [Fact]
    public void WriteRule_Business_MayWriteAnything()
    {
        Merchant.EnsureMayWriteMoney(1m, 2m);
        Merchant.EnsureMayWriteStepPricing(new AdvanceRugStepRequest(null, 1m, StepPricingModel.Fixed, 2m, null, null, null));
        FullMoneyVisibility.Instance.EnsureMayWriteMoney(1m);
    }
}
