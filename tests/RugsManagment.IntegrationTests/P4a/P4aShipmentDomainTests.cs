using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RugsManagment.Domain.Entities;
using RugsManagment.IntegrationTests.P1;
using RugsManagment.IntegrationTests.P2;
using RugsManagment.IntegrationTests.Support;
using static RugsManagment.IntegrationTests.P2.P2Http;
using static RugsManagment.IntegrationTests.P4a.P4aApi;

namespace RugsManagment.IntegrationTests.P4a;

/// <summary>
/// دامنهٔ محموله روی دیتابیس واقعی (K.2/P4a، L.2): نگله‌بندی با شماره، اندازهٔ نهایی، قاعدهٔ دسته در جمع نگله، enricher و
/// فیلترهای فهرست فرش، ارزش گمرکی (جدول، فیلد کلید، نرخ پیش‌فرض، افزودن بازرگان، مهر)، حذف و شماره‌گذاری دوبارهٔ نگله،
/// «قطعی» و «باز کردن دوباره» با هش پایدار. قفل‌ها بعد از «قطعی» در <see cref="P4aLockTests"/> است.
///
/// عددهای دستی (قاعدهٔ دسته، G.4): هر تخته ROUND(L×W, 2)، دلار تخته ROUND(مساحت × نرخ، 2)، و خط = تخته × تعداد.
/// </summary>
public sealed class P4aShipmentDomainTests(RugsWebFactory factory) : IClassFixture<RugsWebFactory>
{
    // ═════════════════════════════════════════════════════════════════════
    // نگله‌بندی با شماره، اندازهٔ نهایی، جمع‌ها، enricher و فیلترها
    // ═════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task BalingBySerial_FinalSizes_AndTheLotRuleTotals_MatchAHandComputation()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var op = P1Client.As(factory, t.Operator);

        await AddRateAsync(merchant, "گبه", 45.55m);
        await AddRateAsync(merchant, "کاشان", 60m);
        var a = await RugAsync(merchant, "3778", 2.08m, 2.10m, "گبه");
        var lot = await RugAsync(merchant, "3911 - 3960", 0.40m, 0.40m, "گبه", 50);
        var c = await RugAsync(merchant, "3779", 1.93m, 1.50m, "کاشان");
        var stock = await RugAsync(merchant, "3780", 1m, 1m, "گبه");
        var shipmentId = await NewShipmentAsync(merchant);

        // نگلهٔ ۱ را اولین فرشش می‌سازد (C.2)
        var first = await OpsAsync(op, shipmentId, new { op = "add", clientOpId = "a", baleNumber = 1, serial = "3778" });
        var firstRow = Result(first).GetProperty("row");
        Assert.Equal(1, firstRow.GetProperty("baleNumber").GetInt32());
        Assert.Single(first.GetProperty("bales").EnumerateArray());
        var bale1 = firstRow.GetProperty("baleId").GetGuid();

        // عدد داخل یک دسته اول می‌پرسد و چیزی نمی‌سازد (نه خط، نه نگلهٔ ۲)
        var inRange = await OpsAsync(op, shipmentId, new { op = "add", clientOpId = "r", baleNumber = 2, serial = "3920" });
        var prompt = Result(inRange, ok: false).GetProperty("prompt");
        Assert.Equal("Q_LINE_SERIAL_IN_RANGE", prompt.GetProperty("code").GetString());
        Assert.Equal("inRange", prompt.GetProperty("matchType").GetString());
        Assert.Equal(Id(lot), prompt.GetProperty("rugId").GetGuid());
        Assert.Equal(50, prompt.GetProperty("quantity").GetInt32());
        Assert.Contains("۳۹۱۱", prompt.GetProperty("message").GetString());
        Assert.Contains("۳۹۶۰", prompt.GetProperty("message").GetString());
        Assert.Single(inRange.GetProperty("bales").EnumerateArray());

        // «[کل دسته را بگذار]»: همان عمل با شناسهٔ دسته → نگلهٔ ۲ همین‌جا ساخته می‌شود
        Result(await OpsAsync(op, shipmentId, Add(Id(lot), 2)));

        // افزودنِ ناموفق با شمارهٔ نگلهٔ تازه هیچ نگلهٔ خالی جا نمی‌گذارد («نگلهٔ بعد» فقط شمارهٔ جاری مرورگر است)
        var missing = await OpsAsync(op, shipmentId, new { op = "add", clientOpId = "x", baleNumber = 3, serial = "99999" });
        Assert.Equal("E_RUG_SERIAL_NOT_FOUND", ErrorCode(Result(missing, ok: false)));
        Assert.Equal(2, missing.GetProperty("bales").GetArrayLength());

        // با شناسهٔ نگله هم می‌شود
        Result(await OpsAsync(op, shipmentId, new { op = "add", clientOpId = "c", baleId = bale1, serial = "3779" }));

        // اسکن دوبارهٔ همان فرش در همان نگله: کاری نیست، خط دوم ساخته نمی‌شود
        Result(await OpsAsync(op, shipmentId, new { op = "add", clientOpId = "again", baleNumber = 1, serial = "3778" }));
        Assert.Equal(3, (await RowsAsync(op, shipmentId)).Count);

        // اندازهٔ نهایی: عمل measure با عدد، «همان اندازه» در عمل، و PUT /final-size
        var rowA = await RowAsync(op, shipmentId, Id(a));
        var measured = Result(await OpsAsync(op, shipmentId, new
        {
            op = "measure", clientOpId = "m", rugId = Id(a), rugVersion = rowA.GetProperty("rugVersion").GetUInt32(),
            finalLength = 2.05m, finalWidth = 2.10m
        })).GetProperty("row");
        Assert.Equal(-3, measured.GetProperty("sizeChangeCm").GetInt32()); // ↓۳ سانت
        Result(await OpsAsync(op, shipmentId, SameAsIntake(await RowAsync(op, shipmentId, Id(lot)))));
        var freshC = await GetJsonAsync(op, $"/api/rugs/{Id(c)}");
        await SendAsync(op, HttpMethod.Put, $"/api/rugs/{Id(c)}/final-size", new { sameAsIntake = true, version = Version(freshC) });

        // ردیف‌ها با قاعدهٔ دسته
        rowA = await RowAsync(op, shipmentId, Id(a));
        Assert.Equal(4.31m, Dec(rowA, "areaM2"));            // 2.05 × 2.10 = 4.305 → 4.31 (نیمه دور از صفر)
        Assert.Equal(45.55m, Dec(rowA, "usdPerM2"));
        Assert.Equal(1, rowA.GetProperty("rateSource").GetInt32()); // از جدول
        Assert.Equal(196.32m, Dec(rowA, "lineUsd"));         // 4.31 × 45.55 = 196.3205
        var rowLot = await RowAsync(op, shipmentId, Id(lot));
        Assert.Equal(0.16m, Dec(rowLot, "areaM2"));
        Assert.Equal(8.00m, Dec(rowLot, "lineAreaM2"));
        Assert.Equal(7.29m, Dec(rowLot, "pieceUsd"));        // 0.16 × 45.55 = 7.288
        Assert.Equal(364.50m, Dec(rowLot, "lineUsd"));       // نه 364.40
        var rowC = await RowAsync(op, shipmentId, Id(c));
        Assert.Equal(2.90m, Dec(rowC, "areaM2"));            // 1.93 × 1.50 = 2.895 → 2.90
        Assert.Equal(174.00m, Dec(rowC, "lineUsd"));

        // جمع نگله‌ها و محموله از همان ارزش‌ها
        var shipment = await ShipmentAsync(op, shipmentId);
        var bales = shipment.GetProperty("bales");
        Assert.Equal((1, 2, 2, 7.21m, 370.32m), Summary(bales[0]));
        Assert.Equal((2, 1, 50, 8.00m, 364.50m), Summary(bales[1]));
        var totals = shipment.GetProperty("totals");
        Assert.Equal(2, totals.GetProperty("bales").GetInt32());
        Assert.Equal(3, totals.GetProperty("lines").GetInt32());
        Assert.Equal(52, totals.GetProperty("pieces").GetInt32());
        Assert.Equal(15.21m, Dec(totals, "areaM2"));
        Assert.Equal(734.82m, Dec(totals, "usdTotal"));
        Assert.DoesNotContain(shipment.GetProperty("readiness").EnumerateArray(), i => i.GetProperty("blocking").GetBoolean());

        var listed = (await GetJsonAsync(op, "/api/shipments")).EnumerateArray().Single(s => s.GetProperty("id").GetGuid() == shipmentId);
        Assert.Equal((2, 3, 52, 15.21m, 734.82m, 0), (listed.GetProperty("bales").GetInt32(), listed.GetProperty("lines").GetInt32(),
            listed.GetProperty("pieces").GetInt32(), Dec(listed, "areaM2"), Dec(listed, "usdTotal"), listed.GetProperty("readinessBlocking").GetInt32()));

        // RugDto فرشِ داخل نگله: محموله، نگله، دلار و جای «در نگله» (enricher)
        var rugA = await GetJsonAsync(op, $"/api/rugs/{Id(a)}");
        Assert.Equal(shipmentId, rugA.GetProperty("shipmentId").GetGuid());
        Assert.Equal(shipment.GetProperty("number").GetInt32(), rugA.GetProperty("shipmentNumber").GetInt32());
        Assert.Equal(bale1, rugA.GetProperty("baleId").GetGuid());
        Assert.Equal(1, rugA.GetProperty("baleNumber").GetInt32());
        Assert.Equal(45.55m, Dec(rugA, "usdPerM2"));
        Assert.Equal(196.32m, Dec(rugA, "lineUsd"));
        Assert.Equal(1, rugA.GetProperty("location").GetInt32());
        var rugStock = await GetJsonAsync(op, $"/api/rugs/{Id(stock)}");
        Assert.Equal(0, rugStock.GetProperty("location").GetInt32());
        Assert.Equal(JsonValueKind.Null, rugStock.GetProperty("shipmentId").ValueKind);

        // فیلترهای /api/rugs (RugRepository)
        var inShipment = await GetJsonAsync(op, $"/api/rugs?shipmentId={shipmentId}");
        Assert.Equal(3, inShipment.GetProperty("totalCount").GetInt32());
        Assert.All(inShipment.GetProperty("items").EnumerateArray(), r =>
        {
            Assert.Equal(1, r.GetProperty("location").GetInt32());
            Assert.Equal(shipment.GetProperty("number").GetInt32(), r.GetProperty("shipmentNumber").GetInt32());
        });
        var inBale2 = await GetJsonAsync(op, $"/api/rugs?baleId={bales[1].GetProperty("id").GetGuid()}");
        Assert.Equal(Id(lot), inBale2.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetGuid());
        Assert.Equal(2, inBale2.GetProperty("items")[0].GetProperty("baleNumber").GetInt32());
        var notBaled = await GetJsonAsync(op, "/api/rugs?notBaled=true");
        Assert.Equal([Id(stock)], notBaled.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("id").GetGuid()));
        Assert.Equal(3, (await GetJsonAsync(op, "/api/rugs?location=1")).GetProperty("totalCount").GetInt32());
        Assert.Equal(1, (await GetJsonAsync(op, "/api/rugs?location=0")).GetProperty("totalCount").GetInt32());
        Assert.Equal(0, (await GetJsonAsync(op, "/api/rugs?location=2")).GetProperty("totalCount").GetInt32());

        // جست‌وجوی شماره هم جای فرش را می‌گوید
        var found = await GetJsonAsync(op, $"/api/shipments/{shipmentId}/find-rug?serial=3920");
        Assert.Equal("inRange", found.GetProperty("matchType").GetString());
        Assert.Equal(2, found.GetProperty("inBale").GetProperty("baleNo").GetInt32());
        Assert.True(found.GetProperty("inBale").GetProperty("sameShipment").GetBoolean());
    }

    [PostgresFact]
    public async Task ARugInAnotherOpenShipment_AsksBeforeMoving_AndRemoveReturnsItToStock()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var op = P1Client.As(factory, t.Operator);
        var rug = await RugAsync(merchant, "4101", 2m, 3m, "گبه");
        var seven = await NewShipmentAsync(merchant);
        var eight = await NewShipmentAsync(merchant);
        Result(await OpsAsync(op, seven, Add(Id(rug), 3)));

        // در محمولهٔ باز دیگر است: اول می‌پرسد «منتقل شود؟» و چیزی عوض نمی‌شود
        var asked = Result(await OpsAsync(op, eight, new { op = "add", clientOpId = "q", baleNumber = 1, serial = "4101" }), ok: false);
        var prompt = asked.GetProperty("prompt");
        Assert.Equal("Q_LINE_MOVE", prompt.GetProperty("code").GetString());
        Assert.Equal(3, prompt.GetProperty("baleNumber").GetInt32());
        Assert.Contains("منتقل شود؟", prompt.GetProperty("message").GetString());
        Assert.Empty((await BalesAsync(op, eight)).EnumerateArray());

        // با تأیید منتقل می‌شود؛ نگلهٔ خالیِ محمولهٔ قبلی می‌ماند (آمادگی آن را می‌گوید)
        var moved = Result(await OpsAsync(op, eight, new { op = "add", clientOpId = "m", baleNumber = 1, serial = "4101", move = true }));
        Assert.Equal(1, moved.GetProperty("row").GetProperty("baleNumber").GetInt32());
        Assert.Empty((await RowsAsync(op, seven)));
        Assert.Contains((await ShipmentAsync(op, seven)).GetProperty("readiness").EnumerateArray(),
            i => i.GetProperty("code").GetString() == "E_EMPTY_BALE" && i.GetProperty("baleNumber").GetInt32() == 3);
        Assert.Equal(eight, (await GetJsonAsync(op, $"/api/rugs/{Id(rug)}")).GetProperty("shipmentId").GetGuid());

        // «خارج کردن از نگله»: ردیف پیش از خروج برمی‌گردد (برای «برگردان») و فرش به انبار برمی‌گردد
        var row = await RowAsync(op, eight, Id(rug));
        var removed = Result(await OpsAsync(op, eight, new
        {
            op = "remove", clientOpId = "rm", lineId = row.GetProperty("lineId").GetGuid(), lineVersion = row.GetProperty("lineVersion").GetUInt32()
        })).GetProperty("row");
        Assert.Equal(1, removed.GetProperty("seq").GetInt32());
        var back = await GetJsonAsync(op, $"/api/rugs/{Id(rug)}");
        Assert.Equal(0, back.GetProperty("location").GetInt32());
        Assert.Equal(JsonValueKind.Null, back.GetProperty("shipmentId").ValueKind);

        // «برگردان»: همان نگله و همان Seq
        var undo = Result(await OpsAsync(op, eight, new { op = "add", clientOpId = "u", baleNumber = 1, rugId = Id(rug), seq = 1 }));
        Assert.Equal(1, undo.GetProperty("row").GetProperty("seq").GetInt32());
    }

    // ═════════════════════════════════════════════════════════════════════
    // ارزش گمرکی
    // ═════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task DeclaredValue_ComesFromTheTable_FollowsTheOrigin_AndTheKeyFieldOrFallbackRevaluesOpenLines()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var admin = P1Client.As(factory, t.Admin);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var op = P1Client.As(factory, t.Operator);

        await ReplaceRatesAsync(admin, ("گبه", 45m), ("کاشان", 60m));
        var r1 = await RugAsync(merchant, "4201", 2m, 3m, "گبه");
        var r2 = await RugAsync(merchant, "4202", 2m, 3m, "قم", pattern: "کاشان");
        var shipmentId = await NewShipmentAsync(merchant);
        var both = await OpsAsync(op, shipmentId, Add(Id(r1), 1), Add(Id(r2), 1));
        Result(both, 0);
        Result(both, 1);

        var row1 = await RowAsync(op, shipmentId, Id(r1));
        Assert.Equal((1, 45m, 270m), Value(row1));       // از جدول: ۶ متر × ۴۵
        Assert.Equal(3, (await RowAsync(op, shipmentId, Id(r2))).GetProperty("rateSource").GetInt32()); // ندارد
        var r1Version = Version(await GetJsonAsync(op, $"/api/rugs/{Id(r1)}"));

        // محل بافت عوض شد → نرخ از جدول دوباره (محمولهٔ باز)
        var edited = await SendAsync(merchant, HttpMethod.Put, $"/api/rugs/{Id(r1)}",
            new { values = Values(("rug.origin", "کاشان")), version = r1Version });
        Assert.Equal(360m, Dec(edited, "lineUsd"));
        Assert.Equal((1, 60m, 360m), Value(await RowAsync(op, shipmentId, Id(r1))));
        r1Version = Version(edited);

        // نرخ پیش‌فرض → خطی که در جدول نبود «نرخ پیش‌فرض» می‌گیرد؛ خط جدول دست نمی‌خورد
        await SetPreferencesAsync(admin, p => p["declaredValueFallbackUsd"] = 10m);
        Assert.Equal((2, 10m, 60m), Value(await RowAsync(op, shipmentId, Id(r2))));
        Assert.Equal((1, 60m, 360m), Value(await RowAsync(op, shipmentId, Id(r1))));

        // کلید ارزش گمرکی «نقشه» → r2 با نقشهٔ «کاشان» از جدول، r1 بی نقشه نرخ پیش‌فرض
        await SetPreferencesAsync(admin, p => p["declaredValueKeyField"] = "rug.pattern");
        Assert.Equal((1, 60m, 360m), Value(await RowAsync(op, shipmentId, Id(r2))));
        Assert.Equal((2, 10m, 60m), Value(await RowAsync(op, shipmentId, Id(r1))));

        // نوشتن ردیف‌های مشتق‌شده نسخهٔ فرش را عوض نمی‌کند (B.1، L.2)
        Assert.Equal(r1Version, Version(await GetJsonAsync(op, $"/api/rugs/{Id(r1)}")));

        // مهر کهنهٔ جدول ← ۴۰۹ با پیام فارسی
        var stamp = await StampAsync(admin);
        var stale = await SendAsync(admin, HttpMethod.Put, "/api/settings/declared-values",
            new { rates = Array.Empty<object>(), stamp = stamp - 1 }, HttpStatusCode.Conflict);
        Assert.Equal("E_DECLARED_STALE", stale.GetProperty("code").GetString());
        Assert.Equal(stamp, stale.GetProperty("stamp").GetInt32());
        Assert.Contains("جدول ارزش گمرکی", stale.GetProperty("message").GetString());
        Assert.Equal(2, (await GetJsonAsync(op, "/api/declared-values")).GetProperty("rates").GetArrayLength());
    }

    [PostgresFact]
    public async Task AMerchantFixesANewOrigin_InOneAction_AndItsTwentyOpenLinesAreRevalued()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var op = P1Client.As(factory, t.Operator);

        var kashan = new List<Guid>();
        for (var i = 0; i < 20; i++)
            kashan.Add(Id(await RugAsync(merchant, (6001 + i).ToString(System.Globalization.CultureInfo.InvariantCulture), 1m, 1.5m, "کاشان")));
        var qom = Id(await RugAsync(merchant, "6100", 1m, 1m, "قم"));
        var shipmentId = await NewShipmentAsync(merchant);
        var batch = await OpsAsync(op, shipmentId, [.. kashan.Select(id => Add(id, 1 + kashan.IndexOf(id) / 10)), Add(qom, 3)]);
        Assert.All(batch.GetProperty("results").EnumerateArray(), r => Assert.True(r.GetProperty("ok").GetBoolean(), r.ToString()));
        var versions = new Dictionary<Guid, uint>();
        foreach (var id in kashan)
            versions[id] = Version(await GetJsonAsync(op, $"/api/rugs/{id}"));

        // تراشهٔ «ارزش گمرکی ۲۰ فرش «کاشان» معلوم نیست» با کلید، برای فرم «نرخ بگذارید»
        var readiness = await GetJsonAsync(op, $"/api/shipments/{shipmentId}/readiness");
        var chip = readiness.EnumerateArray().Single(i => i.GetProperty("code").GetString() == "E_NO_DECLARED_VALUE"
                                                          && i.GetProperty("key").GetString() == "کاشان");
        Assert.Equal(20, chip.GetProperty("count").GetInt32());
        Assert.True(chip.GetProperty("blocking").GetBoolean());
        Assert.Contains("۲۰", chip.GetProperty("message").GetString());

        // یک کار: «كاشان» با کاف عربی همان کلید است؛ هر ۲۰ خط باز دوباره ارزش‌گذاری می‌شوند و «قم» نه
        var stamp = await StampAsync(merchant);
        var added = await SendAsync(merchant, HttpMethod.Post, "/api/declared-values", new { keyValue = "كاشان", usdPerM2 = 40m, stamp });
        Assert.Equal(20, added.GetProperty("affectedOpenLines").GetInt32());
        Assert.Equal(stamp + 1, added.GetProperty("stamp").GetInt32());
        var rows = await RowsAsync(op, shipmentId);
        Assert.All(rows.Where(r => kashan.Contains(r.GetProperty("rugId").GetGuid())), r =>
        {
            Assert.Equal(1, r.GetProperty("rateSource").GetInt32());
            Assert.Equal(60.00m, Dec(r, "lineUsd")); // 1.5 × 40
        });
        Assert.Equal(3, rows.Single(r => r.GetProperty("rugId").GetGuid() == qom).GetProperty("rateSource").GetInt32());
        readiness = await GetJsonAsync(op, $"/api/shipments/{shipmentId}/readiness");
        Assert.Equal(["قم"], readiness.EnumerateArray()
            .Where(i => i.GetProperty("code").GetString() == "E_NO_DECLARED_VALUE")
            .Select(i => i.GetProperty("key").GetString()));

        // کلید موجود ۴۰۹ (ویرایش در تنظیمات است)، نرخ منفی ۴۰۰، مهر کهنه ۴۰۹
        stamp = added.GetProperty("stamp").GetInt32();
        var exists = await SendAsync(merchant, HttpMethod.Post, "/api/declared-values",
            new { keyValue = "کاشان", usdPerM2 = 50m, stamp }, HttpStatusCode.Conflict);
        Assert.Contains("کاشان", exists.GetProperty("message").GetString());
        var negative = await SendAsync(merchant, HttpMethod.Post, "/api/declared-values",
            new { keyValue = "قم", usdPerM2 = -1m, stamp }, HttpStatusCode.BadRequest);
        Assert.Equal("ارزش گمرکی منفی نمی‌شود.", negative.GetProperty("message").GetString());
        await SendAsync(merchant, HttpMethod.Post, "/api/declared-values",
            new { keyValue = "قم", usdPerM2 = 30m, stamp = stamp - 1 }, HttpStatusCode.Conflict);

        // نوشتن ارزش‌ها نسخهٔ هیچ فرشی را عوض نکرد
        foreach (var id in kashan)
            Assert.Equal(versions[id], Version(await GetJsonAsync(op, $"/api/rugs/{id}")));
    }

    // ═════════════════════════════════════════════════════════════════════
    // حذف و شماره‌گذاری دوبارهٔ نگله، حذف محموله
    // ═════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task Bales_DeleteOnlyWhenEmpty_RenumberIsOneFlush_AndAShipmentIsDeletedOnlyOpenAndLineFree()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var op = P1Client.As(factory, t.Operator);
        var rug = await RugAsync(merchant, "4301", 2m, 3m, "گبه");
        var shipmentId = await NewShipmentAsync(merchant);

        // نگله‌های ۲ تا ۱۱ (عدد تایپ‌شده)؛ فرش در نگلهٔ ۵
        foreach (var number in Enumerable.Range(2, 10))
            Assert.Equal(number, (await SendAsync(op, HttpMethod.Post, $"/api/shipments/{shipmentId}/bales", new { number })).GetProperty("number").GetInt32());
        await SendAsync(op, HttpMethod.Post, $"/api/shipments/{shipmentId}/bales", new { number = 5 }, HttpStatusCode.Conflict);
        Result(await OpsAsync(op, shipmentId, Add(Id(rug), 5)));
        var bales = (await BalesAsync(op, shipmentId)).EnumerateArray().ToList();
        var five = bales.Single(b => b.GetProperty("number").GetInt32() == 5);
        var two = bales.Single(b => b.GetProperty("number").GetInt32() == 2);

        // حذف نگله فقط خالی؛ محموله فقط بی‌خط
        var notEmpty = await SendAsync(op, HttpMethod.Delete,
            $"/api/bales/{five.GetProperty("id").GetGuid()}?version={five.GetProperty("version").GetUInt32()}", null, HttpStatusCode.Conflict);
        Assert.Contains("خالی نیست", notEmpty.GetProperty("message").GetString());
        await SendAsync(op, HttpMethod.Delete,
            $"/api/bales/{two.GetProperty("id").GetGuid()}?version={two.GetProperty("version").GetUInt32()}", null, HttpStatusCode.NoContent);
        var full = await SendAsync(merchant, HttpMethod.Delete,
            $"/api/shipments/{shipmentId}?version={await ShipmentVersionAsync(merchant, shipmentId)}", null, HttpStatusCode.Conflict);
        Assert.Contains("فرش دارد", full.GetProperty("message").GetString());

        // شماره‌گذاری دوباره: ۳..۱۱ ← ۱..۹ در یک flush (قید تعویق‌پذیر؛ ترتیب UPDATEها هر چه باشد)
        var version = await ShipmentVersionAsync(op, shipmentId);
        var renumbered = await SendAsync(op, HttpMethod.Post, $"/api/shipments/{shipmentId}/bales/renumber", new { version });
        Assert.Equal(Enumerable.Range(1, 9), renumbered.EnumerateArray().Select(b => b.GetProperty("number").GetInt32()));
        Assert.Equal(3, (await RowAsync(op, shipmentId, Id(rug))).GetProperty("baleNumber").GetInt32()); // ۵ ← ۳
        await SendAsync(op, HttpMethod.Post, $"/api/shipments/{shipmentId}/bales/renumber", new { version }, HttpStatusCode.Conflict);

        await using (var connection = new NpgsqlConnection(Connection()))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                """SELECT condeferrable, condeferred FROM pg_constraint WHERE conname = 'UX_Bales_Shipment_Number'""", connection);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.True(reader.GetBoolean(0));
            Assert.True(reader.GetBoolean(1));
        }

        // شمارهٔ تکراری با PUT ← ۴۰۹
        var one = renumbered[0];
        await SendAsync(op, HttpMethod.Put, $"/api/bales/{one.GetProperty("id").GetGuid()}",
            new { number = 2, weightKg = (decimal?)null, notes = (string?)null, version = one.GetProperty("version").GetUInt32() },
            HttpStatusCode.Conflict);

        // بی‌خط شد → حذف محموله با نگله‌های خالی‌اش
        var row = await RowAsync(op, shipmentId, Id(rug));
        Result(await OpsAsync(op, shipmentId, new
        {
            op = "remove", clientOpId = "rm", lineId = row.GetProperty("lineId").GetGuid(), lineVersion = row.GetProperty("lineVersion").GetUInt32()
        }));
        await SendAsync(merchant, HttpMethod.Delete,
            $"/api/shipments/{shipmentId}?version={await ShipmentVersionAsync(merchant, shipmentId)}", null, HttpStatusCode.NoContent);
        await GetJsonAsync(op, $"/api/shipments/{shipmentId}", HttpStatusCode.NotFound);
        await using var db = factory.CreateDbContext();
        Assert.False(await db.Set<Bale>().AsNoTracking().AnyAsync(b => b.ShipmentId == shipmentId));
        Assert.Equal(0, (await GetJsonAsync(op, $"/api/rugs/{Id(rug)}")).GetProperty("location").GetInt32());
    }

    // ═════════════════════════════════════════════════════════════════════
    // «قطعی»، عکس و «باز کردن دوباره»
    // ═════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task Reopen_RevaluesFromTheCurrentTable_AndTheSnapshotHashIsStableAndReHashable()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var admin = P1Client.As(factory, t.Admin);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var op = P1Client.As(factory, t.Operator);

        await ReplaceRatesAsync(admin, ("گبه", 45m));
        var rug = await RugAsync(merchant, "4401", 2m, 3m, "گبه");
        var shipmentId = await NewShipmentAsync(merchant);
        Result(await OpsAsync(op, shipmentId, Add(Id(rug), 1)));
        Result(await OpsAsync(op, shipmentId, SameAsIntake(await RowAsync(op, shipmentId, Id(rug)))));
        var statusBefore = (await GetJsonAsync(op, $"/api/rugs/{Id(rug)}")).GetProperty("status").GetInt32();

        var first = await FinalizeAsync(merchant, shipmentId);
        var hash = first.GetProperty("snapshotHash").GetString()!;
        Assert.Equal(64, hash.Length);
        Assert.Equal(1, first.GetProperty("finalizeCount").GetInt32());
        var exported = await GetJsonAsync(op, $"/api/rugs/{Id(rug)}");
        Assert.Equal(5, exported.GetProperty("status").GetInt32());   // صادر شده
        Assert.Equal(2, exported.GetProperty("location").GetInt32());

        // هش دوبارهٔ متن ذخیره‌شده همان هش است (ستون text، نه jsonb)؛ عکس هیچ مبلغ تومانی ندارد
        var stored = await StoredSnapshotAsync(shipmentId);
        Assert.Equal(hash, stored.SnapshotHash);
        Assert.Equal(hash, Sha256(stored.SnapshotJson!));
        Assert.Contains("\"usdTotal\":270.00", stored.SnapshotJson);
        Assert.DoesNotContain("purchase", stored.SnapshotJson, StringComparison.OrdinalIgnoreCase);

        // جدول در حالی که قطعی است عوض شد: ارزش‌های قطعی منجمدند (هیچ خط بازی حساب نشد)
        Assert.Equal(0, (await ReplaceRatesAsync(admin, ("گبه", 50m))).GetProperty("affectedOpenLines").GetInt32());
        Assert.Equal(270m, Dec(await RowAsync(op, shipmentId, Id(rug)), "lineUsd"));

        // «باز کردن دوباره»: قفل می‌رود، عکس به «عکس قبلی»، فرش به وضعیت گردش کار، ارزش از جدول فعلی
        var reopened = await SendAsync(merchant, HttpMethod.Post, $"/api/shipments/{shipmentId}/reopen",
            new { version = await ShipmentVersionAsync(merchant, shipmentId), reason = "نرخ تازه" });
        Assert.Equal(0, reopened.GetProperty("status").GetInt32());
        Assert.Equal(JsonValueKind.Null, reopened.GetProperty("snapshotHash").ValueKind);
        Assert.Equal((1, 50m, 300m), Value(await RowAsync(op, shipmentId, Id(rug))));
        var back = await GetJsonAsync(op, $"/api/rugs/{Id(rug)}");
        Assert.Equal(statusBefore, back.GetProperty("status").GetInt32());
        Assert.Equal(1, back.GetProperty("location").GetInt32());
        var afterReopen = await StoredSnapshotAsync(shipmentId);
        Assert.Null(afterReopen.SnapshotJson);
        Assert.Equal(stored.SnapshotJson, afterReopen.LastSnapshotJson);

        // هش با «قطعی» دوباره بی تغییر پایدار است؛ با دادهٔ تازه عوض می‌شود
        var second = await FinalizeAsync(merchant, shipmentId);
        Assert.Equal(2, second.GetProperty("finalizeCount").GetInt32());
        Assert.NotEqual(hash, second.GetProperty("snapshotHash").GetString());
        await SendAsync(merchant, HttpMethod.Post, $"/api/shipments/{shipmentId}/reopen",
            new { version = await ShipmentVersionAsync(merchant, shipmentId), reason = (string?)null });
        var third = await FinalizeAsync(merchant, shipmentId);
        Assert.Equal(3, third.GetProperty("finalizeCount").GetInt32());
        Assert.Equal(second.GetProperty("snapshotHash").GetString(), third.GetProperty("snapshotHash").GetString());
        Assert.Equal(third.GetProperty("snapshotHash").GetString(), Sha256((await StoredSnapshotAsync(shipmentId)).SnapshotJson!));
    }

    // ── کمک‌ها ──────────────────────────────────────────────────────────────

    private static (int Number, int Lines, int Pieces, decimal Area, decimal Usd) Summary(JsonElement bale)
        => (bale.GetProperty("number").GetInt32(), bale.GetProperty("lines").GetInt32(), bale.GetProperty("pieces").GetInt32(),
            Dec(bale, "areaM2"), Dec(bale, "usdTotal"));

    private static (int Source, decimal Rate, decimal LineUsd) Value(JsonElement row)
        => (row.GetProperty("rateSource").GetInt32(), Dec(row, "usdPerM2"), Dec(row, "lineUsd"));

    private static async Task<JsonElement> FinalizeAsync(P1Client merchant, Guid shipmentId)
    {
        var result = await SendAsync(merchant, HttpMethod.Post, $"/api/shipments/{shipmentId}/finalize",
            new { version = await ShipmentVersionAsync(merchant, shipmentId) });
        Assert.True(result.GetProperty("ok").GetBoolean(), result.ToString());
        return result.GetProperty("shipment");
    }

    private async Task<Shipment> StoredSnapshotAsync(Guid shipmentId)
    {
        await using var db = factory.CreateDbContext();
        return await db.Set<Shipment>().AsNoTracking().SingleAsync(s => s.Id == shipmentId);
    }

    private static string Sha256(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private string Connection()
    {
        using var db = factory.CreateDbContext();
        return db.Database.GetConnectionString()!;
    }
}
