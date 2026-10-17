using System.Net;
using System.Text.Json;
using RugsManagment.IntegrationTests.P1;
using RugsManagment.IntegrationTests.P2;
using RugsManagment.IntegrationTests.Support;
using static RugsManagment.IntegrationTests.P2.P2Http;

namespace RugsManagment.IntegrationTests.P4aWeb;

/// <summary>
/// لایهٔ وب فاز ۴a (I.1، I.2، I.4): درهای API محموله، نگله و ارزش گمرکی با سیاست درستِ هر نقش، و قاب صفحه‌های
/// محموله و جدول ارزش گمرکی. قاعده‌های خودِ دامنه (قفل، آمادگی، تصویر قطعی) آزمون‌های فاز ۴a دامنه را دارند؛ این‌جا فقط
/// «چه کسی به کدام در می‌رسد و صفحه چه نشان می‌دهد».
///
/// چرا این‌ها: انباردار (Floor) نگله‌بندی می‌کند و اندازهٔ نهایی می‌نویسد ولی محموله نمی‌سازد و قطعی نمی‌کند؛ بازرگان
/// (Business) محموله را می‌سازد، قطعی می‌کند و کلید تازهٔ ارزش گمرکی را خودش اضافه می‌کند؛ ویرایش کل جدول فقط
/// تنظیم‌کننده (Configure). نرخ دستی یک ردیف هم Business است، ولی رد شدنش بقیهٔ کارهای همان درخواست را نمی‌اندازد.
/// </summary>
public sealed class P4aWebTests(RugsWebFactory factory) : IClassFixture<RugsWebFactory>
{
    [PostgresFact]
    public async Task ShipmentDoors_FollowTheRoles_AndTheWorkspaceRendersTheShell()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var admin = P1Client.As(factory, t.Admin);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var op = P1Client.As(factory, t.Operator);

        // فهرست خالی: انباردار دکمهٔ ساختن ندارد و متن خودش را می‌بیند؛ بازرگان «محمولهٔ تازه» دارد
        var opList = await HtmlAsync(op, "/Shipments");
        Assert.Contains("هنوز محمولهٔ بازی نیست؛ از بازرگان بخواهید یکی بسازد.", opList);
        Assert.DoesNotContain("data-post-json", opList);
        var merchantList = await HtmlAsync(merchant, "/Shipments");
        Assert.Contains("data-post-json=\"/api/shipments\"", merchantList);
        Assert.Contains("محمولهٔ تازه", merchantList);

        // ساختن: فقط Business؛ بی بدنه = همه پیش‌فرض (C.1)
        await SendAsync(op, HttpMethod.Post, "/api/shipments", null, HttpStatusCode.Forbidden);
        var shipment = await SendAsync(merchant, HttpMethod.Post, "/api/shipments", null);
        var id = shipment.GetProperty("id").GetGuid();
        Assert.Equal(0, shipment.GetProperty("status").GetInt32());
        Assert.Equal("اظهار ۱", shipment.GetProperty("title").GetString());
        Assert.NotEqual("0001-01-01", shipment.GetProperty("documentDate").GetString()); // تاریخ خالی = امروزِ ایران

        // خواندن برای همه؛ فهرست با فیلتر وضعیت
        var listed = await GetJsonAsync(op, "/api/shipments");
        Assert.Contains(listed.EnumerateArray(), s => s.GetProperty("id").GetGuid() == id);
        Assert.Empty((await GetJsonAsync(op, "/api/shipments?status=Finalized")).EnumerateArray());
        Assert.Single((await GetJsonAsync(op, "/api/shipments?status=0")).EnumerateArray());
        Assert.Contains($"data-shipment-id=\"{id}\"", await HtmlAsync(op, "/Shipments?status=open"));
        Assert.DoesNotContain($"data-shipment-id=\"{id}\"", await HtmlAsync(op, "/Shipments?status=finalized"));

        // کارگاه: قاب سربرگ (میزبان جزیرهٔ shipment-header) و نوار زبانه یا خلاصهٔ نگله‌ها؛ زبانهٔ ناشناخته صفحه را نمی‌شکند
        var workspace = await HtmlAsync(op, $"/Shipments/{id}");
        Assert.Contains("data-island=\"shipment-header\"", workspace);
        Assert.Contains($"data-shipment-workspace=\"{id}\"", workspace);
        Assert.Contains("محمولهٔ ۱", workspace);
        Assert.Contains("data-readiness=\"E_EMPTY_SHIPMENT\"", workspace);
        await HtmlAsync(op, $"/Shipments/{id}?tab=nope");
        await HtmlAsync(op, $"/Shipments/{Guid.NewGuid()}", HttpStatusCode.NotFound);
        await GetJsonAsync(op, $"/api/shipments/{Guid.NewGuid()}", HttpStatusCode.NotFound);

        // سربرگ، حذف، قطعی کردن و باز کردن دوباره: فقط Business
        var version = shipment.GetProperty("version").GetUInt32();
        await SendAsync(op, HttpMethod.Put, $"/api/shipments/{id}", Header(shipment, version), HttpStatusCode.Forbidden);
        await SendAsync(op, HttpMethod.Delete, $"/api/shipments/{id}", null, HttpStatusCode.Forbidden);
        await SendAsync(op, HttpMethod.Post, $"/api/shipments/{id}/finalize", new { version }, HttpStatusCode.Forbidden);
        await SendAsync(op, HttpMethod.Post, $"/api/shipments/{id}/reopen", new { version, reason = (string?)null }, HttpStatusCode.Forbidden);

        // «قطعی کن» با کمبود: ۲۰۰ با ok = false و فهرست کامل، هیچ چیز عوض نمی‌شود
        var blocked = await SendAsync(merchant, HttpMethod.Post, $"/api/shipments/{id}/finalize", new { version });
        Assert.False(blocked.GetProperty("ok").GetBoolean());
        Assert.Contains(blocked.GetProperty("issues").EnumerateArray(), i => i.GetProperty("code").GetString() == "E_EMPTY_SHIPMENT");
        Assert.Equal(0, blocked.GetProperty("shipment").GetProperty("status").GetInt32());
        var readiness = await GetJsonAsync(op, $"/api/shipments/{id}/readiness");
        Assert.Contains(readiness.EnumerateArray(), i => i.GetProperty("code").GetString() == "E_EMPTY_SHIPMENT");

        // نگله: Floor — انباردار می‌سازد و نگلهٔ خالی را حذف می‌کند؛ حذف بی نسخه ۴۰۰
        var bale = await SendAsync(op, HttpMethod.Post, $"/api/shipments/{id}/bales", null);
        Assert.Equal(1, bale.GetProperty("number").GetInt32());
        var baleId = bale.GetProperty("id").GetGuid();
        await SendAsync(op, HttpMethod.Delete, $"/api/bales/{baleId}", null, HttpStatusCode.BadRequest);
        await SendAsync(op, HttpMethod.Delete, $"/api/bales/{baleId}?version={bale.GetProperty("version").GetUInt32()}", null,
            HttpStatusCode.NoContent);
        Assert.Empty((await GetJsonAsync(op, $"/api/shipments/{id}/bales")).EnumerateArray());

        // حذف محمولهٔ باز و بی‌فرش: Business
        var fresh = await GetJsonAsync(merchant, $"/api/shipments/{id}");
        await SendAsync(merchant, HttpMethod.Delete, $"/api/shipments/{id}?version={fresh.GetProperty("version").GetUInt32()}", null,
            HttpStatusCode.NoContent);
        await GetJsonAsync(admin, $"/api/shipments/{id}", HttpStatusCode.NotFound);
    }

    [PostgresFact]
    public async Task Lines_AreFloor_ButTheRateOverrideIsBusiness_AndARefusedOpDoesNotSinkTheOthers()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var op = P1Client.As(factory, t.Operator);

        var rug = await CreateRugAsync(merchant, Values(
            ("rug.serial", "5101"), ("rug.qty", 1), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m), ("rug.origin", "کاشان")));
        var rugId = Id(rug);
        var shipmentId = (await SendAsync(merchant, HttpMethod.Post, "/api/shipments", null)).GetProperty("id").GetGuid();

        // پیدا کردن با شماره
        var found = await GetJsonAsync(op, $"/api/shipments/{shipmentId}/find-rug?serial=5101");
        Assert.Equal("exact", found.GetProperty("matchType").GetString());
        Assert.Equal(JsonValueKind.Null, found.GetProperty("inBale").ValueKind);
        await GetJsonAsync(op, $"/api/shipments/{shipmentId}/find-rug?serial=99999", HttpStatusCode.NotFound);
        await GetJsonAsync(op, $"/api/shipments/{shipmentId}/find-rug", HttpStatusCode.BadRequest);

        // انباردار با شماره به نگلهٔ ۱ (که هنوز نیست) اضافه می‌کند: نگله با اولین فرشش ساخته می‌شود (C.2)
        var added = await SendAsync(op, HttpMethod.Post, $"/api/shipments/{shipmentId}/lines/batch",
            new { ops = new object[] { new { op = "add", clientOpId = "a1", baleNumber = 1, serial = "5101" } } });
        var addResult = added.GetProperty("results")[0];
        Assert.True(addResult.GetProperty("ok").GetBoolean(), addResult.ToString());
        Assert.Equal(1, addResult.GetProperty("row").GetProperty("baleNumber").GetInt32());
        Assert.Single(added.GetProperty("bales").EnumerateArray());
        Assert.Equal(1, added.GetProperty("totals").GetProperty("pieces").GetInt32());

        // انباردار: نرخ دستی رد می‌شود (پیام فارسی همان عمل)، ولی اندازهٔ نهاییِ همان درخواست ثبت می‌شود
        var row = await RowAsync(op, shipmentId, rugId);
        var mixed = await SendAsync(op, HttpMethod.Post, $"/api/shipments/{shipmentId}/lines/batch", new
        {
            ops = new object[]
            {
                new { op = "rate", clientOpId = "r1", lineId = row.GetProperty("lineId").GetGuid(), lineVersion = row.GetProperty("lineVersion").GetUInt32(), usdPerM2Override = 50m },
                new { op = "measure", clientOpId = "m1", rugId, rugVersion = row.GetProperty("rugVersion").GetUInt32(), sameAsIntake = true }
            }
        });
        var rate = mixed.GetProperty("results")[0];
        Assert.False(rate.GetProperty("ok").GetBoolean());
        Assert.Equal("E_LINE_RATE_FORBIDDEN", rate.GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.Equal("نرخ دلار هر ردیف را فقط بازرگان عوض می‌کند.", rate.GetProperty("errors")[0].GetProperty("message").GetString());
        Assert.True(mixed.GetProperty("results")[1].GetProperty("ok").GetBoolean(), mixed.ToString());

        row = await RowAsync(op, shipmentId, rugId);
        Assert.Equal(2m, row.GetProperty("finalLength").GetDecimal());
        Assert.Equal(3m, row.GetProperty("finalWidth").GetDecimal());

        // بازرگان: همان نرخ دستی پذیرفته می‌شود (منبع «دستی»)
        var overridden = await SendAsync(merchant, HttpMethod.Post, $"/api/shipments/{shipmentId}/lines/batch", new
        {
            ops = new object[]
            {
                new { op = "rate", clientOpId = "r2", lineId = row.GetProperty("lineId").GetGuid(), lineVersion = row.GetProperty("lineVersion").GetUInt32(), usdPerM2Override = 50m }
            }
        });
        var ok = overridden.GetProperty("results")[0];
        Assert.True(ok.GetProperty("ok").GetBoolean(), ok.ToString());
        Assert.Equal(0, ok.GetProperty("row").GetProperty("rateSource").GetInt32());
        Assert.Equal(300m, ok.GetProperty("row").GetProperty("lineUsd").GetDecimal()); // ۶ متر × ۵۰ دلار

        // کارگاه فرش‌دار: اندازه و نرخ دارد، پس هیچ کمبود بازدارنده‌ای نیست و قاب «آمادهٔ قطعی شدن» نشان می‌دهد
        var workspace = await HtmlAsync(op, $"/Shipments/{shipmentId}");
        Assert.Contains("data-readiness-state=\"ready\"", workspace);
        Assert.DoesNotContain("data-readiness=\"E_EMPTY_SHIPMENT\"", workspace);
        Assert.DoesNotContain("data-readiness=\"E_NO_FINAL_SIZE\"", workspace);
        Assert.Contains("آمادهٔ قطعی شدن", workspace);
    }

    [PostgresFact]
    public async Task DeclaredValues_HaveThreeDoorsWithThreePolicies_AndTheSettingsPageIsConfigureOnly()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var admin = P1Client.As(factory, t.Admin);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var op = P1Client.As(factory, t.Operator);

        // یک ردیف باز با محل بافت «کاشان» تا «افزودن» چیزی را دوباره ارزش‌گذاری کند
        var rug = await CreateRugAsync(merchant, Values(
            ("rug.serial", "5201"), ("rug.qty", 1), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m), ("rug.origin", "کاشان")));
        var shipmentId = (await SendAsync(merchant, HttpMethod.Post, "/api/shipments", null)).GetProperty("id").GetGuid();
        await SendAsync(op, HttpMethod.Post, $"/api/shipments/{shipmentId}/lines/batch",
            new { ops = new object[] { new { op = "add", clientOpId = "a1", baleNumber = 1, rugId = Id(rug) } } });
        Assert.Equal(3, (await RowAsync(op, shipmentId, Id(rug))).GetProperty("rateSource").GetInt32()); // ندارد

        // خواندن: Floor (بیرون از /api/settings)
        var table = await GetJsonAsync(op, "/api/declared-values");
        var stamp = table.GetProperty("stamp").GetInt32();
        Assert.Equal("rug.origin", table.GetProperty("keyField").GetString());

        // افزودن کلید تازه: Business؛ «كاشان» با کاف عربی همان «کاشان» است
        await SendAsync(op, HttpMethod.Post, "/api/declared-values", new { keyValue = "قم", usdPerM2 = 30m, stamp }, HttpStatusCode.Forbidden);
        var added = await SendAsync(merchant, HttpMethod.Post, "/api/declared-values", new { keyValue = "كاشان", usdPerM2 = 45m, stamp });
        Assert.Equal(1, added.GetProperty("affectedOpenLines").GetInt32());
        stamp = added.GetProperty("stamp").GetInt32();
        var valued = await RowAsync(op, shipmentId, Id(rug));
        Assert.Equal(1, valued.GetProperty("rateSource").GetInt32()); // از جدول
        Assert.Equal(270m, valued.GetProperty("lineUsd").GetDecimal()); // ۶ متر × ۴۵ دلار

        var exists = await SendAsync(merchant, HttpMethod.Post, "/api/declared-values", new { keyValue = "کاشان", usdPerM2 = 50m, stamp }, HttpStatusCode.Conflict);
        Assert.Contains("کاشان", exists.GetProperty("message").GetString());
        await SendAsync(merchant, HttpMethod.Post, "/api/declared-values", new { keyValue = "قم", usdPerM2 = -1m, stamp }, HttpStatusCode.BadRequest);

        // جایگزینی کل جدول: فقط Configure؛ مهر کهنه ۴۰۹
        var rates = new[] { new { id = (Guid?)null, keyValue = "کاشان", usdPerM2 = 40m } };
        await SendAsync(merchant, HttpMethod.Put, "/api/settings/declared-values", new { rates, stamp }, HttpStatusCode.Forbidden);
        await SendAsync(op, HttpMethod.Put, "/api/settings/declared-values", new { rates, stamp }, HttpStatusCode.Forbidden);
        await SendAsync(admin, HttpMethod.Put, "/api/settings/declared-values", new { rates, stamp = stamp - 1 }, HttpStatusCode.Conflict);
        var replaced = await SendAsync(admin, HttpMethod.Put, "/api/settings/declared-values", new { rates, stamp });
        Assert.Equal(1, replaced.GetProperty("affectedOpenLines").GetInt32());
        Assert.Equal(240m, (await RowAsync(op, shipmentId, Id(rug))).GetProperty("lineUsd").GetDecimal());

        // صفحهٔ تنظیمات: فقط Configure؛ قاب، میزبان جزیره با برچسب فیلد کلید، و کاشی در مرکز تنظیمات
        await HtmlAsync(merchant, "/settings/declared-values", HttpStatusCode.Forbidden);
        await HtmlAsync(op, "/settings/declared-values", HttpStatusCode.Forbidden);
        var page = await HtmlAsync(admin, "/settings/declared-values");
        Assert.Contains("data-island=\"declared-values-editor\"", page);
        Assert.Contains("محل بافت", page);
        Assert.Contains("href=\"/settings/declared-values\"", await HtmlAsync(admin, "/settings"));
    }

    [PostgresFact]
    public async Task Finalized_ShowsTheLockInTheShell_RefusesBaleWrites_AndReopenClearsIt()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var op = P1Client.As(factory, t.Operator);

        // یک فرش آماده: شماره، اندازهٔ نهایی و ارزش گمرکی از جدول. نگلهٔ ۲ (بی نگلهٔ ۱)، تا «شماره‌گذاری دوباره» پس از
        // قطعی شدن واقعاً چیزی را عوض کند (۲ → ۱) و قفل آن را ببیند
        var rug = await CreateRugAsync(merchant, Values(
            ("rug.serial", "5301"), ("rug.qty", 1), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m), ("rug.origin", "تبریز")));
        var shipmentId = (await SendAsync(merchant, HttpMethod.Post, "/api/shipments", null)).GetProperty("id").GetGuid();
        var stamp = (await GetJsonAsync(op, "/api/declared-values")).GetProperty("stamp").GetInt32();
        await SendAsync(merchant, HttpMethod.Post, "/api/declared-values", new { keyValue = "تبریز", usdPerM2 = 40m, stamp });
        var added = await SendAsync(op, HttpMethod.Post, $"/api/shipments/{shipmentId}/lines/batch",
            new { ops = new object[] { new { op = "add", clientOpId = "a1", baleNumber = 2, rugId = Id(rug) } } });
        Assert.True(added.GetProperty("results")[0].GetProperty("ok").GetBoolean(), added.ToString());
        var row = await RowAsync(op, shipmentId, Id(rug));
        var measured = await SendAsync(op, HttpMethod.Post, $"/api/shipments/{shipmentId}/lines/batch", new
        {
            ops = new object[] { new { op = "measure", clientOpId = "m1", rugId = Id(rug), rugVersion = row.GetProperty("rugVersion").GetUInt32(), sameAsIntake = true } }
        });
        Assert.True(measured.GetProperty("results")[0].GetProperty("ok").GetBoolean(), measured.ToString());

        // «قطعی کن» (Business)
        var shipment = await GetJsonAsync(merchant, $"/api/shipments/{shipmentId}");
        var finalized = await SendAsync(merchant, HttpMethod.Post, $"/api/shipments/{shipmentId}/finalize",
            new { version = shipment.GetProperty("version").GetUInt32() });
        Assert.True(finalized.GetProperty("ok").GetBoolean(), finalized.ToString());
        Assert.Equal(1, finalized.GetProperty("shipment").GetProperty("status").GetInt32());

        // قاب: نشان قفل با متن هر نقش؛ تراشهٔ «آماده» دیگر نیست؛ فهرست «قطعی»
        var opPage = await HtmlAsync(op, $"/Shipments/{shipmentId}");
        Assert.Contains("این محموله قطعی شده؛ برای تغییر به بازرگان بگویید.", opPage);
        Assert.DoesNotContain("data-readiness-state=\"ready\"", opPage);
        var merchantPage = await HtmlAsync(merchant, $"/Shipments/{shipmentId}");
        Assert.Contains("قطعی شده است. برای تغییر، اول", merchantPage);
        Assert.Contains($"data-shipment-id=\"{shipmentId}\"", await HtmlAsync(op, "/Shipments?status=finalized"));
        Assert.DoesNotContain($"data-shipment-id=\"{shipmentId}\"", await HtmlAsync(op, "/Shipments?status=open"));

        // نگله‌های محمولهٔ قطعی: وزن، ساختن و شماره‌گذاری دوباره ۴۰۹ با پیام فارسی؛ حذف محموله هم ۴۰۹
        var bale = (await GetJsonAsync(op, $"/api/shipments/{shipmentId}/bales"))[0];
        var weight = await SendAsync(op, HttpMethod.Put, $"/api/bales/{bale.GetProperty("id").GetGuid()}",
            new { number = (int?)null, weightKg = 30m, notes = (string?)null, version = bale.GetProperty("version").GetUInt32() },
            HttpStatusCode.Conflict);
        Assert.False(string.IsNullOrWhiteSpace(weight.GetProperty("message").GetString()));
        await SendAsync(op, HttpMethod.Post, $"/api/shipments/{shipmentId}/bales", null, HttpStatusCode.Conflict);
        var current = await GetJsonAsync(merchant, $"/api/shipments/{shipmentId}");
        await SendAsync(op, HttpMethod.Post, $"/api/shipments/{shipmentId}/bales/renumber",
            new { version = current.GetProperty("version").GetUInt32() }, HttpStatusCode.Conflict);
        await SendAsync(merchant, HttpMethod.Delete, $"/api/shipments/{shipmentId}?version={current.GetProperty("version").GetUInt32()}", null,
            HttpStatusCode.Conflict);

        // «باز کردن دوباره» (Business): قفل از قاب می‌رود
        var reopened = await SendAsync(merchant, HttpMethod.Post, $"/api/shipments/{shipmentId}/reopen",
            new { version = current.GetProperty("version").GetUInt32(), reason = "اصلاح وزن" });
        Assert.Equal(0, reopened.GetProperty("status").GetInt32());
        var reopenedPage = await HtmlAsync(op, $"/Shipments/{shipmentId}");
        Assert.DoesNotContain("این محموله قطعی شده؛ برای تغییر به بازرگان بگویید.", reopenedPage);
        Assert.Contains("data-readiness-state=\"ready\"", reopenedPage);
    }

    [PostgresFact]
    public async Task AnotherTenantsShipment_IsNotFound_OnEveryDoor()
    {
        var a = await P2Tenant.CreateAsync(factory);
        var b = await P2Tenant.CreateAsync(factory);
        using var merchantA = P1Client.As(factory, a.Merchant);
        using var opA = P1Client.As(factory, a.Operator);
        using var merchantB = P1Client.As(factory, b.Merchant);
        using var adminB = P1Client.As(factory, b.Admin);

        var shipment = await SendAsync(merchantA, HttpMethod.Post, "/api/shipments", null);
        var id = shipment.GetProperty("id").GetGuid();
        var version = shipment.GetProperty("version").GetUInt32();
        var bale = await SendAsync(opA, HttpMethod.Post, $"/api/shipments/{id}/bales", null);
        var baleId = bale.GetProperty("id").GetGuid();
        var baleVersion = bale.GetProperty("version").GetUInt32();

        // کارگاه دیگر: درست مثل شناسهٔ ناموجود ۴۰۴، و در فهرستش هم نیست
        Assert.Empty((await GetJsonAsync(merchantB, "/api/shipments")).EnumerateArray());
        Assert.DoesNotContain(id.ToString(), await HtmlAsync(merchantB, "/Shipments"));
        await HtmlAsync(merchantB, $"/Shipments/{id}", HttpStatusCode.NotFound);
        await GetJsonAsync(merchantB, $"/api/shipments/{id}", HttpStatusCode.NotFound);
        await GetJsonAsync(merchantB, $"/api/shipments/{id}/readiness", HttpStatusCode.NotFound);
        await GetJsonAsync(merchantB, $"/api/shipments/{id}/bales", HttpStatusCode.NotFound);
        await GetJsonAsync(merchantB, $"/api/shipments/{id}/find-rug?serial=1", HttpStatusCode.NotFound);
        await SendAsync(merchantB, HttpMethod.Put, $"/api/shipments/{id}", Header(shipment, version), HttpStatusCode.NotFound);
        await SendAsync(merchantB, HttpMethod.Post, $"/api/shipments/{id}/finalize", new { version }, HttpStatusCode.NotFound);
        await SendAsync(merchantB, HttpMethod.Post, $"/api/shipments/{id}/reopen", new { version, reason = (string?)null }, HttpStatusCode.NotFound);
        await SendAsync(merchantB, HttpMethod.Post, $"/api/shipments/{id}/bales", null, HttpStatusCode.NotFound);
        await SendAsync(merchantB, HttpMethod.Post, $"/api/shipments/{id}/bales/renumber", new { version }, HttpStatusCode.NotFound);
        await SendAsync(merchantB, HttpMethod.Post, $"/api/shipments/{id}/lines/batch",
            new { ops = new object[] { new { op = "add", clientOpId = "x", baleNumber = 1, serial = "1" } } }, HttpStatusCode.NotFound);
        await SendAsync(merchantB, HttpMethod.Put, $"/api/bales/{baleId}",
            new { number = (int?)null, weightKg = 10m, notes = (string?)null, version = baleVersion }, HttpStatusCode.NotFound);
        await SendAsync(merchantB, HttpMethod.Delete, $"/api/bales/{baleId}?version={baleVersion}", null, HttpStatusCode.NotFound);
        await SendAsync(adminB, HttpMethod.Delete, $"/api/shipments/{id}?version={version}", null, HttpStatusCode.NotFound);

        // و برای کارگاه خودش همه‌چیز سر جایش است
        Assert.Single((await GetJsonAsync(opA, $"/api/shipments/{id}/bales")).EnumerateArray());
    }

    // ── کمک‌ها ──────────────────────────────────────────────────────────────

    private static async Task<string> HtmlAsync(P1Client client, string url, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var response = await client.GetAsync(url);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"GET {url} → {(int)response.StatusCode} (انتظار {(int)expected})");
        return text;
    }

    private static async Task<JsonElement> RowAsync(P1Client client, Guid shipmentId, Guid rugId)
    {
        var bales = await GetJsonAsync(client, $"/api/shipments/{shipmentId}/bales");
        return bales.EnumerateArray()
            .SelectMany(b => b.GetProperty("rows").EnumerateArray())
            .Single(r => r.GetProperty("rugId").GetGuid() == rugId);
    }

    private static object Header(JsonElement shipment, uint version) => new
    {
        title = shipment.GetProperty("title").GetString(),
        documentDate = shipment.GetProperty("documentDate").GetString(),
        destination = (string?)null,
        buyerName = (string?)null,
        declarationNo = (string?)null,
        customsOffice = (string?)null,
        targetBaleAreaM2 = (decimal?)null,
        usdRate = (decimal?)null,
        notes = "یادداشت",
        version
    };
}
