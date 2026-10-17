using System.Net;
using Microsoft.EntityFrameworkCore;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.IntegrationTests.P1;
using RugsManagment.IntegrationTests.Support;
using static RugsManagment.IntegrationTests.P2.P2Http;

namespace RugsManagment.IntegrationTests.P2;

/// <summary>
/// چسب مرحلهٔ ادغام فاز ۲ (کارهایی که بین بسته‌ها مانده بود):
/// <list type="bullet">
/// <item>شمارهٔ «ورودی» از شمارندهٔ کارگاه: دومین ورودیِ یک کارگاه دیگر با ایندکس یکتای (کارگاه، شماره) ۲۳۵۰۵ نمی‌گیرد.</item>
/// <item>«به‌روزرسانی بهای تمام‌شده» (D.6): ویرایش هزینه بعد از فروش عکس فروش را عوض نمی‌کند؛ فقط همین کار صریح، و فقط
/// برای Business.</item>
/// <item>redirect فهرست فرش‌ها وقتی صفحه از آخر گذشته: همهٔ فیلترها (از جمله withoutBatch و فیلترهای فاز ۲) و همان روز.</item>
/// </list>
/// </summary>
public sealed class P2IntegrationGlueTests(RugsWebFactory factory) : IClassFixture<RugsWebFactory>
{
    [PostgresFact]
    public async Task SecondBatchOfATenant_GetsTheNextNumber_InsteadOfAUniqueViolation()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);

        await SendAsync(merchant, HttpMethod.Post, "/api/batches", new { name = "ورودی یک", description = (string?)null });
        await SendAsync(merchant, HttpMethod.Post, "/api/batches", new { name = "ورودی دو", description = (string?)null });

        await using var db = factory.CreateDbContext();
        var numbers = await db.RugBatches.AsNoTracking()
            .Where(b => b.TenantId == t.TenantId)
            .OrderBy(b => b.Number)
            .Select(b => b.Number)
            .ToListAsync();
        Assert.Equal([1, 2], numbers);
    }

    [PostgresFact]
    public async Task CostChangeAfterSale_KeepsTheSnapshot_UntilBusinessRefreshesIt()
    {
        var t = await P2Tenant.CreateAsync(factory);
        var other = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var op = P1Client.As(factory, t.Operator);
        using var stranger = P1Client.As(factory, other.Merchant);

        var rug = await CreateRugAsync(merchant, Values(
            ("rug.serial", "3778"), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m),
            ("purchase.price", new { rate = 1_000_000m, basis = (int)CostBasis.Total })));
        var id = Id(rug);

        // فرش بی‌فروش: کاری برای به‌روز کردن نیست → ۴۰۰ با پیام فارسی
        var noSale = await SendAsync(merchant, HttpMethod.Post, $"/api/sales/rug/{id}/refresh-cost", null, HttpStatusCode.BadRequest);
        Assert.False(string.IsNullOrWhiteSpace(noSale.GetProperty("message").GetString()));

        var sale = await SendAsync(merchant, HttpMethod.Put, $"/api/sales/rug/{id}", new
        {
            buyerName = "خریدار",
            buyerPhone = (string?)null,
            salePrice = 2_000_000m,
            discount = 0m,
            receivedAmount = 2_000_000m,
            paymentMethod = 0,
            soldAt = (DateTimeOffset?)null,
            reference = (string?)null,
            notes = (string?)null
        });
        Assert.Equal((1_000_000m, 1_000_000m), (Dec(sale, "totalInvestment"), Dec(sale, "actualProfit")));

        // قیمت خرید بعد از فروش بالا رفت: بهای امروز ۱٬۲۰۰٬۰۰۰، عکس فروش همان ۱٬۰۰۰٬۰۰۰
        var lines = await GetJsonAsync(merchant, $"/api/rugs/{id}/purchase-lines");
        await SendAsync(merchant, HttpMethod.Put, $"/api/rugs/{id}/purchase-lines", new
        {
            version = lines.GetProperty("version").GetUInt32(),
            lines = new object[]
            {
                new { id = (Guid?)null, categoryId = (Guid?)null, label = "قیمت خرید", basis = (int)CostBasis.Total, rate = 1_200_000m, amount = 0m, sortOrder = 0, notes = (string?)null }
            }
        });
        var costs = (await GetJsonAsync(merchant, $"/api/rugs/{id}")).GetProperty("costs");
        Assert.Equal((1_200_000m, 1_000_000m), (Dec(costs, "totalInvestment"), Dec(costs, "costAtSale")));
        Assert.Equal(1_000_000m, Dec(await GetJsonAsync(merchant, $"/api/sales/rug/{id}"), "totalInvestment"));

        // فقط Business؛ کارگاه دیگر ۴۰۴ و چیزی عوض نمی‌شود
        await SendAsync(op, HttpMethod.Post, $"/api/sales/rug/{id}/refresh-cost", null, HttpStatusCode.Forbidden);
        await SendAsync(stranger, HttpMethod.Post, $"/api/sales/rug/{id}/refresh-cost", null, HttpStatusCode.NotFound);

        var refreshed = await SendAsync(merchant, HttpMethod.Post, $"/api/sales/rug/{id}/refresh-cost", null);
        Assert.Equal((1_200_000m, 800_000m), (Dec(refreshed, "totalInvestment"), Dec(refreshed, "actualProfit")));

        await using var db = factory.CreateDbContext();
        var stored = await db.RugSales.AsNoTracking().SingleAsync(s => s.RugId == id);
        Assert.Equal(1_200_000m, stored.CostAtSale);
        Assert.NotNull(stored.CostAtSaleAt);
        Assert.Equal(0, (await db.Set<RugCostTotal>().AsNoTracking().SingleAsync(c => c.RugId == id)).Total - 1_200_000m);
    }

    [PostgresFact]
    public async Task RugList_PastTheLastPage_RedirectKeepsEveryFilter_AndTheChosenDay()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        await CreateRugAsync(merchant, Values(("rug.serial", "3778"), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m)));

        using var response = await merchant.GetAsync(
            "/Rugs?withoutBatch=true&serial=3778&location=InStock&createdFrom=2020-01-15&sortBy=Serial&page=99");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Uri.UnescapeDataString(response.Headers.Location!.OriginalString);
        Assert.Contains("withoutBatch=true", location);
        Assert.Contains("serial=3778", location);
        Assert.Contains("location=InStock", location);
        Assert.Contains("createdFrom=2020-01-15", location);
        Assert.Contains("sortBy=Serial", location);
        Assert.DoesNotContain("page=", location); // تنها صفحه = صفحهٔ ۱ (پیش‌فرض در آدرس نمی‌آید)
    }
}
