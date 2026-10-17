using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RugsManagment.Domain.Enums;
using RugsManagment.IntegrationTests.Support;

namespace RugsManagment.IntegrationTests.P1;

/// <summary>
/// «انباردار هیچ مبلغ تومانی نمی‌بیند» (A.5، L.2) و قاعدهٔ نوشتن پولی (erratum E5).
///
/// مبلغ‌های واقعی را اول بازرگان از همان فرش‌ها می‌خواند؛ بعد هیچ‌کدام — نه به‌صورت عدد JSON و نه با قالب
/// فارسی صفحه — نباید در هیچ پاسخِ انباردار پیدا شود، و هیچ کلید پولی در JSONِ او مقدار غیر null نداشته باشد.
/// نوشتن: عضو پولیِ null از انباردار نادیده گرفته می‌شود و مقدار ذخیره‌شده می‌ماند؛ عضو پولیِ پر ۴۰۳ است.
/// </summary>
public sealed class P1MoneyVisibilityTests(RugsWebFactory factory) : IClassFixture<RugsWebFactory>
{
    [PostgresFact]
    public async Task Operator_JsonResponses_CarryNoMoney()
    {
        var people = await P1Data.PeopleAsync(factory);
        var rugIds = await P1Data.DemoRugIdsAsync(factory, people.TenantId);
        Assert.NotEmpty(rugIds);

        using var merchant = P1Client.As(factory, people.Merchant);
        var money = await P1Data.BusinessMoneyValuesAsync(merchant, rugIds);
        Assert.NotEmpty(money); // بازرگان مبلغ‌ها را می‌بیند؛ وگرنه این آزمون چیزی نمی‌سنجد

        using var op = P1Client.As(factory, people.Operator);

        var urls = new List<string>
        {
            "/api/rugs", "/api/rugs?sortBy=TotalCost", "/api/rugs/samples?count=50", "/api/home/summary",
            "/api/lookups/step-types", "/api/step-types", "/api/lookups/service-providers",
            "/api/lookups/workflow-templates", "/api/workflows/templates", "/api/me"
        };
        urls.AddRange(rugIds.Select(id => $"/api/rugs/{id}"));

        foreach (var url in urls)
        {
            using var response = await op.GetAsync(url);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"GET {url} → {(int)response.StatusCode}");

            var text = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(text);

            var leaked = P1Data.NonNullMoneyKeys(doc.RootElement);
            Assert.True(leaked.Count == 0, $"GET {url}: کلید پولی برای انباردار: {string.Join(", ", leaked)}");

            var amounts = P1Data.NumbersEqualTo(doc.RootElement, money);
            Assert.True(amounts.Count == 0, $"GET {url}: مبلغ برای انباردار: {string.Join(", ", amounts)}");
        }
    }

    [PostgresFact]
    public async Task Operator_Pages_ShowNoMoney()
    {
        var people = await P1Data.PeopleAsync(factory);
        var rugIds = await P1Data.DemoRugIdsAsync(factory, people.TenantId);

        using var merchant = P1Client.As(factory, people.Merchant);
        var money = await P1Data.BusinessMoneyValuesAsync(merchant, rugIds);

        using var op = factory.CreateClientAs(people.Operator);

        var urls = new List<string> { "/", "/Dashboard", "/Rugs", "/Groups", "/More" };
        urls.AddRange(rugIds.Select(id => $"/Rugs/Details/{id}"));

        foreach (var url in urls)
        {
            using var response = await op.GetAsync(url);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"GET {url} → {(int)response.StatusCode}");

            var html = await response.Content.ReadAsStringAsync();
            AssertNoAmounts(html, money, url);

            // جزیرهٔ فروش (مبلغ فروش، دریافتی، سود) برای انباردار اصلاً رندر نمی‌شود
            Assert.DoesNotContain("data-island=\"rug-sale\"", html);
        }
    }

    [PostgresFact]
    public async Task Merchant_Pages_DoShowMoney()
    {
        var people = await P1Data.PeopleAsync(factory);
        var rugIds = await P1Data.DemoRugIdsAsync(factory, people.TenantId);

        using var merchant = P1Client.As(factory, people.Merchant);
        var money = await P1Data.BusinessMoneyValuesAsync(merchant, rugIds);

        // دست‌کم یک مبلغ از جزئیات هر فرش با قالب صفحه دیده می‌شود (قاعدهٔ پنهان‌سازی زیادی سفت نشده)
        using var client = factory.CreateClientAs(people.Merchant);
        using var response = await client.GetAsync($"/Rugs/Details/{rugIds[0]}");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-island=\"rug-sale\"", html);
        Assert.Contains(money, v => html.Contains(Web.Support.PersianFormat.Money(v), StringComparison.Ordinal));
    }

    // ── نوشتن پولی (E5) ────────────────────────────────────────────────────

    [PostgresFact]
    public async Task Operator_RugPut_WithNullMoney_KeepsStoredValues()
    {
        var people = await P1Data.PeopleAsync(factory);
        var rug = await FirstRugWithPurchaseAsync(people.TenantId);
        var purchase = await PurchaseLinesOfAsync(rug.Id);

        using var op = P1Client.As(factory, people.Operator);
        using var get = await op.GetAsync($"/api/rugs/{rug.Id}");
        using var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        var r = doc.RootElement;

        // همان چیزی که فرم قدیمیِ فرش برای انباردار می‌فرستد: مبلغ‌ها null (چون برایش null آمده بودند)
        var body = new
        {
            title = r.GetProperty("title").GetString(),
            origin = r.GetProperty("origin").GetString(),
            pattern = r.GetProperty("pattern").GetString(),
            material = r.GetProperty("material").GetString(),
            knotDensity = (int?)null,
            widthMeters = r.GetProperty("widthMeters").GetDecimal(),
            lengthMeters = r.GetProperty("lengthMeters").GetDecimal(),
            purchaseCost = (decimal?)null,
            targetSalePrice = (decimal?)null,
            status = (int?)null,
            imageUrl = (string?)null,
            notes = "ویرایش انباردار",
            metadataJson = r.TryGetProperty("metadataJson", out var meta) ? meta.GetString() : null
        };

        using var put = await op.SendJsonAsync(HttpMethod.Put, $"/api/rugs/{rug.Id}", body);
        Assert.True(put.IsSuccessStatusCode, $"PUT → {(int)put.StatusCode}: {await put.Content.ReadAsStringAsync()}");

        await using var db = factory.CreateDbContext();
        var stored = await db.Rugs.AsNoTracking().SingleAsync(x => x.Id == rug.Id);
        Assert.Equal(purchase, await PurchaseLinesOfAsync(rug.Id));
        Assert.Equal(rug.TargetSalePrice, stored.TargetSalePrice);
        Assert.Equal("ویرایش انباردار", stored.Notes);
    }

    [PostgresFact]
    public async Task Operator_MoneyWrites_Are403()
    {
        var people = await P1Data.PeopleAsync(factory);
        var rug = await FirstRugWithPurchaseAsync(people.TenantId);
        var purchase = await PurchaseLinesOfAsync(rug.Id);
        var stepId = await FirstStepIdAsync(rug.Id);

        using var op = P1Client.As(factory, people.Operator);

        // PUT فرش با قیمت خرید
        using (var put = await op.SendJsonAsync(HttpMethod.Put, $"/api/rugs/{rug.Id}", new
        {
            title = "x", widthMeters = rug.WidthMeters, lengthMeters = rug.LengthMeters, purchaseCost = 1m
        }))
            Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);

        // PUT فرش با قیمت فروش هدف
        using (var put = await op.SendJsonAsync(HttpMethod.Put, $"/api/rugs/{rug.Id}", new
        {
            title = "x", widthMeters = rug.WidthMeters, lengthMeters = rug.LengthMeters, targetSalePrice = 1m
        }))
            Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);

        // ویرایش گروهی با قیمت فروش هدف
        using (var bulk = await op.SendJsonAsync(HttpMethod.Put, "/api/rugs/bulk/fields", new
        {
            rugIds = new[] { rug.Id }, targetSalePrice = 5m
        }))
            Assert.Equal(HttpStatusCode.Forbidden, bulk.StatusCode);

        // قیمت‌گذاری مرحله: هر عضو پولی جدا
        foreach (var member in new object[]
                 {
                     new { unitRate = 1000m },
                     new { pricingModel = (int)StepPricingModel.Fixed },
                     new { manualCostOverride = 1000m },
                     new { adjustment = -100m }
                 })
        {
            using var pricing = await op.SendJsonAsync(
                HttpMethod.Put, $"/api/rugs/{rug.Id}/steps/{stepId}/pricing", member);
            Assert.True(pricing.StatusCode == HttpStatusCode.Forbidden,
                $"pricing {JsonSerializer.Serialize(member)} → {(int)pricing.StatusCode}");
        }

        // تکمیل مرحله با مبلغ
        using (var advance = await op.SendJsonAsync(
                   HttpMethod.Post, $"/api/rugs/{rug.Id}/steps/{stepId}/advance", new { manualCostOverride = 1000m }))
            Assert.Equal(HttpStatusCode.Forbidden, advance.StatusCode);

        // هیچ‌کدام چیزی ننوشت
        await using var db = factory.CreateDbContext();
        var stored = await db.Rugs.AsNoTracking().SingleAsync(x => x.Id == rug.Id);
        Assert.Equal(purchase, await PurchaseLinesOfAsync(rug.Id));
        Assert.Equal(rug.TargetSalePrice, stored.TargetSalePrice);
    }

    [PostgresFact]
    public async Task Operator_CannotUsePricingPreview()
    {
        var people = await P1Data.PeopleAsync(factory);
        using var op = P1Client.As(factory, people.Operator);

        using var preview = await op.SendJsonAsync(HttpMethod.Post, "/api/pricing/preview", new { unitRate = 1000m });
        Assert.Equal(HttpStatusCode.Forbidden, preview.StatusCode);
    }

    // ── کمکی‌ها ────────────────────────────────────────────────────────────

    private static void AssertNoAmounts(string text, IReadOnlyList<decimal> money, string url)
    {
        foreach (var value in money)
        {
            foreach (var form in P1Data.RenderedForms(value))
                Assert.False(text.Contains(form, StringComparison.Ordinal),
                    $"{url}: مبلغ {value} (به شکل «{form}») برای انباردار دیده می‌شود.");
        }
    }

    /// <summary>فرش دمویی که قیمت خرید (از فاز ۲ سطر خرید) و قیمت هدف دارد.</summary>
    private async Task<Domain.Entities.Rug> FirstRugWithPurchaseAsync(Guid tenantId)
    {
        await using var db = factory.CreateDbContext();
        return await db.Rugs.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.PurchaseLines.Any() && r.TargetSalePrice != null)
            .OrderBy(r => r.Sku)
            .FirstAsync();
    }

    /// <summary>سطرهای خرید یک فرش (مبنا، نرخ، مبلغ) به ترتیب؛ برای «هیچ مبلغی عوض نشد».</summary>
    private async Task<IReadOnlyList<(CostBasis Basis, decimal Rate, decimal Amount)>> PurchaseLinesOfAsync(Guid rugId)
    {
        await using var db = factory.CreateDbContext();
        var lines = await db.Set<Domain.Entities.RugPurchaseLine>().AsNoTracking()
            .Where(l => l.RugId == rugId)
            .OrderBy(l => l.SortOrder).ThenBy(l => l.Id)
            .Select(l => new { l.Basis, l.Rate, l.Amount })
            .ToListAsync();
        return lines.Select(l => (l.Basis, l.Rate, l.Amount)).ToList();
    }

    private async Task<Guid> FirstStepIdAsync(Guid rugId)
    {
        await using var db = factory.CreateDbContext();
        return await db.RugWorkflowSteps.AsNoTracking()
            .Where(s => s.RugId == rugId)
            .OrderBy(s => s.OrderIndex)
            .Select(s => s.Id)
            .FirstAsync();
    }
}
