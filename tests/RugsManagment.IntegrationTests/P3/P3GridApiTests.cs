using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RugsManagment.Application.Features.Intake;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.IntegrationTests.P1;
using RugsManagment.IntegrationTests.P2;
using RugsManagment.IntegrationTests.Support;
using static RugsManagment.IntegrationTests.P2.P2Http;
using static RugsManagment.IntegrationTests.P3.P3Grid;

namespace RugsManagment.IntegrationTests.P3;

/// <summary>
/// پذیرش سمت سرور جدول ورود (spec K.2 «P3 Acceptance»، G.2، G.4، G.6، E6): ستون‌های پیش‌فرض و پول فقط برای Business،
/// D.7 #1–#3 تایپ‌شده در جدول، یکتایی با ClientRowId، شمارهٔ بعدی، ورودیِ (روز، فروشنده)، ادغام سه‌طرفه و تعارض، دو
/// ویرایش هم‌زمان خرید، جداسازی ردیف خطادار، خانه‌های خرید (سطر «کل» قدیمی، «چند سطر»، پاک کردن)، قاعدهٔ حذف و برگرداندن
/// انباردار، و جداسازی کارگاه.
/// </summary>
public sealed class P3GridApiTests(RugsWebFactory factory) : IClassFixture<RugsWebFactory>
{
    private static readonly string Gabbeh = "گبه";
    private static readonly string HajReza = "حاج رضا";

    // ── ستون‌ها و پول ──────────────────────────────────────────────────

    [PostgresFact]
    public async Task IntakeColumns_FollowG2_AndMoneyColumnsOnlyForBusiness()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var op = P1Client.As(factory, t.Operator);

        var business = await GetJsonAsync(merchant, "/api/grid/intake");
        Assert.Equal(
            ["rug.serial", "batch.seller", "rug.qty", "rug.pattern", "rug.origin", "rug.material", "rug.intakeLength",
             "rug.intakeWidth", "rug.areaM2", "purchase.price", "purchase.broker", "rug.purchaseTotal", "rug.notes"],
            ColumnKeys(business));
        var price = business.GetProperty("columns").EnumerateArray().Single(c => c.GetProperty("key").GetString() == "purchase.price");
        Assert.Equal("money+unit", price.GetProperty("type").GetString());
        Assert.True(price.GetProperty("sticky").GetBoolean());
        Assert.Equal("PerM2", price.GetProperty("options")[0].GetString());
        var area = business.GetProperty("columns").EnumerateArray().Single(c => c.GetProperty("key").GetString() == "rug.areaM2");
        Assert.False(area.GetProperty("tabStop").GetBoolean());

        var floor = await GetJsonAsync(op, "/api/grid/intake");
        Assert.DoesNotContain(ColumnKeys(floor), k => k.StartsWith("purchase.", StringComparison.Ordinal) || k == "rug.purchaseTotal");
        Assert.All(floor.GetProperty("columns").EnumerateArray(), c => Assert.False(c.GetProperty("money").GetBoolean()));
        Assert.Equal(JsonValueKind.Null, floor.GetProperty("totals").GetProperty("purchase").ValueKind);

        await GetJsonAsync(merchant, "/api/grid/nope", HttpStatusCode.NotFound);
    }

    // ── D.7 #1–#3 در جدول ───────────────────────────────────────────────

    [PostgresFact]
    public async Task D7_TypedInTheGrid_GivesThePurchaseCosts_AndDayTotals()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);

        var saved = await SaveAsync(merchant, D7Rows(), seller: HajReza);
        AllSaved(saved);
        var results = Results(saved);

        Assert.Equal(
            [40_116_600m, 15_300_000m, 73_174_800m],
            results.Select(r => Derived(r, "rug.purchaseTotal").GetDecimal()).ToArray());
        Assert.Equal([4.37m, 0.16m, 8.44m], results.Select(r => Derived(r, "rug.areaM2").GetDecimal()).ToArray());
        Assert.Equal(8m, Derived(results[1], "rug.totalAreaM2").GetDecimal());

        // خانه‌های خرید همان‌طور که نوشته شد برمی‌گردند: «هر تخته» ۳۰۰٬۰۰۰ و دلالی ۲٪
        var bPrice = Current(results[1], "purchase.price");
        Assert.Equal((300_000m, (int)CostBasis.PerPiece), (bPrice.GetProperty("rate").GetDecimal(), bPrice.GetProperty("basis").GetInt32()));
        Assert.Equal(2m, Current(results[1], "purchase.broker").GetProperty("rate").GetDecimal());
        Assert.Equal(HajReza, Current(results[0], "batch.seller").GetString());

        var totals = saved.GetProperty("totals");
        Assert.Equal(3, totals.GetProperty("rows").GetInt32());
        Assert.Equal(52, totals.GetProperty("pieces").GetInt32());
        Assert.Equal(20.81m, totals.GetProperty("areaM2").GetDecimal());
        Assert.Equal(128_591_400m, totals.GetProperty("purchase").GetDecimal());
        var group = Assert.Single(totals.GetProperty("groups").EnumerateArray());
        Assert.Equal(HajReza, group.GetProperty("sellerName").GetString());

        // پس از ذخیره: شمارهٔ بعدی بعد از «3911 - 3960»
        Assert.Equal(3961, saved.GetProperty("nextSerial").GetInt32());

        // GET همان ردیف‌ها را با همان عددها می‌دهد؛ متراژ حساب‌شده همان ستون ذخیره‌شده است (L.2)
        var grid = await GetJsonAsync(merchant, "/api/grid/intake");
        await using var db = factory.CreateDbContext();
        foreach (var result in results)
        {
            var row = Row(grid, RugId(result));
            var stored = await db.Rugs.AsNoTracking().SingleAsync(r => r.Id == RugId(result));
            Assert.Equal(stored.AreaM2, row.GetProperty("derived").GetProperty("rug.areaM2").GetDecimal());
            Assert.Equal(stored.TotalAreaM2, row.GetProperty("derived").GetProperty("rug.totalAreaM2").GetDecimal());
        }
    }

    // ── یکتایی با ClientRowId ───────────────────────────────────────────

    [PostgresFact]
    public async Task SameClientRowId_Twice_IsOneRug()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var op = P1Client.As(factory, t.Operator);

        var row = P3Op.New(("rug.serial", "100"), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m));
        var first = Results(await SaveAsync(op, [row]))[0];
        var again = Results(await SaveAsync(op, [row]))[0];

        Assert.Equal(0, Status(again));
        Assert.Equal(RugId(first), RugId(again));
        await using var db = factory.CreateDbContext();
        Assert.Equal(1, await db.Rugs.CountAsync(r => r.TenantId == t.TenantId && r.ClientRowId == row.ClientRowId));
    }

    // ── شمارهٔ بعدی و حالت خودکار ──────────────────────────────────────

    [PostgresFact]
    public async Task NextSerial_After3985To4003_Is4004_AndAutoModeNumbersALotInTheRequest()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var admin = P1Client.As(factory, t.Admin);

        var saved = await SaveAsync(merchant, [
            P3Op.New(("rug.serial", "3985 - 4003"), ("rug.qty", 19), ("rug.intakeLength", 1m), ("rug.intakeWidth", 1m)),
            P3Op.New(("rug.serial", "2026"), ("rug.intakeLength", 1m), ("rug.intakeWidth", 1m))
        ]);
        AllSaved(saved);
        Assert.Equal(4004, saved.GetProperty("nextSerial").GetInt32());
        Assert.Equal(4004, (await GetJsonAsync(merchant, "/api/grid/intake")).GetProperty("nextSerial").GetInt32());

        // حالت خودکار: سرور شماره می‌گذارد، دسته بازهٔ کامل، ردیف بعدی همان درخواست بعد از دسته
        await SetPreferencesAsync(admin, prefs => prefs["serialMode"] = (int)SerialMode.Auto);
        var auto = await SaveAsync(merchant, [
            P3Op.New(("rug.serial", "1"), ("rug.qty", 50), ("rug.intakeLength", 0.40m), ("rug.intakeWidth", 0.40m)),
            P3Op.New(("rug.intakeLength", 2m), ("rug.intakeWidth", 3m))
        ]);
        AllSaved(auto);
        var rows = Results(auto);
        Assert.Equal("4004 - 4053", Current(rows[0], "rug.serial").GetString());
        Assert.Equal("4054", Current(rows[1], "rug.serial").GetString());
    }

    // ── ورودیِ (روز، فروشنده) ───────────────────────────────────────────

    [PostgresFact]
    public async Task TwoSellersOneDay_AreTwoBatches_ChangingASellerMovesOnlyThatRow_AndYesterdayReopens()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);

        var saved = await SaveAsync(merchant, [
            P3Op.New(("batch.seller", HajReza), ("rug.serial", "1"), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m)),
            P3Op.New(("batch.seller", "حاج‌رضا"), ("rug.serial", "2"), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m)),
            P3Op.New(("batch.seller", "کریمی"), ("rug.serial", "3"), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m))
        ]);
        AllSaved(saved);
        var rows = Results(saved);
        var reza = Derived(rows[0], "batch.id").GetGuid();
        Assert.Equal(reza, Derived(rows[1], "batch.id").GetGuid()); // «حاج‌رضا» با نیم‌فاصله همان فروشنده است
        var karimi = Derived(rows[2], "batch.id").GetGuid();
        Assert.NotEqual(reza, karimi);
        Assert.Equal(2, saved.GetProperty("totals").GetProperty("groups").GetArrayLength());

        // فروشندهٔ ردیف ۱ عوض می‌شود → فقط همان ردیف به ورودیِ کریمی می‌رود
        var moved = Results(await SaveAsync(merchant, [
            new P3Op(rows[0].GetProperty("clientRowId").GetGuid(), Values(("batch.seller", "کریمی")), RugId(rows[0]),
                RowVersion(rows[0]), Values(("batch.seller", HajReza)))
        ]))[0];
        Assert.Equal(0, Status(moved));
        Assert.Equal(karimi, Derived(moved, "batch.id").GetGuid());
        await using (var db = factory.CreateDbContext())
        {
            Assert.Equal(reza, (await db.Rugs.AsNoTracking().SingleAsync(r => r.Id == RugId(rows[1]))).BatchId);
            Assert.Equal(2, await db.RugBatches.CountAsync(b => b.TenantId == t.TenantId));
        }

        // دیروز: ورودی جدا، در «ورودی‌های اخیر» با روزش، و جدول همان روز دوباره بازش می‌کند
        var yesterday = IranDay.Today().AddDays(-1);
        var late = Results(await SaveAsync(merchant, [
            P3Op.New(("rug.serial", "10 - 12"), ("rug.qty", 3), ("rug.intakeLength", 1m), ("rug.intakeWidth", 1m))
        ], seller: HajReza, day: yesterday))[0];
        Assert.Equal(0, Status(late));
        Assert.NotEqual(reza, Derived(late, "batch.id").GetGuid());

        var recent = await GetJsonAsync(merchant, "/api/batches/recent?days=7");
        var old = recent.EnumerateArray().Single(b => b.GetProperty("id").GetGuid() == Derived(late, "batch.id").GetGuid());
        Assert.Equal(yesterday.ToString("yyyy-MM-dd"), old.GetProperty("receivedOn").GetString());
        Assert.Equal(3, old.GetProperty("pieces").GetInt32());
        Assert.Equal(HajReza, old.GetProperty("sellerName").GetString());

        var yesterdayGrid = await GetJsonAsync(merchant, $"/api/grid/intake?date={yesterday:yyyy-MM-dd}");
        Assert.Equal([RugId(late)], Rows(yesterdayGrid).Select(r => r.GetProperty("rugId").GetGuid()).ToArray());
        var todayGrid = await GetJsonAsync(merchant, "/api/grid/intake");
        Assert.DoesNotContain(Rows(todayGrid), r => r.GetProperty("rugId").GetGuid() == RugId(late));
    }

    // ── ادغام سه‌طرفه و تعارض ──────────────────────────────────────────

    [PostgresFact]
    public async Task TwoTabs_DifferentFieldsMerge_SameFieldConflicts()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);

        var created = Results(await SaveAsync(merchant, [
            P3Op.New(("rug.serial", "500"), ("rug.pattern", "قاب"), ("rug.origin", "کاشان"), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m))
        ]))[0];
        var row = created.GetProperty("clientRowId").GetGuid();
        var id = RugId(created);
        var seen = RowVersion(created);

        // زبانهٔ ۱ نقشه را عوض می‌کند
        var tab1 = Results(await SaveAsync(merchant, [
            new P3Op(row, Values(("rug.pattern", "لچک ترنج")), id, seen, Values(("rug.pattern", "قاب")))
        ]))[0];
        Assert.Equal(0, Status(tab1));

        // زبانهٔ ۲ با نسخهٔ کهنه محل بافت را عوض می‌کند → ادغام، هر دو می‌مانند
        var tab2 = Results(await SaveAsync(merchant, [
            new P3Op(row, Values(("rug.origin", Gabbeh)), id, seen, Values(("rug.origin", "کاشان")))
        ]))[0];
        Assert.Equal(0, Status(tab2));
        Assert.Equal("لچک ترنج", Current(tab2, "rug.pattern").GetString());
        Assert.Equal(Gabbeh, Current(tab2, "rug.origin").GetString());

        // زبانهٔ ۲ همان نقشه را (از پایهٔ کهنه) چیز دیگری می‌نویسد → تعارض با مقدار سرور
        var conflict = Results(await SaveAsync(merchant, [
            new P3Op(row, Values(("rug.pattern", "افشان")), id, seen, Values(("rug.pattern", "قاب")))
        ]))[0];
        Assert.Equal(2, Status(conflict));
        Assert.Equal("لچک ترنج", Current(conflict, "rug.pattern").GetString());
        Assert.Contains(conflict.GetProperty("errors").EnumerateArray(), e => e.GetProperty("field").GetString() == "rug.pattern");

        // «نوشتهٔ من بماند»: با نسخهٔ تازه دوباره → ذخیره
        var mine = Results(await SaveAsync(merchant, [
            new P3Op(row, Values(("rug.pattern", "افشان")), id, RowVersion(conflict), Values(("rug.pattern", "لچک ترنج")))
        ]))[0];
        Assert.Equal(0, Status(mine));
        Assert.Equal("افشان", Current(mine, "rug.pattern").GetString());
    }

    [PostgresFact]
    public async Task TwoConcurrentPurchaseEdits_GiveExactlyOneConflict()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var admin = P1Client.As(factory, t.Admin);

        var created = Results(await SaveAsync(merchant, [
            P3Op.New(("rug.serial", "600"), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m),
                ("purchase.price", Price(1_000_000m, CostBasis.PerM2)))
        ]))[0];
        var row = created.GetProperty("clientRowId").GetGuid();
        var id = RugId(created);
        var seen = RowVersion(created);
        var basePrice = Values(("purchase.price", Price(1_000_000m, CostBasis.PerM2)));

        var first = SaveAsync(merchant, [new P3Op(row, Values(("purchase.price", Price(2_000_000m, CostBasis.PerM2))), id, seen, basePrice)]);
        var second = SaveAsync(admin, [new P3Op(row, Values(("purchase.price", Price(3_000_000m, CostBasis.PerM2))), id, seen, basePrice)]);
        var statuses = (await Task.WhenAll(first, second)).Select(r => Status(Results(r)[0])).OrderBy(s => s).ToArray();

        Assert.Equal([0, 2], statuses);
        await using var db = factory.CreateDbContext();
        var line = await db.Set<RugPurchaseLine>().AsNoTracking().SingleAsync(l => l.RugId == id);
        Assert.Contains(line.Rate, new[] { 2_000_000m, 3_000_000m });
    }

    // ── ردیف خطادار جدا می‌شود ─────────────────────────────────────────

    [PostgresFact]
    public async Task OverlappingRow_IsIsolated_OthersAreSaved_AndItGetsNextFree()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        AllSaved(await SaveAsync(merchant, [
            P3Op.New(("rug.serial", "3911 - 3960"), ("rug.qty", 50), ("rug.intakeLength", 0.40m), ("rug.intakeWidth", 0.40m))
        ]));

        var saved = await SaveAsync(merchant, [
            P3Op.New(("rug.serial", "7001"), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m)),
            P3Op.New(("rug.serial", "3950"), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m)),
            P3Op.New(("rug.serial", "7002"), ("rug.intakeLength", 0.5m), ("rug.intakeWidth", 99m)), // اندازهٔ نادرست
            P3Op.New(("rug.serial", "7003"), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m))
        ]);
        var rows = Results(saved);
        Assert.Equal([0, 1, 1, 0], rows.Select(Status).ToArray());
        Assert.Equal("rug.serial", rows[1].GetProperty("errors")[0].GetProperty("field").GetString());
        Assert.Equal(JsonValueKind.Number, rows[1].GetProperty("nextFree").ValueKind);
        Assert.Equal("rug.intakeWidth", rows[2].GetProperty("errors")[0].GetProperty("field").GetString());

        await using var db = factory.CreateDbContext();
        Assert.Equal(3, await db.Rugs.CountAsync(r => r.TenantId == t.TenantId));
    }

    // ── خانه‌های خرید ──────────────────────────────────────────────────

    [PostgresFact]
    public async Task PurchaseCells_LegacyTotalShowsAsKol_TwoLinesAreReadOnly_ClearingDeletes_UnitChipChangesBasis()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);

        // سطر «کل» قدیمی (همان که M2 از قیمت خرید ساخت): از فرم قدیمی
        var legacy = await SendAsync(merchant, HttpMethod.Post, "/api/rugs", new
        {
            title = "قدیمی", lengthMeters = 2m, widthMeters = 3m, purchaseCost = 40_000_000m, customSteps = Array.Empty<object>()
        });
        var legacyId = Id(legacy);
        var byTitle = "/api/grid/rugs?search=" + Uri.EscapeDataString("قدیمی");
        var grid = await GetJsonAsync(merchant, byTitle);
        var cell = Row(grid, legacyId).GetProperty("values").GetProperty("purchase.price");
        Assert.Equal((40_000_000m, (int)CostBasis.Total), (cell.GetProperty("rate").GetDecimal(), cell.GetProperty("basis").GetInt32()));

        // دو سطر از یک نوع → «چند سطر» فقط‌خواندنی؛ نوشتنش خطای همان خانه است
        var lines = await GetJsonAsync(merchant, $"/api/rugs/{legacyId}/purchase-lines");
        var priceCategory = lines.GetProperty("lines")[0].GetProperty("categoryId").GetGuid();
        await SendAsync(merchant, HttpMethod.Put, $"/api/rugs/{legacyId}/purchase-lines", new
        {
            version = lines.GetProperty("version").GetUInt32(),
            lines = new object[]
            {
                new { id = (Guid?)null, categoryId = priceCategory, label = "قیمت خرید", basis = (int)CostBasis.Total, rate = 40_000_000m, amount = 0m, sortOrder = 10, notes = (string?)null },
                new { id = (Guid?)null, categoryId = priceCategory, label = "قیمت خرید", basis = (int)CostBasis.Total, rate = 1_000_000m, amount = 0m, sortOrder = 11, notes = (string?)null }
            }
        });
        var multiGrid = await GetJsonAsync(merchant, byTitle);
        var multiRow = Row(multiGrid, legacyId);
        var multi = multiRow.GetProperty("values").GetProperty("purchase.price");
        Assert.Equal(2, multi.GetProperty("lines").GetInt32());
        Assert.Equal(JsonValueKind.Null, multi.GetProperty("rate").ValueKind);
        Assert.Equal(41_000_000m, multi.GetProperty("amount").GetDecimal());

        var refused = Results(await SaveAsync(merchant, [
            new P3Op(Guid.NewGuid(), Values(("purchase.price", Price(5m, CostBasis.Total))), legacyId,
                multiRow.GetProperty("version").GetUInt32(), null)
        ], gridId: "rugs"))[0];
        Assert.Equal(1, Status(refused));
        Assert.Equal("purchase.price", refused.GetProperty("errors")[0].GetProperty("field").GetString());

        // یک سطر «هر متر» روی دسته؛ چیپ واحد به «هر تخته»؛ بعد پاک کردن خانه = حذف سطر
        var lot = Results(await SaveAsync(merchant, [
            P3Op.New(("rug.serial", "800 - 849"), ("rug.qty", 50), ("rug.intakeLength", 0.40m), ("rug.intakeWidth", 0.40m),
                ("purchase.price", Price(9_000_000m, CostBasis.PerM2)))
        ]))[0];
        Assert.Equal(72_000_000m, Derived(lot, "rug.purchaseTotal").GetDecimal()); // ۹٬۰۰۰٬۰۰۰ × ۰٫۱۶ × ۵۰
        var chip = Results(await SaveAsync(merchant, [
            new P3Op(lot.GetProperty("clientRowId").GetGuid(), Values(("purchase.price", new { rate = (decimal?)null, basis = (int)CostBasis.PerPiece })),
                RugId(lot), RowVersion(lot), null)
        ]))[0];
        Assert.Equal(0, Status(chip));
        Assert.Equal(450_000_000m, Derived(chip, "rug.purchaseTotal").GetDecimal()); // ۹٬۰۰۰٬۰۰۰ × ۵۰
        var cleared = Results(await SaveAsync(merchant, [
            new P3Op(lot.GetProperty("clientRowId").GetGuid(), Values(("purchase.price", null)), RugId(lot), RowVersion(chip), null)
        ]))[0];
        Assert.Equal(0, Status(cleared));
        Assert.Equal(JsonValueKind.Null, Current(cleared, "purchase.price").ValueKind);
        await using var db = factory.CreateDbContext();
        Assert.False(await db.Set<RugPurchaseLine>().AnyAsync(l => l.RugId == RugId(lot)));
    }

    // ── انباردار: پول و حذف و برگرداندن ────────────────────────────────

    [PostgresFact]
    public async Task Operator_GetsNoMoney_InjectedMoneyKeyIs403_ForTheWholeRequest()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var op = P1Client.As(factory, t.Operator);
        AllSaved(await SaveAsync(merchant, D7Rows(), seller: HajReza));

        var grid = await GetJsonAsync(op, "/api/grid/intake");
        Assert.Equal(3, Rows(grid).Count);
        foreach (var row in Rows(grid))
        {
            Assert.DoesNotContain(row.GetProperty("values").EnumerateObject(), p => p.Name.StartsWith("purchase.", StringComparison.Ordinal));
            Assert.False(row.GetProperty("derived").TryGetProperty("rug.purchaseTotal", out _));
        }
        Assert.Empty(P1Data.NonNullMoneyKeys(grid));

        var plain = P3Op.New(("rug.serial", "900"), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m));
        var injected = P3Op.New(("rug.serial", "901"), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m),
            ("purchase.price", Price(1m, CostBasis.Total)));
        await SaveAsync(op, [plain, injected], expected: HttpStatusCode.Forbidden);
        await SaveAsync(op, [plain with { BaseValues = Values(("purchase.broker", 2m)) }], expected: HttpStatusCode.Forbidden);

        await using var db = factory.CreateDbContext();
        Assert.False(await db.Rugs.AnyAsync(r => r.TenantId == t.TenantId && r.SerialNo == "900"));
    }

    [PostgresFact]
    public async Task Operator_DeletesOnlyHisOwnMoneylessRug_PerRow_AndRestoresIt()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var op = P1Client.As(factory, t.Operator);

        var mine = Results(await SaveAsync(op, [P3Op.New(("rug.serial", "11"), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m))]))[0];
        var withMoney = Results(await SaveAsync(op, [P3Op.New(("rug.serial", "12"), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m))]))[0];
        var merchants = Results(await SaveAsync(merchant, [P3Op.New(("rug.serial", "13"), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m))]))[0];
        Assert.True(Derived(mine, "row.canDelete").GetBoolean());
        Assert.True(Derived(merchants, "row.canDelete").GetBoolean()); // بازرگان هر فرش فروخته‌نشده‌ای را حذف می‌کند

        // بازرگان روی فرش انباردار قیمت خرید می‌گذارد → دیگر «بی مبلغ» نیست
        AllSaved(await SaveAsync(merchant, [
            new P3Op(withMoney.GetProperty("clientRowId").GetGuid(), Values(("purchase.price", Price(1_000m, CostBasis.Total))),
                RugId(withMoney), RowVersion(withMoney), null)
        ]));

        var opGrid = await GetJsonAsync(op, "/api/grid/intake");
        Assert.True(Row(opGrid, RugId(mine)).GetProperty("derived").GetProperty("row.canDelete").GetBoolean());
        Assert.False(Row(opGrid, RugId(withMoney)).GetProperty("derived").GetProperty("row.canDelete").GetBoolean());
        Assert.False(Row(opGrid, RugId(merchants)).GetProperty("derived").GetProperty("row.canDelete").GetBoolean());

        var deleted = Results(await SaveAsync(op, [
            new P3Op(Guid.NewGuid(), Values(), RugId(mine), null, null, Delete: true),
            new P3Op(Guid.NewGuid(), Values(), RugId(withMoney), null, null, Delete: true),
            new P3Op(Guid.NewGuid(), Values(), RugId(merchants), null, null, Delete: true),
            P3Op.New(("rug.serial", "14"), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m))
        ]));
        Assert.Equal([3, 1, 1, 0], deleted.Select(Status).ToArray());
        Assert.Equal("این فرش را فقط بازرگان می‌تواند حذف کند.", deleted[2].GetProperty("errors")[0].GetProperty("message").GetString());

        await using (var db = factory.CreateDbContext())
        {
            Assert.NotNull((await db.Rugs.IgnoreQueryFilters().AsNoTracking().SingleAsync(r => r.Id == RugId(mine))).DeletedAt);
            Assert.Null((await db.Rugs.AsNoTracking().SingleAsync(r => r.Id == RugId(merchants))).DeletedAt);
        }

        // «برگردان» (E6): انباردار فرش خودش را برمی‌گرداند؛ فرش بازرگان را نه
        var restored = await SendAsync(op, HttpMethod.Post, $"/api/grid/rugs/{RugId(mine)}/restore", null);
        Assert.Equal(RugId(mine), restored.GetProperty("rugId").GetGuid());
        Assert.Equal("11", restored.GetProperty("values").GetProperty("rug.serial").GetString());

        var merchantDelete = Results(await SaveAsync(merchant, [
            new P3Op(Guid.NewGuid(), Values(), RugId(merchants), null, null, Delete: true)
        ]))[0];
        Assert.Equal(3, Status(merchantDelete));
        await SendAsync(op, HttpMethod.Post, $"/api/grid/rugs/{RugId(merchants)}/restore", null, HttpStatusCode.Forbidden);
        await SendAsync(merchant, HttpMethod.Post, $"/api/grid/rugs/{RugId(merchants)}/restore", null);
        await SendAsync(merchant, HttpMethod.Post, $"/api/grid/rugs/{RugId(merchants)}/restore", null, HttpStatusCode.BadRequest);
    }

    // ── جداسازی کارگاه ─────────────────────────────────────────────────

    [PostgresFact]
    public async Task OtherTenantsRugsAndBatches_AreInvisible_AndUntouchable()
    {
        var a = await P2Tenant.CreateAsync(factory);
        var b = await P2Tenant.CreateAsync(factory);
        using var merchantA = P1Client.As(factory, a.Merchant);
        using var merchantB = P1Client.As(factory, b.Merchant);

        var bRow = Results(await SaveAsync(merchantB, [
            P3Op.New(("rug.serial", "77"), ("rug.pattern", "قاب"), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m))
        ], seller: HajReza))[0];

        Assert.Empty(Rows(await GetJsonAsync(merchantA, "/api/grid/intake")));

        // ردیف با شناسهٔ فرش کارگاه دیگر: «دیگر نیست»، و هیچ چیزی عوض نمی‌شود
        var foreign = Results(await SaveAsync(merchantA, [
            new P3Op(Guid.NewGuid(), Values(("rug.pattern", "افشان")), RugId(bRow), RowVersion(bRow), null)
        ]))[0];
        Assert.Equal(3, Status(foreign));
        await SaveAsync(merchantA, [new P3Op(Guid.NewGuid(), Values(), RugId(bRow), null, null, Delete: true)]);
        await SaveAsync(merchantA, [P3Op.New(("rug.intakeLength", 1m), ("rug.intakeWidth", 1m))],
            batchId: Derived(bRow, "batch.id").GetGuid(), expected: HttpStatusCode.NotFound);
        await SendAsync(merchantA, HttpMethod.Post, $"/api/grid/rugs/{RugId(bRow)}/restore", null, HttpStatusCode.NotFound);

        // همان ClientRowId در کارگاه دیگر فرش خودش را می‌سازد
        var sameRow = Results(await SaveAsync(merchantA, [
            new P3Op(bRow.GetProperty("clientRowId").GetGuid(), Values(("rug.serial", "77"), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m)))
        ]))[0];
        Assert.Equal(0, Status(sameRow));
        Assert.NotEqual(RugId(bRow), RugId(sameRow));

        await using var db = factory.CreateDbContext();
        var stored = await db.Rugs.AsNoTracking().SingleAsync(r => r.Id == RugId(bRow));
        Assert.Equal(("قاب", (DateTimeOffset?)null), (stored.Pattern, stored.DeletedAt));
    }

    // ── داده‌ها ─────────────────────────────────────────────────────────

    /// <summary>D.7 #1–#3: A «3778» گبه ۲٫۰۸×۲٫۱۰ متری ۹٬۰۰۰٬۰۰۰؛ B «3911 - 3960» × ۵۰ هر تخته ۳۰۰٬۰۰۰؛ C «3782» متری ۸٬۵۰۰٬۰۰۰؛ دلالی ۲.</summary>
    private static IReadOnlyList<P3Op> D7Rows() =>
    [
        P3Op.New(("rug.serial", "3778"), ("rug.origin", Gabbeh), ("rug.intakeLength", 2.08m), ("rug.intakeWidth", 2.10m),
            ("purchase.price", Price(9_000_000m, CostBasis.PerM2)), ("purchase.broker", 2m)),
        P3Op.New(("rug.serial", "3911 - 3960"), ("rug.qty", 50), ("rug.origin", Gabbeh), ("rug.intakeLength", 0.40m),
            ("rug.intakeWidth", 0.40m), ("purchase.price", Price(300_000m, CostBasis.PerPiece)), ("purchase.broker", 2m)),
        P3Op.New(("rug.serial", "3782"), ("rug.origin", Gabbeh), ("rug.intakeLength", 3.35m), ("rug.intakeWidth", 2.52m),
            ("purchase.price", Price(8_500_000m, CostBasis.PerM2)), ("purchase.broker", 2m))
    ];
}
