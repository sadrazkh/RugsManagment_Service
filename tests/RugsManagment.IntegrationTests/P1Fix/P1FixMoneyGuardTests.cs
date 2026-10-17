using System.Net;
using Microsoft.EntityFrameworkCore;
using RugsManagment.Domain.Enums;
using RugsManagment.IntegrationTests.P1;
using RugsManagment.IntegrationTests.Support;

namespace RugsManagment.IntegrationTests.P1Fix;

/// <summary>
/// یافتهٔ بازبینی فاز ۱: انباردار با <c>{"pricingConfigJson":""}</c> از نگهبان پول (E5) رد می‌شد. موتور گردش کار هر
/// پیکربندیِ غیر null، حتی خالی، را «پاک کن» می‌خواند؛ پس قیمت ترکیبیِ مرحله پاک و بهای مرحله، سرمایهٔ فرش و حساب طرف
/// خدمات عوض می‌شد. حالا هر سه مسیر (قیمت‌گذاری، تکمیل و تکمیل گروهی) ۴۰۳ می‌دهند و چیزی ذخیره نمی‌شود.
/// </summary>
public sealed class P1FixMoneyGuardTests(RugsWebFactory factory) : IClassFixture<RugsWebFactory>
{
    /// <summary>ترکیبی: ۱۰۰٬۰۰۰ هر متر + ۵۰٬۰۰۰ ثابت.</summary>
    private const string Combined = """{"items":[{"model":1,"rate":100000},{"model":0,"rate":50000}]}""";

    [PostgresTheory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Operator_BlankPricingConfig_Is403_OnPricingAdvanceAndBulkAdvance(string blank)
    {
        var people = await P1Data.PeopleAsync(factory);
        var (rugId, stepId) = await CurrentStepAsync(people.TenantId);

        // بازرگان قیمت ترکیبی را روی مرحلهٔ جاری می‌گذارد
        using (var merchant = P1Client.As(factory, people.Merchant))
        using (var priced = await merchant.SendJsonAsync(HttpMethod.Put, $"/api/rugs/{rugId}/steps/{stepId}/pricing", new
               {
                   pricingModel = (int)StepPricingModel.Combined,
                   pricingConfigJson = Combined,
                   markCompleted = false
               }))
        {
            Assert.True(priced.IsSuccessStatusCode, $"PUT pricing (merchant) → {(int)priced.StatusCode}: {await priced.Content.ReadAsStringAsync()}");
        }

        var before = await StepAsync(stepId);
        Assert.Equal(Combined, before.PricingConfigJson);
        Assert.NotNull(before.CalculatedCost);

        using var op = P1Client.As(factory, people.Operator);

        using (var pricing = await op.SendJsonAsync(HttpMethod.Put, $"/api/rugs/{rugId}/steps/{stepId}/pricing",
                   new { pricingConfigJson = blank }))
            Assert.Equal(HttpStatusCode.Forbidden, pricing.StatusCode);

        using (var advance = await op.SendJsonAsync(HttpMethod.Post, $"/api/rugs/{rugId}/steps/{stepId}/advance",
                   new { pricingConfigJson = blank, markCompleted = true }))
            Assert.Equal(HttpStatusCode.Forbidden, advance.StatusCode);

        using (var bulk = await op.SendJsonAsync(HttpMethod.Post, "/api/rugs/bulk/advance",
                   new { rugIds = new[] { rugId }, step = new { pricingConfigJson = blank, markCompleted = true } }))
            Assert.Equal(HttpStatusCode.Forbidden, bulk.StatusCode);

        // هیچ‌کدام چیزی ننوشت: پیکربندی، بها و وضعیت مرحله همان است
        var after = await StepAsync(stepId);
        Assert.Equal(before.PricingConfigJson, after.PricingConfigJson);
        Assert.Equal(before.CalculatedCost, after.CalculatedCost);
        Assert.Equal(before.AppliedPricingModel, after.AppliedPricingModel);
        Assert.Equal(before.Status, after.Status);
    }

    [PostgresFact]
    public async Task Operator_CompletionWithoutMoney_StillWorks()
    {
        // رفتار درست انباردار عوض نشده: تکمیل بی‌مبلغ (همان چیزی که پنل برای او می‌فرستد) ۲۰۰ است
        var people = await P1Data.PeopleAsync(factory);
        var (rugId, stepId) = await CurrentStepAsync(people.TenantId);

        using var op = P1Client.As(factory, people.Operator);
        using var advance = await op.SendJsonAsync(HttpMethod.Post, $"/api/rugs/{rugId}/steps/{stepId}/advance", new
        {
            serviceProviderId = (Guid?)null,
            manualCostOverride = (decimal?)null,
            pricingModel = (int?)null,
            unitRate = (decimal?)null,
            pricingConfigJson = (string?)null,
            fieldValuesJson = (string?)null,
            notes = (string?)null,
            markCompleted = true
        });
        Assert.True(advance.IsSuccessStatusCode, $"advance (operator) → {(int)advance.StatusCode}: {await advance.Content.ReadAsStringAsync()}");
        Assert.Equal(WorkflowStepStatus.Completed, (await StepAsync(stepId)).Status);
    }

    // ── کمکی‌ها ────────────────────────────────────────────────────────────

    /// <summary>
    /// یک فرش دمو با مرحلهٔ در حال انجام که هنوز هیچ آزمونی به آن دست نزده (هر آزمون فرش خودش را برمی‌دارد).
    /// </summary>
    private async Task<(Guid RugId, Guid StepId)> CurrentStepAsync(Guid tenantId)
    {
        await using var db = factory.CreateDbContext();
        var candidates = await db.RugWorkflowSteps.AsNoTracking()
            .Where(s => s.Rug.TenantId == tenantId && s.Status == WorkflowStepStatus.InProgress
                        && s.PricingConfigJson == null && s.Rug.Sale == null)
            .OrderBy(s => s.Rug.Sku)
            .Select(s => new { s.RugId, s.Id })
            .ToListAsync();

        lock (Taken)
        {
            foreach (var c in candidates)
            {
                if (Taken.Add(c.Id))
                    return (c.RugId, c.Id);
            }
        }

        throw new InvalidOperationException("فرش دموی آزادی با مرحلهٔ در حال انجام نماند.");
    }

    private static readonly HashSet<Guid> Taken = [];

    private async Task<Domain.Entities.RugWorkflowStep> StepAsync(Guid stepId)
    {
        await using var db = factory.CreateDbContext();
        return await db.RugWorkflowSteps.AsNoTracking().SingleAsync(s => s.Id == stepId);
    }
}
