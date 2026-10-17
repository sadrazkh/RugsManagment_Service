using System.Net;
using Microsoft.EntityFrameworkCore;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.IntegrationTests.P1;
using RugsManagment.IntegrationTests.P2;
using RugsManagment.IntegrationTests.Support;
using static RugsManagment.IntegrationTests.P2.P2Http;

namespace RugsManagment.IntegrationTests.P2Fix;

/// <summary>
/// یافته‌های تأییدشدهٔ بازبینی فاز ۲ از راه HTTP (spec A.5، E5، G.6 #5، L.2): انباردار با کلید پولیِ فاصله‌دار یا عضوهای پولی
/// قدیمی کنار <c>values</c> ۴۰۳ می‌گیرد و هیچ مبلغی عوض نمی‌شود؛ مبلغ سطر خریدی که از ستون بیرون می‌زند ۴۰۰ روی همان خانه
/// است، نه ۵۰۰؛ و پیش‌نمایش هزینهٔ مرحله تعداد تخته‌ها را می‌گیرد.
/// </summary>
public sealed class P2ReviewFixApiTests(RugsWebFactory factory) : IClassFixture<RugsWebFactory>
{
    // ── #11 (high): کلید پولی با فاصله/تب ──────────────────────────────────

    [PostgresFact]
    public async Task Operator_PaddedMoneyKeys_Are403_AndChangeNoMoney()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var op = P1Client.As(factory, t.Operator);
        var rug = await CreateRugAsync(merchant, Values(
            ("rug.intakeLength", 2.08m), ("rug.intakeWidth", 2.10m), ("rug.targetSalePrice", 5_000_000m),
            ("purchase.price", new { rate = 9_000_000m, basis = (int)CostBasis.Total })));
        var id = Id(rug);

        await SendAsync(op, HttpMethod.Put, $"/api/rugs/{id}",
            new { values = Values((" rug.targetSalePrice", 777m)), version = Version(rug) }, HttpStatusCode.Forbidden);
        await SendAsync(op, HttpMethod.Put, $"/api/rugs/{id}",
            new { values = Values((" purchase.price", null)), version = Version(rug) }, HttpStatusCode.Forbidden);
        await SendAsync(op, HttpMethod.Post, "/api/rugs",
            new { values = Values(("rug.intakeLength", 1m), ("rug.intakeWidth", 1m), ("\tpurchase.price", new { rate = 1234m, basis = 0 })) },
            HttpStatusCode.Forbidden);

        await using var db = factory.CreateDbContext();
        var stored = await db.Rugs.AsNoTracking().SingleAsync(r => r.Id == id);
        Assert.Equal(5_000_000m, stored.TargetSalePrice);
        var line = Assert.Single(await db.Set<RugPurchaseLine>().AsNoTracking().Where(l => l.TenantId == t.TenantId).ToListAsync());
        Assert.Equal(9_000_000m, line.Rate);
    }

    // ── #12: عضوهای پولی قدیمی کنار values ───────────────────────────────

    [PostgresFact]
    public async Task Operator_LegacyMoneyMembers_BesideValues_Are403_WhileNullsStayAllowed()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var op = P1Client.As(factory, t.Operator);
        var rug = await CreateRugAsync(merchant, Values(("rug.intakeLength", 2m), ("rug.intakeWidth", 3m)));
        var id = Id(rug);

        await SendAsync(op, HttpMethod.Put, $"/api/rugs/{id}",
            new { values = Values(("rug.title", "x")), version = Version(rug), purchaseCost = 5m, targetSalePrice = 6m },
            HttpStatusCode.Forbidden);
        await SendAsync(op, HttpMethod.Put, $"/api/rugs/{id}",
            new { values = Values(("rug.title", "x")), version = Version(rug), targetSalePrice = 6m },
            HttpStatusCode.Forbidden);
        await SendAsync(op, HttpMethod.Post, "/api/rugs",
            new { values = Values(("rug.intakeLength", 1m), ("rug.intakeWidth", 1m)), purchaseCost = 5m },
            HttpStatusCode.Forbidden);

        // null همان «دست نزن» است (E5) و پذیرفته می‌شود
        var edited = await SendAsync(op, HttpMethod.Put, $"/api/rugs/{id}",
            new { values = Values(("rug.title", "گبه")), version = Version(rug), purchaseCost = (decimal?)null, targetSalePrice = (decimal?)null });
        Assert.Equal("گبه", edited.GetProperty("title").GetString());
    }

    // ── #13: مبلغ بیرون از ستون ───────────────────────────────────────────

    [PostgresFact]
    public async Task PurchaseAmount_OverflowingTheColumn_Is400OnTheCell()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);

        var error = await CreateRugAsync(merchant, Values(
            ("rug.intakeLength", 30m), ("rug.intakeWidth", 30m),
            ("purchase.price", new { rate = 99_999_999_999_999m, basis = (int)CostBasis.PerM2 })), HttpStatusCode.BadRequest);
        Assert.True(error.GetProperty("errors").TryGetProperty("purchase.price", out _), error.ToString());

        var lot = await CreateRugAsync(merchant, Values(
            ("rug.serial", "1 - 1000"), ("rug.qty", 1000), ("rug.intakeLength", 1m), ("rug.intakeWidth", 1m),
            ("purchase.price", new { rate = 10_000_000_000_000m, basis = (int)CostBasis.PerPiece })), HttpStatusCode.BadRequest);
        Assert.True(lot.GetProperty("errors").TryGetProperty("purchase.price", out _), lot.ToString());
    }

    // ── #3: پیش‌نمایش هزینهٔ مرحله روی دسته ───────────────────────────────

    [PostgresFact]
    public async Task PricingPreview_TakesTheQuantity()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);

        var preview = await SendAsync(merchant, HttpMethod.Post, "/api/pricing/preview", new
        {
            widthMeters = 0.40m, lengthMeters = 0.40m, model = (int)StepPricingModel.PerSquareMeter, unitRate = 85_000m,
            combinedJson = (string?)null, manualCost = (decimal?)null, adjustment = -10_000m, quantity = 50
        });

        Assert.Equal(680_000m, Dec(preview, "base"));
        Assert.Equal(670_000m, Dec(preview, "total"));

        // بی تعداد همان رفتار پیشین (یک تخته)
        var single = await SendAsync(merchant, HttpMethod.Post, "/api/pricing/preview", new
        {
            widthMeters = 0.40m, lengthMeters = 0.40m, model = (int)StepPricingModel.PerSquareMeter, unitRate = 85_000m
        });
        Assert.Equal(13_600m, Dec(single, "total"));
    }
}
