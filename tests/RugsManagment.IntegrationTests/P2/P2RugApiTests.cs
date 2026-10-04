using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.IntegrationTests.P1;
using RugsManagment.IntegrationTests.Support;
using static RugsManagment.IntegrationTests.P2.P2Http;

namespace RugsManagment.IntegrationTests.P2;

/// <summary>
/// پذیرش API فاز ۲ فرش (spec K.2 «P2 Acceptance»، L.2): دستهٔ «3911 - 3960» × ۵۰ با ۸٫۰۰ متر و قیمت متری ۴۵٫۵۵ →
/// ۳۵۰، همپوشانی شماره ۴۰۹ با <c>field: rug.serial</c>، شمارهٔ بعدی ۴۰۰۴، «یک درخواست، عددهای تازه»، رفت‌وبرگشت فرم
/// قدیمی (LM-02)، دو PUT سطرهای خرید با یک نسخه → دقیقاً یک ۴۰۹، اندازهٔ نهایی، برش دسته، پیشنهاد مقدار، کاتالوگ فیلدها،
/// سلامت حساب‌ها و پنهان بودن پول برای انباردار.
/// </summary>
public sealed class P2RugApiTests(RugsWebFactory factory) : IClassFixture<RugsWebFactory>
{
    private static readonly Dictionary<string, object?> LotB = Values(
        ("rug.serial", "3911 - 3960"), ("rug.qty", 50), ("rug.title", "گبه"), ("rug.origin", "گبه"),
        ("rug.intakeLength", 0.40m), ("rug.intakeWidth", 0.40m));

    // ── دسته، متراژ، قیمت متری (P2 acceptance #3) ───────────────────────

    [PostgresFact]
    public async Task Lot_HasEightSquareMeters_AndPerM2PurchaseRoundsPerPiece()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);

        var values = new Dictionary<string, object?>(LotB)
        {
            ["purchase.price"] = new { rate = 45.55m, basis = (int)CostBasis.PerM2 }
        };
        var rug = await CreateRugAsync(merchant, values);

        Assert.Equal("3911 - 3960", rug.GetProperty("serialNo").GetString());
        Assert.Equal(50, rug.GetProperty("quantity").GetInt32());
        Assert.Equal(0.16m, Dec(rug, "areaSquareMeters"));
        Assert.Equal(8.00m, Dec(rug, "totalAreaM2"));

        // ROUND(45.55 × 0.16, 0) = 7 برای هر تخته → ۳۵۰ (نه ۳۶۴)
        var line = Assert.Single(rug.GetProperty("purchaseLines").EnumerateArray());
        Assert.Equal(350m, Dec(line, "amount"));
        Assert.Equal(350m, Dec(rug, "purchaseCost"));
        Assert.Equal(350m, Dec(rug.GetProperty("costs"), "totalInvestment"));
        Assert.True(Version(rug) > 0);
    }

    // ── شماره (P2 acceptance #4) ───────────────────────────────────────

    [PostgresFact]
    public async Task OverlappingSerial_Is409OnTheSerialField_WithNextFree_AndNextSerialIs4004()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        await CreateRugAsync(merchant, LotB);

        var overlap = await CreateRugAsync(merchant, Values(
            ("rug.serial", "3950-3970"), ("rug.qty", 1), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m)), HttpStatusCode.Conflict);
        Assert.Equal("rug.serial", overlap.GetProperty("field").GetString());
        Assert.Contains("۳۹۵۰", overlap.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Number, overlap.GetProperty("nextFree").ValueKind);

        await CreateRugAsync(merchant, Values(
            ("rug.serial", "3985 - 4003"), ("rug.qty", 19), ("rug.intakeLength", 1m), ("rug.intakeWidth", 1m)));
        await CreateRugAsync(merchant, Values(
            ("rug.serial", "2026"), ("rug.intakeLength", 1m), ("rug.intakeWidth", 1m))); // اشتباه تایپ کوچک‌تر

        var next = await GetJsonAsync(merchant, "/api/rugs/next-serial");
        Assert.Equal(4004, next.GetProperty("next").GetInt32());
    }

    [PostgresFact]
    public async Task BySerial_FindsExactAndInsideALot_AndNotPartialText()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var op = P1Client.As(factory, t.Operator);
        using var merchant = P1Client.As(factory, t.Merchant);
        var lot = await CreateRugAsync(merchant, LotB);
        await CreateRugAsync(merchant, Values(("rug.serial", "3778"), ("rug.intakeLength", 2.08m), ("rug.intakeWidth", 2.10m)));

        var exact = await GetJsonAsync(op, "/api/rugs/by-serial/3778");
        Assert.Equal("exact", exact.GetProperty("matchType").GetString());

        var inRange = await GetJsonAsync(op, "/api/rugs/by-serial/3925");
        Assert.Equal("inRange", inRange.GetProperty("matchType").GetString());
        Assert.Equal(Id(lot), inRange.GetProperty("rug").GetProperty("id").GetGuid());
        Assert.Empty(P1Data.NonNullMoneyKeys(inRange));

        await GetJsonAsync(op, "/api/rugs/by-serial/377", HttpStatusCode.NotFound);
        await GetJsonAsync(op, $"/api/rugs/by-serial/{Uri.EscapeDataString("۳۷۷۸")}");
    }

    // ── یک درخواست، عددهای تازه (P2 acceptance #6) ─────────────────────

    [PostgresFact]
    public async Task IntakeSizePut_ReturnsAndPersistsTheNewTotal()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        var rug = await CreateRugAsync(merchant, Values(
            ("rug.serial", "3778"), ("rug.intakeLength", 2.08m), ("rug.intakeWidth", 2.10m),
            ("purchase.price", new { rate = 9_000_000m, basis = (int)CostBasis.PerM2 })));
        Assert.Equal(39_330_000m, Dec(rug.GetProperty("costs"), "totalInvestment"));

        var updated = await SendAsync(merchant, HttpMethod.Put, $"/api/rugs/{Id(rug)}", new
        {
            values = Values(("rug.intakeLength", 3.00m), ("rug.intakeWidth", 2.00m)),
            version = Version(rug)
        });

        Assert.Equal(54_000_000m, Dec(updated.GetProperty("costs"), "totalInvestment"));
        Assert.Equal(54_000_000m, Dec(updated, "purchaseCost"));
        Assert.NotEqual(Version(rug), Version(updated));

        await using var db = factory.CreateDbContext();
        var total = await db.Set<RugCostTotal>().AsNoTracking().SingleAsync(x => x.RugId == Id(rug));
        Assert.Equal(54_000_000m, total.Total);

        // نسخهٔ کهنه → ۴۰۹
        await SendAsync(merchant, HttpMethod.Put, $"/api/rugs/{Id(rug)}",
            new { values = Values(("rug.title", "x")), version = Version(rug) }, HttpStatusCode.Conflict);
        // درخواست فاز ۲ بی نسخه → ۴۰۰
        await SendAsync(merchant, HttpMethod.Put, $"/api/rugs/{Id(rug)}",
            new { values = Values(("rug.title", "x")) }, HttpStatusCode.BadRequest);
    }

    // ── رفت‌وبرگشت فرم قدیمی (P2 acceptance #7، LM-02) ─────────────────

    [PostgresFact]
    public async Task LegacyRoundTrip_KeepsLinesIntakeAndTotal()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        var rug = await CreateRugAsync(merchant, Values(
            ("rug.serial", "3782"), ("rug.title", "گبه"), ("rug.intakeLength", 3.35m), ("rug.intakeWidth", 2.52m),
            ("purchase.price", new { rate = 8_500_000m, basis = (int)CostBasis.PerM2 }), ("purchase.broker", 2m)));
        var id = Id(rug);

        // اندازهٔ نهایی ۳٫۳۰ × ۲٫۵۰ (D.7 #7)
        rug = await SendAsync(merchant, HttpMethod.Put, $"/api/rugs/{id}/final-size",
            new { length = 3.30m, width = 2.50m, version = Version(rug) });
        var before = await SnapshotAsync(id);

        var get = await GetJsonAsync(merchant, $"/api/rugs/{id}");
        Assert.Equal(73_174_800m, Dec(get, "purchaseCost")); // ۷۱٬۷۴۰٬۰۰۰ + ۱٬۴۳۴٬۸۰۰ (D.7 #3)

        // همان DTO با شکل قدیمی (فرم قدیمی purchaseCost و طول/عرض مؤثر را دوباره می‌فرستد)
        var legacy = LegacyBody(get);
        await SendAsync(merchant, HttpMethod.Put, $"/api/rugs/{id}", legacy);
        var after = await SnapshotAsync(id);
        Assert.Equal(before.Lines, after.Lines);
        Assert.Equal((before.IntakeLength, before.IntakeWidth), (after.IntakeLength, after.IntakeWidth));
        Assert.Equal(before.Total, after.Total);

        // فقط عنوان عوض شده: هر دو سطر می‌مانند
        get = await GetJsonAsync(merchant, $"/api/rugs/{id}");
        await SendAsync(merchant, HttpMethod.Put, $"/api/rugs/{id}", LegacyBody(get) with { title = "گبهٔ تازه" });
        Assert.Equal(2, (await SnapshotAsync(id)).Lines.Count);

        // قیمت خرید متفاوت روی فرش چندسطری → ۴۰۹
        get = await GetJsonAsync(merchant, $"/api/rugs/{id}");
        var conflict = await SendAsync(merchant, HttpMethod.Put, $"/api/rugs/{id}",
            LegacyBody(get) with { purchaseCost = 1m }, HttpStatusCode.Conflict);
        Assert.Contains("چندسطری", conflict.GetProperty("message").GetString());

        // طول/عرض متفاوت با اندازهٔ نهایی → ۴۰۹ (از «اندازهٔ نهایی» عوض شود)
        await SendAsync(merchant, HttpMethod.Put, $"/api/rugs/{id}",
            LegacyBody(get) with { lengthMeters = 3.40m }, HttpStatusCode.Conflict);
    }

    private sealed record LegacyPut(
        string? title, string? origin, string? pattern, string? material, int? knotDensity, decimal widthMeters,
        decimal lengthMeters, decimal? purchaseCost, decimal? targetSalePrice, int? status, string? imageUrl, string? notes,
        string? metadataJson, uint version);

    private static LegacyPut LegacyBody(JsonElement rug) => new(
        rug.GetProperty("title").GetString(), rug.GetProperty("origin").GetString(), rug.GetProperty("pattern").GetString(),
        rug.GetProperty("material").GetString(), null, Dec(rug, "widthMeters"), Dec(rug, "lengthMeters"),
        rug.GetProperty("purchaseCost").ValueKind == JsonValueKind.Null ? null : Dec(rug, "purchaseCost"),
        rug.GetProperty("targetSalePrice").ValueKind == JsonValueKind.Null ? null : Dec(rug, "targetSalePrice"),
        null, null, rug.GetProperty("notes").GetString(), rug.GetProperty("metadataJson").GetString(), Version(rug));

    private sealed record Snapshot(IReadOnlyList<(CostBasis Basis, decimal Rate, decimal Amount)> Lines,
        decimal IntakeLength, decimal IntakeWidth, decimal Total);

    private async Task<Snapshot> SnapshotAsync(Guid rugId)
    {
        await using var db = factory.CreateDbContext();
        var rug = await db.Rugs.AsNoTracking().SingleAsync(r => r.Id == rugId);
        var lines = await db.Set<RugPurchaseLine>().AsNoTracking().Where(l => l.RugId == rugId)
            .OrderBy(l => l.SortOrder).Select(l => new { l.Basis, l.Rate, l.Amount }).ToListAsync();
        var total = await db.Set<RugCostTotal>().AsNoTracking().Where(x => x.RugId == rugId).Select(x => x.Total).SingleAsync();
        return new Snapshot(lines.Select(l => (l.Basis, l.Rate, l.Amount)).ToList(), rug.IntakeLength, rug.IntakeWidth, total);
    }

    // ── سطرهای خرید (I.2، L.2) ─────────────────────────────────────────

    [PostgresFact]
    public async Task PurchaseLines_ReplaceAll_FreshAmounts_AndTwoPutsWithOneVersionGiveExactlyOne409()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        var rug = await CreateRugAsync(merchant, LotB);
        var id = Id(rug);

        var current = await GetJsonAsync(merchant, $"/api/rugs/{id}/purchase-lines");
        var version = current.GetProperty("version").GetUInt32();
        var body = new
        {
            version,
            lines = new object[]
            {
                new { id = (Guid?)null, categoryId = (Guid?)null, label = "قیمت خرید", basis = (int)CostBasis.PerPiece, rate = 300_000m, amount = 0m, sortOrder = 10, notes = (string?)null },
                new { id = (Guid?)null, categoryId = (Guid?)null, label = "دلالی", basis = (int)CostBasis.Percent, rate = 2m, amount = 0m, sortOrder = 20, notes = (string?)null }
            }
        };

        var saved = await SendAsync(merchant, HttpMethod.Put, $"/api/rugs/{id}/purchase-lines", body);
        Assert.Equal(new[] { 15_000_000m, 300_000m }, saved.GetProperty("lines").EnumerateArray().Select(l => Dec(l, "amount")).ToArray());
        Assert.Equal(15_300_000m, Dec(saved, "purchaseCost")); // D.7 #3، دستهٔ B
        Assert.Equal(15_300_000m, Dec(saved.GetProperty("costs"), "totalInvestment"));
        Assert.NotEqual(version, saved.GetProperty("version").GetUInt32());

        // همان نسخه دوباره → ۴۰۹
        await SendAsync(merchant, HttpMethod.Put, $"/api/rugs/{id}/purchase-lines", body, HttpStatusCode.Conflict);

        var options = await GetJsonAsync(merchant, "/api/rugs/purchase-categories");
        Assert.Equal(3, options.GetArrayLength());
    }

    // ── اندازهٔ نهایی و برش دسته ───────────────────────────────────────

    [PostgresFact]
    public async Task FinalSize_SameAsIntake_Clear_AndBadShapes()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var op = P1Client.As(factory, t.Operator); // Floor
        using var merchant = P1Client.As(factory, t.Merchant);
        var rug = await CreateRugAsync(merchant, Values(("rug.serial", "3778"), ("rug.intakeLength", 2.08m), ("rug.intakeWidth", 2.10m)));
        var id = Id(rug);

        var same = await SendAsync(op, HttpMethod.Put, $"/api/rugs/{id}/final-size", new { sameAsIntake = true, version = Version(rug) });
        Assert.Equal(2.08m, Dec(same, "finalLength"));
        Assert.Equal(2.10m, Dec(same, "finalWidth"));
        Assert.Empty(P1Data.NonNullMoneyKeys(same));

        await SendAsync(op, HttpMethod.Put, $"/api/rugs/{id}/final-size",
            new { length = 2.0m, sameAsIntake = true, version = Version(same) }, HttpStatusCode.BadRequest);
        await SendAsync(op, HttpMethod.Put, $"/api/rugs/{id}/final-size",
            new { length = 31m, width = 2m, version = Version(same) }, HttpStatusCode.BadRequest);
        await SendAsync(op, HttpMethod.Put, $"/api/rugs/{id}/final-size",
            new { clear = true, version = Version(rug) }, HttpStatusCode.Conflict); // نسخهٔ کهنه

        var cleared = await SendAsync(op, HttpMethod.Put, $"/api/rugs/{id}/final-size", new { clear = true, version = Version(same) });
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("finalLength").ValueKind);
        Assert.Equal(2.08m, Dec(cleared, "lengthMeters"));
    }

    [PostgresFact]
    public async Task Split_AutomaticSerials_AndTotalsKept()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        var values = new Dictionary<string, object?>(LotB)
        {
            ["purchase.price"] = new { rate = 300_000m, basis = (int)CostBasis.PerPiece },
            ["purchase.broker"] = 2m
        };
        var lot = await CreateRugAsync(merchant, values);
        Assert.Equal(15_300_000m, Dec(lot, "purchaseCost"));

        var result = await SendAsync(merchant, HttpMethod.Post, $"/api/rugs/{Id(lot)}/split",
            new { quantity = 20, serialFrom = (string?)null, serialTo = (string?)null, version = Version(lot) });

        var original = result.GetProperty("original");
        var created = result.GetProperty("created");
        Assert.Equal(("3911 - 3940", 30), (original.GetProperty("serialNo").GetString(), original.GetProperty("quantity").GetInt32()));
        Assert.Equal(("3941 - 3960", 20), (created.GetProperty("serialNo").GetString(), created.GetProperty("quantity").GetInt32()));
        Assert.Equal(4.80m, Dec(original, "totalAreaM2"));
        Assert.Equal(3.20m, Dec(created, "totalAreaM2"));
        Assert.Equal(15_300_000m, Dec(original, "purchaseCost") + Dec(created, "purchaseCost"));
        Assert.NotEqual(original.GetProperty("sku").GetString(), created.GetProperty("sku").GetString());

        // بخش ماندهٔ «3911 - 3940» × ۳۰ هنوز بازه‌اش برابر تعداد است، پس برش دوباره هم خودکار است
        var again = await SendAsync(merchant, HttpMethod.Post, $"/api/rugs/{Id(original)}/split",
            new { quantity = 10, version = Version(original) });
        Assert.Equal("3931 - 3940", again.GetProperty("created").GetProperty("serialNo").GetString());

        // بازهٔ بلندتر از تعداد شمارهٔ نوشته‌شده می‌خواهد
        var loose = await CreateRugAsync(merchant, Values(
            ("rug.serial", "5000 - 5010"), ("rug.qty", 5), ("rug.intakeLength", 1m), ("rug.intakeWidth", 1m)));
        await SendAsync(merchant, HttpMethod.Post, $"/api/rugs/{Id(loose)}/split",
            new { quantity = 2, version = Version(loose) }, HttpStatusCode.BadRequest);
        var typed = await SendAsync(merchant, HttpMethod.Post, $"/api/rugs/{Id(loose)}/split",
            new { quantity = 2, serialFrom = "5000 - 5002", serialTo = "۵۰۰۹ تا ۵۰۱۰", version = Version(loose) });
        Assert.Equal("5009 - 5010", typed.GetProperty("created").GetProperty("serialNo").GetString());
    }

    // ── پیشنهاد مقدار و کاتالوگ فیلدها ──────────────────────────────────

    [PostgresFact]
    public async Task Values_AndFields_RespectTenantAndMoney()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var op = P1Client.As(factory, t.Operator);
        await CreateRugAsync(merchant, Values(("rug.origin", "كاشان"), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m)));
        await CreateRugAsync(merchant, Values(("rug.origin", "تبریز"), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m)));

        var values = await GetJsonAsync(op, $"/api/rugs/values?field=rug.origin&q={Uri.EscapeDataString("کا")}");
        Assert.Equal(new[] { "کاشان" }, values.EnumerateArray().Select(v => v.GetString()).ToArray());
        await GetJsonAsync(op, "/api/rugs/values?field=rug.notes", HttpStatusCode.BadRequest);

        var opFields = await GetJsonAsync(op, "/api/fields?purpose=grid");
        Assert.DoesNotContain(opFields.EnumerateArray(), f => f.GetProperty("money").GetBoolean());
        var merchantFields = await GetJsonAsync(merchant, "/api/fields?purpose=grid");
        Assert.Contains(merchantFields.EnumerateArray(), f => f.GetProperty("key").GetString() == "rug.totalCost");
        await GetJsonAsync(op, "/api/fields?purpose=nope", HttpStatusCode.BadRequest);
    }

    // ── پول برای انباردار (A.5) ─────────────────────────────────────────

    [PostgresFact]
    public async Task Operator_CannotWriteOrReadMoney_ThroughTheNewPaths()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var op = P1Client.As(factory, t.Operator);
        var rug = await CreateRugAsync(merchant, Values(
            ("rug.serial", "3778"), ("rug.intakeLength", 2.08m), ("rug.intakeWidth", 2.10m),
            ("purchase.price", new { rate = 9_000_000m, basis = (int)CostBasis.PerM2 })));
        var id = Id(rug);

        // کلید پولی از انباردار، حتی null → ۴۰۳ برای کل درخواست
        await SendAsync(op, HttpMethod.Put, $"/api/rugs/{id}",
            new { values = Values(("rug.title", "x"), ("purchase.price", null)), version = Version(rug) }, HttpStatusCode.Forbidden);
        await SendAsync(op, HttpMethod.Post, "/api/rugs",
            new { values = Values(("rug.targetSalePrice", 1m), ("rug.intakeLength", 1m), ("rug.intakeWidth", 1m)) }, HttpStatusCode.Forbidden);

        foreach (var url in new[] { $"/api/rugs/{id}/purchase-lines", $"/api/rugs/{id}/cost-breakdown", "/api/rugs/purchase-categories" })
            await GetJsonAsync(op, url, HttpStatusCode.Forbidden);

        // نوشتن بی‌پول از انباردار آزاد است و پاسخش پولی ندارد
        var edited = await SendAsync(op, HttpMethod.Put, $"/api/rugs/{id}",
            new { values = Values(("rug.title", "گبه")), version = Version(rug) });
        Assert.Empty(P1Data.NonNullMoneyKeys(edited));
        Assert.Equal(JsonValueKind.Null, edited.GetProperty("purchaseLines").ValueKind);

        await using var db = factory.CreateDbContext();
        Assert.Single(await db.Set<RugPurchaseLine>().AsNoTracking().Where(l => l.RugId == id).ToListAsync());
    }

    // ── سلامت حساب‌ها (L.4 #9) ─────────────────────────────────────────

    [PostgresFact]
    public async Task Diagnostics_AreConfigureOnly_AndAHealthyTenantHasNoMismatch()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var admin = P1Client.As(factory, t.Admin);
        using var merchant = P1Client.As(factory, t.Merchant);
        await CreateRugAsync(merchant, new Dictionary<string, object?>(LotB)
        {
            ["purchase.price"] = new { rate = 300_000m, basis = (int)CostBasis.PerPiece }
        });

        await GetJsonAsync(merchant, "/api/diagnostics/cost-consistency", HttpStatusCode.Forbidden);
        await SendAsync(merchant, HttpMethod.Post, "/api/diagnostics/ledger-rebuild", null, HttpStatusCode.Forbidden);

        var report = await GetJsonAsync(admin, "/api/diagnostics/cost-consistency");
        Assert.Equal(1, report.GetProperty("checked").GetInt32());
        Assert.Equal(0, report.GetProperty("mismatches").GetArrayLength());

        var rebuilt = await SendAsync(admin, HttpMethod.Post, "/api/diagnostics/ledger-rebuild", null);
        Assert.Equal(1, rebuilt.GetProperty("rugs").GetInt32());

        using var page = await admin.GetAsync("/settings/ledger-health");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("data-island=\"ledger-health\"", await page.Content.ReadAsStringAsync());
    }

    // ── قفل رقم اعشار بعد از اولین مبلغ (P2 acceptance، D.5 #22) ────────

    [PostgresFact]
    public async Task MoneyScale_IsLocked_AfterTheFirstPurchaseLine()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var admin = P1Client.As(factory, t.Admin);

        // پیش از هر مبلغی (فقط نوع‌های خرید seed شده، و نرخ درصدی «دلالی» مبلغ نیست) رقم اعشار آزاد است
        var prefs = await GetJsonAsync(admin, "/api/settings/preferences");
        Assert.False(prefs.GetProperty("moneyScaleLocked").GetBoolean());

        await CreateRugAsync(admin, new Dictionary<string, object?>(LotB)
        {
            ["purchase.price"] = new { rate = 300_000m, basis = (int)CostBasis.PerPiece }
        });

        prefs = await GetJsonAsync(admin, "/api/settings/preferences");
        var body = System.Text.Json.Nodes.JsonNode.Parse(prefs.GetRawText())!.AsObject();
        body["moneyScale"] = 2;
        using var put = await admin.SendRawJsonAsync(HttpMethod.Put, "/api/settings/preferences", body.ToJsonString());
        Assert.Equal(HttpStatusCode.Conflict, put.StatusCode);
    }

    // ── صفحهٔ فرش ──────────────────────────────────────────────────────

    [PostgresFact]
    public async Task DetailsPage_HasTheP2Islands_AndHidesMoneyFromTheOperator()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var op = P1Client.As(factory, t.Operator);
        var rug = await CreateRugAsync(merchant, new Dictionary<string, object?>(LotB)
        {
            ["purchase.price"] = new { rate = 1_234_567m, basis = (int)CostBasis.Total }
        });

        var merchantHtml = await (await merchant.GetAsync($"/Rugs/Details/{Id(rug)}")).Content.ReadAsStringAsync();
        foreach (var island in new[] { "rug-final-size", "purchase-lines-editor", "rug-cost-breakdown", "rug-split" })
            Assert.Contains($"data-island=\"{island}\"", merchantHtml);

        using var opResponse = await op.GetAsync($"/Rugs/Details/{Id(rug)}");
        Assert.Equal(HttpStatusCode.OK, opResponse.StatusCode);
        var opHtml = await opResponse.Content.ReadAsStringAsync();
        Assert.Contains("data-island=\"rug-final-size\"", opHtml);
        Assert.DoesNotContain("data-island=\"purchase-lines-editor\"", opHtml);
        Assert.DoesNotContain("data-island=\"rug-cost-breakdown\"", opHtml);
        foreach (var form in P1Data.RenderedForms(1_234_567m))
            Assert.DoesNotContain(form, opHtml);

        // فهرست: شماره ستون اول و قابل مرتب‌سازی
        var list = await (await merchant.GetAsync("/Rugs?sortBy=Serial&descending=false")).Content.ReadAsStringAsync();
        Assert.Contains("3911 - 3960", list.Replace("۳۹۱۱ - ۳۹۶۰", "3911 - 3960"));
    }
}
