using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.IntegrationTests.P1;
using RugsManagment.IntegrationTests.Support;
using static RugsManagment.IntegrationTests.P2.P2Http;

namespace RugsManagment.IntegrationTests.P2;

/// <summary>
/// لبه‌های API فاز ۲ فرش که پذیرش اصلی (<see cref="P2RugApiTests"/>) نمی‌پوشاند:
/// <list type="bullet">
/// <item>شمارهٔ خودکار (G.4، E2): پیش از flush روی همان فرش نوشته می‌شود، پس پاسخ ثبت شماره و نسخهٔ تازه را دارد و ویرایش
/// بعدی ۴۰۹ نمی‌گیرد؛ متن کاربر نادیده گرفته می‌شود و درخواست قدیمی هم شماره می‌گیرد.</item>
/// <item>دسته با قیمت متری در رقم اعشار ۲: ۷٫۲۹ × ۵۰ = ۳۶۴٫۵۰ (همتای ۳۵۰ در رقم اعشار ۰).</item>
/// <item>مسابقهٔ شماره (L.2): دو ثبت هم‌زمانِ بازه‌های همپوشان → دقیقاً یکی موفق، بازنده ۴۰۹ با <c>nextFree</c>؛ و ویرایش
/// شماره به بازهٔ همپوشان (E1).</item>
/// <item>جداسازی کارگاه: هر مسیر تازهٔ فرش با شناسه یا شمارهٔ کارگاه دیگر ۴۰۴ است و چیزی را عوض نمی‌کند.</item>
/// <item>قالب با مرحلهٔ کارگاه دیگر (E17): هم در ثبت و هم در «اعمال قالب» ۴۰۰.</item>
/// <item>مسیرهای قدیمی (K.2 P2): PUT قدیمیِ اندازه بهای تازه را در همان پاسخ می‌دهد؛ ویرایش گروهی حساب را سالم نگه می‌دارد؛
/// رفت‌وبرگشت سطر «کل» با نرخ اعشاری نرخ را عوض نمی‌کند (LM-02).</item>
/// </list>
/// </summary>
public sealed class P2RugApiEdgeTests(RugsWebFactory factory) : IClassFixture<RugsWebFactory>
{
    // ── شمارهٔ خودکار (G.4، E2) ────────────────────────────────────────

    [PostgresFact]
    public async Task AutoSerialMode_AssignsBeforeTheFlush_IgnoresClientText_AndTheNextEditIsNotStale()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var admin = P1Client.As(factory, t.Admin);
        using var merchant = P1Client.As(factory, t.Merchant);
        await SetPreferencesAsync(admin, prefs =>
        {
            prefs["serialMode"] = (int)SerialMode.Auto;
            prefs["serialStart"] = 5000;
        });

        var single = await CreateRugAsync(merchant, Values(("rug.intakeLength", 2m), ("rug.intakeWidth", 3m)));
        Assert.Equal("5000", single.GetProperty("serialNo").GetString());

        // متن کاربر در حالت خودکار نادیده گرفته می‌شود؛ دسته بازهٔ «از تا از + تعداد − ۱» می‌گیرد
        var lot = await CreateRugAsync(merchant, Values(
            ("rug.serial", "9999"), ("rug.qty", 3), ("rug.intakeLength", 0.40m), ("rug.intakeWidth", 0.40m)));
        Assert.Equal("5001 - 5003", lot.GetProperty("serialNo").GetString());
        Assert.Equal(3, lot.GetProperty("quantity").GetInt32());

        // نسخهٔ پاسخ ثبت همان نسخهٔ دیتابیس است (شماره پیش از flush نوشته شد، نه با SQL بعد از آن)
        var edited = await SendAsync(merchant, HttpMethod.Put, $"/api/rugs/{Id(lot)}",
            new { values = Values(("rug.title", "گبه")), version = Version(lot) });
        Assert.Equal("5001 - 5003", edited.GetProperty("serialNo").GetString());
        await using (var db = factory.CreateDbContext())
        {
            var stored = await db.Rugs.AsNoTracking().SingleAsync(r => r.Id == Id(lot));
            Assert.Equal(("5001 - 5003", 5001, 5003), (stored.SerialNo, stored.SerialFrom, stored.SerialTo));
        }

        // درخواست شکل قدیمی (بی values) هم در حالت خودکار شماره می‌گیرد
        var legacy = await SendAsync(merchant, HttpMethod.Post, "/api/rugs",
            new { title = "قدیمی", widthMeters = 1.5m, lengthMeters = 2m });
        Assert.Equal("5004", legacy.GetProperty("serialNo").GetString());

        var next = await GetJsonAsync(merchant, "/api/rugs/next-serial");
        Assert.Equal(5005, next.GetProperty("next").GetInt32());
    }

    // ── رقم اعشار ۲ (P2 acceptance #3، G.4) ─────────────────────────────

    [PostgresFact]
    public async Task Lot_AtMoneyScaleTwo_PerM2PurchaseIs364_50()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var admin = P1Client.As(factory, t.Admin);
        using var merchant = P1Client.As(factory, t.Merchant);
        // پیش از هر مبلغی رقم اعشار آزاد است
        await SetPreferencesAsync(admin, prefs => prefs["moneyScale"] = 2);

        var rug = await CreateRugAsync(merchant, Values(
            ("rug.serial", "3911 - 3960"), ("rug.qty", 50), ("rug.intakeLength", 0.40m), ("rug.intakeWidth", 0.40m),
            ("purchase.price", new { rate = 45.55m, basis = (int)CostBasis.PerM2 })));

        // ROUND(45.55 × 0.16، 2) = 7.29 برای هر تخته → ۳۶۴٫۵۰
        var line = Assert.Single(rug.GetProperty("purchaseLines").EnumerateArray());
        Assert.Equal(364.50m, Dec(line, "amount"));
        var costs = rug.GetProperty("costs");
        Assert.Equal(364.50m, Dec(costs, "totalInvestment"));
        Assert.Equal(45.56m, Dec(costs, "costPerM2"));   // ROUND(364.50 ÷ 8.00، 2)
        Assert.Equal(7.29m, Dec(costs, "costPerPiece")); // ROUND(364.50 ÷ 50، 2)
    }

    // ── مسابقهٔ شماره (L.2) و ویرایش شماره (E1) ────────────────────────

    [PostgresFact]
    public async Task TwoConcurrentOverlappingCreates_ExactlyOneWins_AndTheLoserGetsNextFree()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var first = P1Client.As(factory, t.Merchant);
        using var second = P1Client.As(factory, t.Admin);

        // توکن ضد CSRF هر دو کلاینت پیش از مسابقه گرفته می‌شود تا دو درخواست واقعاً هم‌زمان برسند
        await CreateRugAsync(first, Values(("rug.intakeLength", 1m), ("rug.intakeWidth", 1m)));
        await CreateRugAsync(second, Values(("rug.intakeLength", 1m), ("rug.intakeWidth", 1m)));

        var responses = await Task.WhenAll(
            first.SendJsonAsync(HttpMethod.Post, "/api/rugs", new
            {
                values = Values(("rug.serial", "7000 - 7010"), ("rug.qty", 11), ("rug.intakeLength", 1m), ("rug.intakeWidth", 1m))
            }),
            second.SendJsonAsync(HttpMethod.Post, "/api/rugs", new
            {
                values = Values(("rug.serial", "7005 - 7020"), ("rug.qty", 16), ("rug.intakeLength", 1m), ("rug.intakeWidth", 1m))
            }));

        try
        {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            var loser = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
            var body = await JsonAsync(loser, HttpStatusCode.Conflict);
            Assert.Equal("rug.serial", body.GetProperty("field").GetString());
            var nextFree = body.GetProperty("nextFree").GetInt32();
            Assert.True(nextFree is 7011 or 7021, $"nextFree = {nextFree}");
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }

        await using var db = factory.CreateDbContext();
        Assert.Equal(1, await db.Rugs.AsNoTracking().CountAsync(r => r.TenantId == t.TenantId && r.SerialFrom >= 7000));
    }

    [PostgresFact]
    public async Task SerialEdit_ToAnOverlappingRange_Is409_AndTheRugKeepsItsSerial()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        await CreateRugAsync(merchant, Values(
            ("rug.serial", "3911 - 3960"), ("rug.qty", 50), ("rug.intakeLength", 0.40m), ("rug.intakeWidth", 0.40m)));
        var rug = await CreateRugAsync(merchant, Values(("rug.serial", "3778"), ("rug.intakeLength", 2.08m), ("rug.intakeWidth", 2.10m)));

        var conflict = await SendAsync(merchant, HttpMethod.Put, $"/api/rugs/{Id(rug)}",
            new { values = Values(("rug.serial", "3925")), version = Version(rug) }, HttpStatusCode.Conflict);
        Assert.Equal("rug.serial", conflict.GetProperty("field").GetString());
        Assert.Equal(3961, conflict.GetProperty("nextFree").GetInt32());

        var after = await GetJsonAsync(merchant, $"/api/rugs/{Id(rug)}");
        Assert.Equal("3778", after.GetProperty("serialNo").GetString());
        Assert.Equal(Version(rug), Version(after));

        // شمارهٔ آزاد پذیرفته می‌شود و نسخه جلو می‌رود
        var moved = await SendAsync(merchant, HttpMethod.Put, $"/api/rugs/{Id(rug)}",
            new { values = Values(("rug.serial", "۳۷۷۹")), version = Version(rug) });
        Assert.Equal("3779", moved.GetProperty("serialNo").GetString());
        Assert.NotEqual(Version(rug), Version(moved));
    }

    // ── جداسازی کارگاه ─────────────────────────────────────────────────

    [PostgresFact]
    public async Task EveryNewRugPath_WithAnotherTenantsRug_Is404_AndChangesNothing()
    {
        var a = await P2Tenant.CreateAsync(factory);
        var b = await P2Tenant.CreateAsync(factory);
        using var merchantA = P1Client.As(factory, a.Merchant);
        using var merchantB = P1Client.As(factory, b.Merchant);

        var rug = await CreateRugAsync(merchantA, Values(
            ("rug.serial", "3911 - 3960"), ("rug.qty", 50), ("rug.intakeLength", 0.40m), ("rug.intakeWidth", 0.40m),
            ("purchase.price", new { rate = 300_000m, basis = (int)CostBasis.PerPiece })));
        var id = Id(rug);
        var version = Version(rug);

        await GetJsonAsync(merchantB, $"/api/rugs/{id}", HttpStatusCode.NotFound);
        await GetJsonAsync(merchantB, "/api/rugs/by-serial/3925", HttpStatusCode.NotFound);
        await GetJsonAsync(merchantB, $"/api/rugs/{id}/purchase-lines", HttpStatusCode.NotFound);
        await GetJsonAsync(merchantB, $"/api/rugs/{id}/cost-breakdown", HttpStatusCode.NotFound);
        await SendAsync(merchantB, HttpMethod.Put, $"/api/rugs/{id}",
            new { values = Values(("rug.title", "x")), version }, HttpStatusCode.NotFound);
        await SendAsync(merchantB, HttpMethod.Put, $"/api/rugs/{id}/final-size",
            new { sameAsIntake = true, version }, HttpStatusCode.NotFound);
        await SendAsync(merchantB, HttpMethod.Post, $"/api/rugs/{id}/split",
            new { quantity = 10, version }, HttpStatusCode.NotFound);
        await SendAsync(merchantB, HttpMethod.Put, $"/api/rugs/{id}/purchase-lines",
            new { version, lines = Array.Empty<object>() }, HttpStatusCode.NotFound);

        // فرش کارگاه A دست‌نخورده: همان نسخه، همان دسته، همان سطر خرید
        var after = await GetJsonAsync(merchantA, $"/api/rugs/{id}");
        Assert.Equal(version, Version(after));
        Assert.Equal(50, after.GetProperty("quantity").GetInt32());
        Assert.Equal(JsonValueKind.Null, after.GetProperty("finalLength").ValueKind);
        Assert.Single(after.GetProperty("purchaseLines").EnumerateArray());
        Assert.Equal(15_000_000m, Dec(after, "purchaseCost"));

        // کنترل مثبت: خودِ کارگاه A با همان شماره پیدایش می‌کند
        var hit = await GetJsonAsync(merchantA, "/api/rugs/by-serial/3925");
        Assert.Equal(id, hit.GetProperty("rug").GetProperty("id").GetGuid());
    }

    // ── قالب با مرحلهٔ کارگاه دیگر (E17) ──────────────────────────────

    [PostgresFact]
    public async Task TemplateWithAnotherTenantsStep_IsRefused_OnCreateAndOnApply()
    {
        var a = await P2Tenant.CreateAsync(factory);
        var b = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, a.Merchant);

        var ownStep = await StepTypeAsync(a.TenantId);
        var foreignStep = await StepTypeAsync(b.TenantId);
        var leaky = await TemplateAsync(a.TenantId, foreignStep);
        var clean = await TemplateAsync(a.TenantId, ownStep);

        var refused = await SendAsync(merchant, HttpMethod.Post, "/api/rugs", new
        {
            values = Values(("rug.serial", "3778"), ("rug.intakeLength", 2.08m), ("rug.intakeWidth", 2.10m)),
            workflowTemplateId = leaky
        }, HttpStatusCode.BadRequest);
        Assert.False(string.IsNullOrWhiteSpace(refused.GetProperty("message").GetString()));

        // چیزی ساخته نشد (نه فرش، نه شمارهٔ ۳۷۷۸)
        await GetJsonAsync(merchant, "/api/rugs/by-serial/3778", HttpStatusCode.NotFound);

        // کنترل مثبت: قالبِ مرحلهٔ خودِ کارگاه کار می‌کند
        var rug = await SendAsync(merchant, HttpMethod.Post, "/api/rugs", new
        {
            values = Values(("rug.serial", "3778"), ("rug.intakeLength", 2.08m), ("rug.intakeWidth", 2.10m)),
            workflowTemplateId = clean
        });
        Assert.Single(rug.GetProperty("workflowSteps").EnumerateArray());

        var bare = await CreateRugAsync(merchant, Values(("rug.serial", "3779"), ("rug.intakeLength", 1m), ("rug.intakeWidth", 1m)));
        await SendAsync(merchant, HttpMethod.Post, $"/api/rugs/{Id(bare)}/workflow/apply-template",
            new { templateId = leaky, skippedOptionalStepIds = (Guid[]?)null }, HttpStatusCode.BadRequest);

        await using var db = factory.CreateDbContext();
        Assert.False(await db.RugWorkflowSteps.AsNoTracking().AnyAsync(s => s.RugId == Id(bare)));
    }

    // ── مسیرهای قدیمی (K.2 P2) ─────────────────────────────────────────

    [PostgresFact]
    public async Task LegacyPutOfTheSize_ReturnsTheFreshTotal_AndBulkFieldsKeepTheLedgerHealthy()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var admin = P1Client.As(factory, t.Admin);
        var rug = await CreateRugAsync(merchant, Values(
            ("rug.serial", "3778"), ("rug.title", "گبه"), ("rug.intakeLength", 2.08m), ("rug.intakeWidth", 2.10m),
            ("purchase.price", new { rate = 9_000_000m, basis = (int)CostBasis.PerM2 })));
        var id = Id(rug);
        Assert.Equal(39_330_000m, Dec(rug.GetProperty("costs"), "totalInvestment"));

        // فرم قدیمی: طول/عرض تازه با همان قیمت خریدی که GET داده بود (برابر جمع سطرها → نادیده)
        var updated = await SendAsync(merchant, HttpMethod.Put, $"/api/rugs/{id}", new
        {
            title = "گبه",
            widthMeters = 2.00m,
            lengthMeters = 3.00m,
            purchaseCost = 39_330_000m,
            version = Version(rug)
        });
        Assert.Equal(54_000_000m, Dec(updated.GetProperty("costs"), "totalInvestment"));
        Assert.Equal((3.00m, 2.00m), (Dec(updated, "intakeLength"), Dec(updated, "intakeWidth")));
        await using (var db = factory.CreateDbContext())
        {
            Assert.Equal(54_000_000m, (await db.Set<RugCostTotal>().AsNoTracking().SingleAsync(x => x.RugId == id)).Total);
            var line = await db.Set<RugPurchaseLine>().AsNoTracking().SingleAsync(l => l.RugId == id);
            Assert.Equal((CostBasis.PerM2, 9_000_000m, 54_000_000m), (line.Basis, line.Rate, line.Amount));
        }

        // ویرایش گروهی (نقشه و ورودی): از پاس می‌گذرد و حساب‌ها سالم می‌مانند
        var batchId = await BatchAsync(t.TenantId);
        var bulk = await SendAsync(merchant, HttpMethod.Put, "/api/rugs/bulk/fields", new
        {
            rugIds = new[] { id },
            pattern = "لچک ترنج",
            batchId
        });
        Assert.Equal(1, bulk.GetProperty("successCount").GetInt32());

        var after = await GetJsonAsync(merchant, $"/api/rugs/{id}");
        Assert.Equal(batchId, after.GetProperty("batchId").GetGuid());
        Assert.Equal(54_000_000m, Dec(after.GetProperty("costs"), "totalInvestment"));
        var report = await GetJsonAsync(admin, "/api/diagnostics/cost-consistency");
        Assert.Equal(0, report.GetProperty("mismatches").GetArrayLength());
    }

    [PostgresFact]
    public async Task LegacyRoundTrip_OfAFractionalTotalLine_KeepsItsRate()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        var rug = await CreateRugAsync(merchant, Values(
            ("rug.serial", "3778"), ("rug.intakeLength", 2.08m), ("rug.intakeWidth", 2.10m),
            ("purchase.price", new { rate = 1000.5m, basis = (int)CostBasis.Total })));
        var id = Id(rug);

        // رقم اعشار ۰: ROUND(1000.5) = 1001 (نیمه دور از صفر)
        var get = await GetJsonAsync(merchant, $"/api/rugs/{id}");
        Assert.Equal(1001m, Dec(get, "purchaseCost"));

        await SendAsync(merchant, HttpMethod.Put, $"/api/rugs/{id}", new
        {
            title = (string?)null,
            widthMeters = Dec(get, "widthMeters"),
            lengthMeters = Dec(get, "lengthMeters"),
            purchaseCost = Dec(get, "purchaseCost"),
            version = Version(get)
        });

        await using var db = factory.CreateDbContext();
        var line = await db.Set<RugPurchaseLine>().AsNoTracking().SingleAsync(l => l.RugId == id);
        Assert.Equal((1000.5m, 1001m), (line.Rate, line.Amount));
    }

    // ═════════════════════════════════════════════════════════════════════
    // کمکی‌ها
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>ترجیحات کارگاه را می‌خواند، عوض می‌کند و با همان نسخه برمی‌گرداند (Configure).</summary>
    private static async Task SetPreferencesAsync(P1Client admin, Action<JsonObject> change)
    {
        var prefs = await GetJsonAsync(admin, "/api/settings/preferences");
        var body = JsonNode.Parse(prefs.GetRawText())!.AsObject();
        change(body);
        using var put = await admin.SendRawJsonAsync(HttpMethod.Put, "/api/settings/preferences", body.ToJsonString());
        await JsonAsync(put, put.StatusCode == HttpStatusCode.NoContent ? HttpStatusCode.NoContent : HttpStatusCode.OK);
    }

    /// <summary>نوع مرحلهٔ اختصاصیِ یک کارگاه، مستقیم در دیتابیس.</summary>
    private async Task<Guid> StepTypeAsync(Guid tenantId)
    {
        var step = new ProcessStepType
        {
            TenantId = tenantId,
            Code = $"it-{Guid.NewGuid():N}"[..20],
            NameFa = "شستشوی آزمون",
            NameEn = "IT washing",
            DefaultPricingModel = StepPricingModel.Fixed,
            DefaultUnitRate = 0m
        };
        await using var db = factory.CreateDbContext();
        db.ProcessStepTypes.Add(step);
        await db.SaveChangesAsync();
        return step.Id;
    }

    /// <summary>
    /// قالبی در کارگاه <paramref name="tenantId"/> با یک مرحله — مستقیم در دیتابیس، چون API قالب از P0b به بعد مرحلهٔ
    /// کارگاه دیگر را نمی‌پذیرد؛ این همان قالب‌های قدیمیِ پیش از آن رفع است (E17).
    /// </summary>
    private async Task<Guid> TemplateAsync(Guid tenantId, Guid stepTypeId)
    {
        var template = new WorkflowTemplate { TenantId = tenantId, Name = $"قالب آزمون {Guid.NewGuid():N}"[..20] };
        template.Steps.Add(new WorkflowTemplateStep { ProcessStepTypeId = stepTypeId, OrderIndex = 0 });
        await using var db = factory.CreateDbContext();
        db.WorkflowTemplates.Add(template);
        await db.SaveChangesAsync();
        return template.Id;
    }

    /// <summary>
    /// یک «ورودی» در کارگاه، مستقیم در دیتابیس با شمارهٔ شمارنده‌دار (ساخت ورودی از سرویس قدیمی هنوز شماره نمی‌گیرد).
    /// </summary>
    private async Task<Guid> BatchAsync(Guid tenantId)
    {
        await using var db = factory.CreateDbContext();
        var number = await db.Set<RugBatch>().IgnoreQueryFilters().Where(x => x.TenantId == tenantId)
            .Select(x => (int?)x.Number).MaxAsync() ?? 0;
        var batch = new RugBatch { TenantId = tenantId, Number = number + 1, Name = "ورودی آزمون", ReceivedAt = DateTimeOffset.UtcNow };
        db.Set<RugBatch>().Add(batch);
        await db.SaveChangesAsync();
        return batch.Id;
    }
}
