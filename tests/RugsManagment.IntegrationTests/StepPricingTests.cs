using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.IntegrationTests.Support;

namespace RugsManagment.IntegrationTests;

/// <summary>
/// قیمت‌گذاری مرحله از مسیر واقعیِ سرویس (همان چیزی که پنل گردش کار می‌فرستد) روی فرش ۲×۳ (۶ m²):
/// <list type="bullet">
/// <item>ویرایشگرِ دست‌نخورده (همهٔ فیلدهای قیمت null) قیمتی را که قبلاً صریح ذخیره شده با نرخ توافقیِ
/// همان طرف خدمات بازنویسی نمی‌کند؛ مرحلهٔ بدون قیمتِ ذخیره‌شده همچنان نرخ توافقی می‌گیرد.</item>
/// <item>مبلغ دستی فقط با پرچم صریح ClearManualOverride پاک می‌شود.</item>
/// <item>انجام‌دهنده فقط با پرچم صریح ClearServiceProvider برداشته می‌شود و آن‌وقت نرخ توافقی تزریق نمی‌شود.</item>
/// </list>
/// </summary>
public sealed class StepPricingTests(TwoTenantFixture fixture) : IClassFixture<TwoTenantFixture>
{
    private const decimal Area = 6m;

    private TenantSeed A => fixture.A;

    // ── تزریق نرخ توافقی روی قیمتِ صریح ──────────────────────────────────

    [PostgresFact]
    public async Task UntouchedEditor_KeepsAnExplicitlySavedRate_WhenTheProviderIsUnchanged()
    {
        var rug = await fixture.CreateRugAsync(A);
        var stepId = rug.WorkflowSteps.Single().Id;

        // «فقط ذخیره» با نرخ ویژهٔ همین فرش (کمتر از نرخ توافقی ۱۲۰۰)
        var saved = await AsA(s => s.UpdateStepPricingAsync(A.TenantId, rug.Id, stepId,
            Pricing(A.ProviderId, StepPricingModel.PerSquareMeter, 700m, markCompleted: false)));
        Assert.Equal(700m * Area, saved.WorkflowSteps.Single().EffectiveCost);

        // باز کردن دوباره و «تکمیل» بدون دست زدن به هزینه — انجام‌دهنده همان از پیش انتخاب‌شده است
        var completed = await AsA(s => s.AdvanceStepAsync(A.TenantId, rug.Id, stepId, Untouched(A.ProviderId)));

        var step = completed.WorkflowSteps.Single();
        Assert.Equal(WorkflowStepStatus.Completed, step.Status);
        Assert.Equal(700m, step.AppliedUnitRate);
        Assert.Equal(700m * Area, step.EffectiveCost);
    }

    [PostgresFact]
    public async Task UntouchedEditor_KeepsASavedCombinedConfiguration()
    {
        var rug = await fixture.CreateRugAsync(A);
        var stepId = rug.WorkflowSteps.Single().Id;
        const string combined = """{"items":[{"model":0,"rate":500},{"model":1,"rate":100}]}""";

        await AsA(s => s.UpdateStepPricingAsync(A.TenantId, rug.Id, stepId,
            new AdvanceRugStepRequest(A.ProviderId, null, StepPricingModel.Combined, null, combined, null, null,
                MarkCompleted: false)));

        var completed = await AsA(s => s.AdvanceStepAsync(A.TenantId, rug.Id, stepId, Untouched(null)));

        var step = completed.WorkflowSteps.Single();
        Assert.Equal(nameof(StepPricingModel.Combined), step.AppliedPricingModel);
        Assert.Equal(500m + 100m * Area, step.EffectiveCost);
    }

    [PostgresFact]
    public async Task UntouchedEditor_StillAppliesTheAgreedRate_WhenNothingWasSavedOnTheStep()
    {
        var rug = await CreateRugWithProviderAsync(A.ProviderId);
        var stepId = rug.WorkflowSteps.Single().Id;

        var completed = await AsA(s => s.AdvanceStepAsync(A.TenantId, rug.Id, stepId, Untouched(null)));

        var step = completed.WorkflowSteps.Single();
        Assert.Equal(TwoTenantFixture.ProviderAgreedRate, step.AppliedUnitRate);
        Assert.Equal(TwoTenantFixture.ProviderAgreedRate * Area, step.EffectiveCost);
    }

    [PostgresFact]
    public async Task UntouchedEditor_AppliesTheNewProvidersRate_WhenTheProviderChanges()
    {
        var otherProviderId = await AddProviderWithRateAsync(900m);
        var rug = await fixture.CreateRugAsync(A);
        var stepId = rug.WorkflowSteps.Single().Id;

        await AsA(s => s.UpdateStepPricingAsync(A.TenantId, rug.Id, stepId,
            Pricing(A.ProviderId, StepPricingModel.PerSquareMeter, 700m, markCompleted: false)));

        var completed = await AsA(s => s.AdvanceStepAsync(A.TenantId, rug.Id, stepId, Untouched(otherProviderId)));

        var step = completed.WorkflowSteps.Single();
        Assert.Equal(otherProviderId, step.ServiceProviderId);
        Assert.Equal(900m * Area, step.EffectiveCost);
    }

    // ── مبلغ دستی ─────────────────────────────────────────────────────────

    [PostgresFact]
    public async Task ManualOverride_IsExposed_AndOnlyTheExplicitFlagClearsIt()
    {
        var rug = await fixture.CreateRugAsync(A);
        var stepId = rug.WorkflowSteps.Single().Id;

        var manual = await AsA(s => s.UpdateStepPricingAsync(A.TenantId, rug.Id, stepId,
            new AdvanceRugStepRequest(null, 5000m, null, null, null, null, null, MarkCompleted: false)));
        Assert.Equal(5000m, manual.WorkflowSteps.Single().ManualCostOverride);
        Assert.Equal(5000m, manual.WorkflowSteps.Single().EffectiveCost);

        // روش بدون پرچم (مثل تزریق نرخ یا تکمیل گروهی): مبلغ دستی می‌ماند
        var kept = await AsA(s => s.UpdateStepPricingAsync(A.TenantId, rug.Id, stepId,
            Pricing(null, StepPricingModel.PerSquareMeter, 400m, markCompleted: false)));
        Assert.Equal(5000m, kept.WorkflowSteps.Single().ManualCostOverride);
        Assert.Equal(5000m, kept.WorkflowSteps.Single().EffectiveCost);

        // کاربر حالت دستی را خاموش کرده و فرمول انتخاب کرده → پرچم پاک‌کردن
        var cleared = await AsA(s => s.AdvanceStepAsync(A.TenantId, rug.Id, stepId,
            Pricing(null, StepPricingModel.PerSquareMeter, 400m, markCompleted: true) with { ClearManualOverride = true }));

        var step = cleared.WorkflowSteps.Single();
        Assert.Null(step.ManualCostOverride);
        Assert.Equal(400m * Area, step.EffectiveCost);

        await using var db = fixture.CreateContext();
        Assert.Null((await db.RugWorkflowSteps.SingleAsync(s => s.Id == stepId)).ManualCostOverride);
    }

    // ── برداشتن انجام‌دهنده ───────────────────────────────────────────────

    [PostgresFact]
    public async Task ClearServiceProvider_UnassignsTheProvider_AndSkipsTheAgreedRate()
    {
        var rug = await CreateRugWithProviderAsync(A.ProviderId);
        var stepId = rug.WorkflowSteps.Single().Id;
        Assert.Equal(A.ProviderId, rug.WorkflowSteps.Single().ServiceProviderId);

        var completed = await AsA(s => s.AdvanceStepAsync(A.TenantId, rug.Id, stepId,
            Untouched(null) with { ClearServiceProvider = true }));

        var step = completed.WorkflowSteps.Single();
        Assert.Null(step.ServiceProviderId);
        Assert.Equal(TwoTenantFixture.SystemDefaultRate * Area, step.EffectiveCost);

        await using var db = fixture.CreateContext();
        Assert.False(await db.RugWorkflowSteps.AnyAsync(s => s.Id == stepId && s.ServiceProviderId != null));
    }

    [PostgresFact]
    public async Task WithoutTheFlag_ANullProviderStillMeansKeep()
    {
        var rug = await CreateRugWithProviderAsync(A.ProviderId);
        var stepId = rug.WorkflowSteps.Single().Id;

        var saved = await AsA(s => s.UpdateStepPricingAsync(A.TenantId, rug.Id, stepId,
            Untouched(null) with { MarkCompleted = false, Notes = "kept" }));

        Assert.Equal(A.ProviderId, saved.WorkflowSteps.Single().ServiceProviderId);
    }

    // ── کمکی‌ها ──────────────────────────────────────────────────────────

    /// <summary>ویرایشگرِ دست‌نخورده: همهٔ فیلدهای قیمت null.</summary>
    private static AdvanceRugStepRequest Untouched(Guid? providerId)
        => new(providerId, null, null, null, null, null, null);

    private static AdvanceRugStepRequest Pricing(Guid? providerId, StepPricingModel model, decimal rate, bool markCompleted)
        => new(providerId, null, model, rate, null, null, null, markCompleted);

    private async Task<RugDto> AsA(Func<IRugManagementService, Task<RugDto>> action)
    {
        await using var scope = fixture.As(A);
        return await action(scope.ServiceProvider.GetRequiredService<IRugManagementService>());
    }

    private async Task<RugDto> CreateRugWithProviderAsync(Guid providerId)
    {
        await using var scope = fixture.As(A);
        return await scope.ServiceProvider.GetRequiredService<IRugManagementService>().CreateAsync(
            A.TenantId, TwoTenantFixture.NewRugRequest([new(fixture.SystemStepTypeId, false, providerId)]));
    }

    private async Task<Guid> AddProviderWithRateAsync(decimal rate)
    {
        var provider = new Domain.Entities.ServiceProvider { TenantId = A.TenantId, Name = $"Provider A2 {Guid.NewGuid():N}" };
        await using var db = fixture.CreateContext();
        db.ServiceProviders.Add(provider);
        db.ServiceProviderRates.Add(new ServiceProviderRate
        {
            ServiceProviderId = provider.Id,
            ProcessStepTypeId = fixture.SystemStepTypeId,
            PricingModel = StepPricingModel.PerSquareMeter,
            UnitRate = rate
        });
        await db.SaveChangesAsync();
        return provider.Id;
    }
}
