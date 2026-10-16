using System.Net;
using System.Text.Json;
using RugsManagment.IntegrationTests.P1;
using RugsManagment.IntegrationTests.P2;
using RugsManagment.IntegrationTests.P3;
using RugsManagment.IntegrationTests.Support;
using static RugsManagment.IntegrationTests.P2.P2Http;
using static RugsManagment.IntegrationTests.P4a.P4aApi;

namespace RugsManagment.IntegrationTests.P4b;

/// <summary>
/// لایهٔ وب نگله‌بندی (فاز ۴b، K.2/P4b): جدول «نگله‌ها» (<c>GET /api/grid/bales</c>) با برچسب‌های واژهٔ کارگاه و ایستگاه‌های
/// Tab نمای ساده، ستون «جا»ی جدول فرش‌ها، قفل «قطعی» روی ذخیرهٔ دسته‌ای جدول و عمل‌های نگله، پیدا کردن فرش با شماره، برگهٔ
/// انتخاب محموله در خانه، و صفحه‌های زبانهٔ نگله‌ها، «حالت اسکن (گوشی)» و جای فرش در فهرست و صفحهٔ فرش.
///
/// تصمیم دربارهٔ «۴۰۹» ذخیرهٔ دسته‌ای جدول (<c>POST /api/grid/rugs/batch</c>): مثل هر خطای یک ردیف (G.6 «یک ردیف بد بقیه را
/// نمی‌اندازد») رد شدن قفل هم در نتیجهٔ همان ردیف می‌آید — <c>status = Invalid</c> با پیام فارسی قفل — و پاسخ کل درخواست ۲۰۰
/// است؛ عمل‌های جدول نگله‌ها (<c>lines/batch</c>) هم همین‌طور هر کدام کد <c>E_SHIPMENT_LOCKED</c> خودشان را دارند. درِ تک‌فرشی
/// (PUT فرش، اندازهٔ نهایی) همان ۴۰۹ واقعی است (آزمون‌های فاز ۴a).
/// </summary>
public sealed class P4bWebTests(RugsWebFactory factory) : IClassFixture<RugsWebFactory>
{
    // ═════════════════════════════════════════════════════════════════════
    // جدول «نگله‌ها»
    // ═════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task BalesGrid_HasOneRowPerLine_TermLabels_AndSimpleModeTabStops()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var admin = P1Client.As(factory, t.Admin);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var op = P1Client.As(factory, t.Operator);

        var a = await RugAsync(merchant, "5501", 2m, 3m, "کاشان");
        var lot = await RugAsync(merchant, "5510 - 5559", 0.40m, 0.40m, "کاشان", 50);
        var c = await RugAsync(merchant, "5502", 1.93m, 1.50m, "تبریز");
        var shipmentId = await NewShipmentAsync(merchant);
        var added = await OpsAsync(op, shipmentId, Add(Id(a), 1), Add(Id(lot), 1));
        Result(added, 0);
        Result(added, 1);
        Result(await OpsAsync(op, shipmentId, Add(Id(c), 2)));

        // بازرگان (نمای ساده): ستون‌های G.2، خانه‌های توصیفی ایستگاه Tab نیستند؛ Tab از شماره مستقیم به «طول» نهایی
        var grid = await GetJsonAsync(merchant, $"/api/grid/bales?shipmentId={shipmentId}");
        var columns = Columns(grid);
        var keys = grid.GetProperty("columns").EnumerateArray().Select(col => col.GetProperty("key").GetString()).ToList();
        Assert.Equal(["bale.number", "rug.serial", "rug.qty"], keys.Take(3));
        Assert.True(keys.IndexOf("rug.finalLength") < keys.IndexOf("rug.finalWidth"));
        Assert.True(keys.IndexOf("rug.intakeSize") < keys.IndexOf("rug.finalLength"));
        Assert.Equal("نگله", columns["bale.number"].GetProperty("label").GetString());
        Assert.Equal("طول", columns["rug.finalLength"].GetProperty("label").GetString());
        Assert.Equal("عرض", columns["rug.finalWidth"].GetProperty("label").GetString());
        Assert.Equal("اندازهٔ اول", columns["rug.intakeSize"].GetProperty("label").GetString());
        Assert.False(columns["rug.pattern"].GetProperty("tabStop").GetBoolean());
        Assert.False(columns["rug.origin"].GetProperty("tabStop").GetBoolean());
        Assert.False(columns["rug.qty"].GetProperty("tabStop").GetBoolean());
        Assert.True(columns["rug.serial"].GetProperty("tabStop").GetBoolean());
        Assert.True(columns["rug.finalLength"].GetProperty("tabStop").GetBoolean());
        Assert.True(columns["line.usdPerM2"].GetProperty("readOnly").GetBoolean());
        Assert.DoesNotContain(columns.Values, col => col.GetProperty("money").GetBoolean());

        // ردیف‌ها: یکی برای هر خط، به ترتیب نگله و Seq؛ نسخهٔ خط جدا از نسخهٔ فرش
        var rows = grid.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal([Id(a), Id(lot), Id(c)], rows.Select(r => r.GetProperty("rugId").GetGuid()));
        Assert.All(rows, r =>
        {
            Assert.NotEqual(JsonValueKind.Null, r.GetProperty("lineId").ValueKind);
            Assert.True(r.GetProperty("lineVersion").GetUInt32() > 0);
            Assert.False(r.GetProperty("derived").TryGetProperty("rug.purchaseTotal", out _)); // جدول محموله بی‌پول است
        });
        Assert.Equal(1, rows[0].GetProperty("values").GetProperty("bale.number").GetInt32());
        Assert.Equal(2, rows[2].GetProperty("values").GetProperty("bale.number").GetInt32());
        Assert.Equal("5501", rows[0].GetProperty("values").GetProperty("rug.serial").GetString());
        Assert.Equal("کاشان", rows[0].GetProperty("values").GetProperty("rug.origin").GetString());
        Assert.Equal("۲٫۰۰ × ۳٫۰۰", rows[0].GetProperty("derived").GetProperty("rug.intakeSize").GetString());
        Assert.Equal("ندارد", rows[0].GetProperty("derived").GetProperty("line.rateSourceLabel").GetString());
        Assert.Equal(6m, rows[0].GetProperty("derived").GetProperty("line.area").GetDecimal());
        // دسته: قاعدهٔ هر تخته (۰٫۱۶ × ۵۰ = ۸٫۰۰)
        Assert.Equal("۰٫۱۶ × ۵۰ = ۸٫۰۰", rows[1].GetProperty("derived").GetProperty("line.areaText").GetString());
        Assert.Equal(50, rows[1].GetProperty("derived").GetProperty("rug.qty").GetInt32());
        var totals = grid.GetProperty("totals");
        Assert.Equal(3, totals.GetProperty("rows").GetInt32());
        Assert.Equal(52, totals.GetProperty("pieces").GetInt32());
        Assert.Equal(6m + 8m + 2.90m, totals.GetProperty("areaM2").GetDecimal()); // ۱٫۹۳ × ۱٫۵۰ = ۲٫۸۹۵ → ۲٫۹۰
        Assert.Equal(JsonValueKind.Null, totals.GetProperty("purchase").ValueKind);

        // تنظیم‌کننده در نمای کامل: خانه‌های توصیفی ایستگاه Tab‌اند
        using (var full = await admin.SendJsonAsync(HttpMethod.Put, "/api/me/preferences", new { uiMode = 1 }))
            Assert.True(full.IsSuccessStatusCode, await full.Content.ReadAsStringAsync());
        var adminColumns = Columns(await GetJsonAsync(admin, $"/api/grid/bales?shipmentId={shipmentId}"));
        Assert.True(adminColumns["rug.pattern"].GetProperty("tabStop").GetBoolean());

        // «عدل»: برچسب ستون، عنوان زبانه و «جا»ی جدول فرش‌ها همه از واژهٔ کارگاه
        await SetPreferencesAsync(admin, p => p["baleTerm"] = 1);
        var adl = Columns(await GetJsonAsync(merchant, $"/api/grid/bales?shipmentId={shipmentId}"));
        Assert.Equal("عدل", adl["bale.number"].GetProperty("label").GetString());
        var workspace = await HtmlAsync(merchant, $"/Shipments/{shipmentId}");
        Assert.Contains("عدل‌ها", workspace);
        var rugsGrid = await GetJsonAsync(merchant, "/api/grid/rugs");
        Assert.Contains(Columns(rugsGrid).Keys, k => k == "rug.location");
        var located = rugsGrid.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("rugId").GetGuid() == Id(a));
        Assert.Equal("در عدل ۱ (محمولهٔ ۱)", located.GetProperty("derived").GetProperty("rug.location").GetString());
        Assert.Equal(1, located.GetProperty("derived").GetProperty("rug.locationCode").GetInt32());

        // بی محموله، محمولهٔ ناموجود و محمولهٔ کارگاه دیگر: ۴۰۴ (وجودش لو نمی‌رود)
        await GetJsonAsync(merchant, "/api/grid/bales", HttpStatusCode.NotFound);
        await GetJsonAsync(merchant, $"/api/grid/bales?shipmentId={Guid.NewGuid()}", HttpStatusCode.NotFound);
        var other = await P2Tenant.CreateAsync(factory);
        using var stranger = P1Client.As(factory, other.Merchant);
        await GetJsonAsync(stranger, $"/api/grid/bales?shipmentId={shipmentId}", HttpStatusCode.NotFound);
    }

    // ═════════════════════════════════════════════════════════════════════
    // قفل «قطعی» روی جدول‌ها (K.2/P4b: از فاز ۴a به این‌جا آمد)
    // ═════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task AfterFinalize_TheGridBatchAndTheBalesGridOps_AreRefusedByTheLock()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var op = P1Client.As(factory, t.Operator);

        await AddRateAsync(merchant, "تبریز", 40m);
        var rug = await RugAsync(merchant, "5701", 2m, 3m, "تبریز", pattern: "ماهی");
        var shipmentId = await NewShipmentAsync(merchant);
        Result(await OpsAsync(op, shipmentId, Add(Id(rug), 1)));

        // تا محموله باز است خانهٔ توصیفی از جدول نگله‌ها نوشته می‌شود (همان ذخیرهٔ دسته‌ای، gridId = bales)
        var openRow = await GridRowAsync(op, shipmentId, Id(rug));
        var saved = await P3Grid.SaveAsync(op, [Edit(openRow, ("rug.pattern", "هریس"))], gridId: "bales");
        P3Grid.AllSaved(saved);
        Result(await OpsAsync(op, shipmentId, SameAsIntake(await RowAsync(op, shipmentId, Id(rug)))));

        var finalized = await SendAsync(merchant, HttpMethod.Post, $"/api/shipments/{shipmentId}/finalize",
            new { version = await ShipmentVersionAsync(merchant, shipmentId) });
        Assert.True(finalized.GetProperty("ok").GetBoolean(), finalized.ToString());

        // ذخیرهٔ دسته‌ای جدول فرش‌ها و جدول نگله‌ها: همان ردیف «نادرست» با پیام فارسی قفل، و چیزی نوشته نمی‌شود
        var row = await GridRowAsync(op, shipmentId, Id(rug));
        foreach (var gridId in new[] { "rugs", "bales" })
        {
            foreach (var client in new[] { merchant, op })
            {
                var refused = P3Grid.Results(await P3Grid.SaveAsync(client, [Edit(row, ("rug.pattern", "گبه"))], gridId: gridId))[0];
                Assert.Equal(1, P3Grid.Status(refused)); // Invalid
                var text = refused.GetProperty("errors")[0].GetProperty("message").GetString();
                Assert.Contains("قطعی", text);
            }
        }

        var opRefusal = P3Grid.Results(await P3Grid.SaveAsync(op, [Edit(row, ("rug.notes", null), ("rug.finalLength", 2.5m))]))[0];
        Assert.Equal(1, P3Grid.Status(opRefusal));
        Assert.Equal("این محموله قطعی شده؛ برای تغییر به بازرگان بگویید.",
            opRefusal.GetProperty("errors")[0].GetProperty("message").GetString());

        Assert.Equal("هریس", (await GridRowAsync(op, shipmentId, Id(rug))).GetProperty("values").GetProperty("rug.pattern").GetString());

        // عمل‌های جدول نگله‌ها: اندازه، انتقال، خارج کردن، افزودن — هر کدام E_SHIPMENT_LOCKED
        var line = await RowAsync(op, shipmentId, Id(rug));
        var ops = await OpsAsync(op, shipmentId,
            new { op = "measure", clientOpId = "m", rugId = Id(rug), rugVersion = line.GetProperty("rugVersion").GetUInt32(), finalLength = 2.5m, finalWidth = 3m },
            new { op = "move", clientOpId = "v", lineId = line.GetProperty("lineId").GetGuid(), lineVersion = line.GetProperty("lineVersion").GetUInt32(), baleNumber = 2 },
            new { op = "remove", clientOpId = "r", lineId = line.GetProperty("lineId").GetGuid(), lineVersion = line.GetProperty("lineVersion").GetUInt32() });
        for (var i = 0; i < 3; i++)
            Assert.Equal("E_SHIPMENT_LOCKED", ErrorCode(Result(ops, i, ok: false)));
        Assert.Single((await BalesAsync(op, shipmentId)).EnumerateArray());

        // خواندن جدول همچنان آزاد است (فقط‌خواندنی)
        Assert.Single((await GetJsonAsync(op, $"/api/grid/bales?shipmentId={shipmentId}")).GetProperty("rows").EnumerateArray());
    }

    // ═════════════════════════════════════════════════════════════════════
    // پیدا کردن فرش با شماره (جعبهٔ «شماره را بزنید یا اسکن کنید»)
    // ═════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task FindRug_ExactInRangeAndPlacement_AndAnUnknownSerialIs404()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var op = P1Client.As(factory, t.Operator);

        var single = await RugAsync(merchant, "5601", 2m, 3m, "کاشان");
        await RugAsync(merchant, "5610 - 5619", 0.40m, 0.40m, "کاشان", 10);
        var first = await NewShipmentAsync(merchant);
        var second = await NewShipmentAsync(merchant);
        Result(await OpsAsync(op, first, Add(Id(single), 2)));

        var exact = await GetJsonAsync(op, $"/api/shipments/{first}/find-rug?serial=5601");
        Assert.Equal("exact", exact.GetProperty("matchType").GetString());
        Assert.Equal(Id(single), exact.GetProperty("rug").GetProperty("id").GetGuid());
        Assert.Equal(2, exact.GetProperty("inBale").GetProperty("baleNo").GetInt32());
        Assert.True(exact.GetProperty("inBale").GetProperty("sameShipment").GetBoolean());

        // ارقام فارسی (اسکن یا صفحه‌کلید فارسی) همان شماره است
        Assert.Equal("exact", (await GetJsonAsync(op, $"/api/shipments/{first}/find-rug?serial={Uri.EscapeDataString("۵۶۰۱")}"))
            .GetProperty("matchType").GetString());

        // از محمولهٔ دیگر: همان فرش در محمولهٔ ۱ / نگلهٔ ۲ است («منتقل شود؟»)
        var elsewhere = await GetJsonAsync(op, $"/api/shipments/{second}/find-rug?serial=5601");
        Assert.False(elsewhere.GetProperty("inBale").GetProperty("sameShipment").GetBoolean());
        Assert.Equal(1, elsewhere.GetProperty("inBale").GetProperty("shipmentNo").GetInt32());

        // شمارهٔ داخل دسته: کل دسته پیدا می‌شود (C.3 «کل دسته را بگذار / جدا کن»)
        var inRange = await GetJsonAsync(op, $"/api/shipments/{first}/find-rug?serial=5615");
        Assert.Equal("inRange", inRange.GetProperty("matchType").GetString());
        Assert.Equal("5610 - 5619", inRange.GetProperty("rug").GetProperty("serialNo").GetString());
        Assert.Equal(JsonValueKind.Null, inRange.GetProperty("inBale").ValueKind);

        var missing = await GetJsonAsync(op, $"/api/shipments/{first}/find-rug?serial=99999", HttpStatusCode.NotFound);
        Assert.False(string.IsNullOrWhiteSpace(missing.GetProperty("message").GetString()));
    }

    // ═════════════════════════════════════════════════════════════════════
    // خانه: برگهٔ انتخاب با دو محمولهٔ باز (A.4)
    // ═════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task Home_WithTwoOpenShipments_TheTilesOpenTheChooserSheet()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var op = P1Client.As(factory, t.Operator);

        // بی محموله: کارت‌های محموله و آمادگی نیستند (کاشی خودش «محمولهٔ تازه» را می‌گوید)
        var empty = await HtmlAsync(merchant, "/");
        Assert.DoesNotContain("data-home-card=\"open-shipments\"", empty);
        Assert.DoesNotContain("data-home-card=\"readiness\"", empty);

        // یک محمولهٔ باز: کاشی مستقیم همان را باز می‌کند؛ کارت جمع‌ها و آمادگی‌اش را دارد
        var firstId = await NewShipmentAsync(merchant);
        var one = await HtmlAsync(merchant, "/");
        Assert.Contains($"href=\"/Shipments/{firstId}?tab=bales\"", one);
        Assert.DoesNotContain("data-sheet-open=\"home-sheet-baling\"", one);
        Assert.Contains("data-home-card=\"open-shipments\"", one);
        Assert.Contains("data-home-card=\"readiness\"", one);
        Assert.Contains("data-readiness=\"E_EMPTY_SHIPMENT\"", one);

        // دو محمولهٔ باز: کاشی و کارت برگهٔ انتخاب را باز می‌کنند؛ هر دو محموله در برگه، تازه‌ترین فوکوس‌شده
        var rug = await RugAsync(merchant, "5801", 2m, 3m, "کاشان");
        var secondId = await NewShipmentAsync(merchant);
        Result(await OpsAsync(op, secondId, Add(Id(rug), 1))); // دومی تازه‌ترین فعالیت را دارد

        foreach (var client in new[] { merchant, op })
        {
            var two = await HtmlAsync(client, "/");
            Assert.Contains("data-sheet-open=\"home-sheet-baling\"", two);
            Assert.Contains("data-sheet-open=\"home-sheet-open-shipments\"", two);
            Assert.Contains($"href=\"/Shipments/{firstId}?tab=bales\"", two);
            Assert.Contains($"href=\"/Shipments/{secondId}?tab=bales\"", two);
            Assert.Contains("data-sheet-autofocus=\"true\"", two);
            Assert.Contains("محمولهٔ ۲ (", two);
            Assert.Contains("محمولهٔ ۱ (", two);

            // تازه‌ترین (محمولهٔ ۲) محمولهٔ هدف کارت است
            var card = two[two.IndexOf("data-home-card=\"open-shipments\"", StringComparison.Ordinal)..];
            Assert.True(card.IndexOf($"data-home-shipment=\"{secondId}\"", StringComparison.Ordinal)
                        < card.IndexOf($"data-home-shipment=\"{firstId}\"", StringComparison.Ordinal));
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // صفحه‌ها: زبانهٔ نگله‌ها، «حالت اسکن»، فهرست محموله‌ها، جای فرش
    // ═════════════════════════════════════════════════════════════════════

    [PostgresFact]
    public async Task Pages_BalesTabPackModeAndRugLocation_RenderForFloor_AndForeignIdsAre404()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var op = P1Client.As(factory, t.Operator);

        var rug = await RugAsync(merchant, "5901", 2m, 3m, "کاشان");
        var shipmentId = await NewShipmentAsync(merchant);
        Result(await OpsAsync(op, shipmentId, Add(Id(rug), 3)));
        var baleId = (await BalesAsync(op, shipmentId))[0].GetProperty("id").GetGuid();

        // زبانهٔ نگله‌ها: میزبان baling-board با props I.5، نمای جدول یا کارت از ?view=، پیوند «حالت اسکن (گوشی)»
        var tab = await HtmlAsync(op, $"/Shipments/{shipmentId}");
        Assert.Contains("data-island=\"baling-board\"", tab);
        Assert.Contains($"\"shipmentId\":\"{shipmentId}\"", tab);
        Assert.Contains("\"view\":\"table\"", tab);
        Assert.Contains("\"locked\":false", tab);
        Assert.Contains("\"tolerancePercent\":", tab);
        Assert.Contains($"href=\"/Shipments/{shipmentId}/Pack\"", tab);
        Assert.Contains("data-shipment-tab=\"bales\"", tab);
        Assert.Contains("\"view\":\"cards\"", await HtmlAsync(op, $"/Shipments/{shipmentId}?tab=bales&view=cards"));

        // «حالت اسکن (گوشی)»: Floor؛ نگلهٔ آغازین فقط اگر مال همین محموله است
        var pack = await HtmlAsync(op, $"/Shipments/{shipmentId}/Pack?baleId={baleId}");
        Assert.Contains("data-island=\"pack-mode\"", pack);
        Assert.Contains($"\"baleId\":\"{baleId}\"", pack);
        Assert.Contains("\"locked\":false", pack);
        Assert.Contains("\"baleId\":null", await HtmlAsync(op, $"/Shipments/{shipmentId}/Pack?baleId={Guid.NewGuid()}"));
        await HtmlAsync(op, $"/Shipments/{Guid.NewGuid()}/Pack", HttpStatusCode.NotFound);
        var other = await P2Tenant.CreateAsync(factory);
        using var stranger = P1Client.As(factory, other.Operator);
        await HtmlAsync(stranger, $"/Shipments/{shipmentId}/Pack", HttpStatusCode.NotFound);

        // فهرست محموله‌ها: جزیره فیلتر صفحه را هم می‌گیرد
        Assert.Contains("\"status\":\"open\"", await HtmlAsync(merchant, "/Shipments?status=open"));
        Assert.Contains("\"canCreate\":false", await HtmlAsync(op, "/Shipments"));

        // فهرست فرش‌ها (نمای ساده): چیپ‌های هر چهار جا، ستون «جا» با شمارهٔ نگله و محموله، بی فیلتر وضعیت کارگاهی
        var list = await HtmlAsync(merchant, "/Rugs");
        Assert.Contains("location=InBale", list);
        Assert.Contains("location=Exported", list);
        Assert.Contains("data-rug-location=\"InBale\"", list);
        Assert.Contains("در نگلهٔ ۳", list);
        Assert.Contains("محمولهٔ ۱", list);
        Assert.DoesNotContain("aria-label=\"فیلتر وضعیت\"", list);
        Assert.Contains("data-rug-location=\"InBale\"", await HtmlAsync(merchant, "/Rugs?location=InBale"));

        // صفحهٔ فرش: چیپ «جا» در سربرگ و بخش «محموله / نگله» با پیوند به زبانهٔ نگله‌ها
        var details = await HtmlAsync(op, $"/Rugs/Details/{Id(rug)}");
        Assert.Contains("data-rug-location=\"InBale\"", details);
        Assert.Contains($"data-rug-shipment=\"{shipmentId}\"", details);
        Assert.Contains($"href=\"/Shipments/{shipmentId}?tab=bales\"", details);
    }

    // ── کمک‌ها ──────────────────────────────────────────────────────────────

    private static Dictionary<string, JsonElement> Columns(JsonElement grid)
        => grid.GetProperty("columns").EnumerateArray()
            .ToDictionary(c => c.GetProperty("key").GetString()!, c => c);

    /// <summary>ردیف یک فرش در جدول نگله‌ها.</summary>
    private static async Task<JsonElement> GridRowAsync(P1Client client, Guid shipmentId, Guid rugId)
        => (await GetJsonAsync(client, $"/api/grid/bales?shipmentId={shipmentId}")).GetProperty("rows").EnumerateArray()
            .Single(r => r.GetProperty("rugId").GetGuid() == rugId);

    /// <summary>ویرایش خانه‌های یک ردیف جدول با نسخهٔ همان ردیف.</summary>
    private static P3Op Edit(JsonElement row, params (string Key, object? Value)[] values)
        => new(Guid.NewGuid(), Values(values), row.GetProperty("rugId").GetGuid(), row.GetProperty("version").GetUInt32());

    private static async Task<string> HtmlAsync(P1Client client, string url, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var response = await client.GetAsync(url);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"GET {url} → {(int)response.StatusCode} (انتظار {(int)expected})");
        return text;
    }
}
