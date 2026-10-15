using System.Net;
using System.Text.Json;
using RugsManagment.IntegrationTests.P1;
using RugsManagment.IntegrationTests.P2;
using RugsManagment.IntegrationTests.Support;
using static RugsManagment.IntegrationTests.P2.P2Http;
using static RugsManagment.IntegrationTests.P4a.P4aApi;

namespace RugsManagment.IntegrationTests.P4a;

/// <summary>
/// «قطعی کن» و قفل بعد از آن روی دیتابیس واقعی (spec C.6–C.8، K.2/P4a، L.2 «Locks»): «قطعی» با کمبود بازدارنده فهرست
/// کامل را برمی‌گرداند و چیزی را عوض نمی‌کند؛ بعد از «قطعی» هر مسیر نوشتنی که ستون قفل‌داری را عوض کند ۴۰۹ با پیام فارسی
/// می‌گیرد — حتی ذخیره‌های «فقط قفل» (ویرایش نقشه، «بایگانی» گروهی، شماره‌گذاری دوبارهٔ نگله، تاریخ اظهار) — و یادداشت
/// فرش و یادداشت محموله آزاد می‌مانند.
/// </summary>
public sealed class P4aLockTests(RugsWebFactory factory) : IClassFixture<RugsWebFactory>
{
    [PostgresFact]
    public async Task Finalize_IsBlockedWithTheFullIssueList_ThenSucceeds_AndEveryWritePathIsLocked()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var admin = P1Client.As(factory, t.Admin);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var op = P1Client.As(factory, t.Operator);

        await ReplaceRatesAsync(admin, ("گبه", 45m));
        var templateId = await WorkflowTemplateAsync(factory, t.TenantId);
        var a = await SendAsync(merchant, HttpMethod.Post, "/api/rugs", new
        {
            values = Values(("rug.serial", "4501"), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m), ("rug.origin", "گبه"),
                ("rug.pattern", "ساده")),
            workflowTemplateId = templateId
        });
        var b = await RugAsync(merchant, "4502", 1m, 2m, "تبریز");
        var s = await RugAsync(merchant, "4503", 1m, 1m, "گبه");
        var shipmentId = await NewShipmentAsync(merchant);
        var number = (await ShipmentAsync(merchant, shipmentId)).GetProperty("number").GetInt32();
        var adds = await OpsAsync(op, shipmentId, Add(Id(a), 1), Add(Id(b), 1), Add(Id(s), 1));
        Assert.All(adds.GetProperty("results").EnumerateArray(), r => Assert.True(r.GetProperty("ok").GetBoolean(), r.ToString()));
        var empty = await SendAsync(op, HttpMethod.Post, $"/api/shipments/{shipmentId}/bales", null);
        var statusesBefore = await StatusesAsync(op, Id(a), Id(b), Id(s));

        // ── «قطعی کن» با کمبود: ok = false با فهرست کامل؛ هیچ چیز عوض نمی‌شود ──
        var blocked = await SendAsync(merchant, HttpMethod.Post, $"/api/shipments/{shipmentId}/finalize",
            new { version = await ShipmentVersionAsync(merchant, shipmentId) });
        Assert.False(blocked.GetProperty("ok").GetBoolean());
        var issues = blocked.GetProperty("issues").EnumerateArray().ToList();
        var emptyBale = issues.Single(i => i.GetProperty("code").GetString() == "E_EMPTY_BALE");
        Assert.Equal(2, emptyBale.GetProperty("baleNumber").GetInt32());
        Assert.Contains("نگلهٔ ۲ خالی است", emptyBale.GetProperty("message").GetString());
        var noFinal = issues.Single(i => i.GetProperty("code").GetString() == "E_NO_FINAL_SIZE");
        Assert.Equal(3, noFinal.GetProperty("count").GetInt32());
        Assert.Equal(3, noFinal.GetProperty("rugIds").GetArrayLength());
        var noValue = issues.Single(i => i.GetProperty("code").GetString() == "E_NO_DECLARED_VALUE");
        Assert.Equal("تبریز", noValue.GetProperty("key").GetString());
        Assert.Equal([Id(b)], noValue.GetProperty("rugIds").EnumerateArray().Select(x => x.GetGuid()));
        Assert.All(issues.Where(i => i.GetProperty("code").GetString()!.StartsWith("E_", StringComparison.Ordinal)),
            i => Assert.True(i.GetProperty("blocking").GetBoolean()));
        Assert.Equal(0, blocked.GetProperty("shipment").GetProperty("status").GetInt32());
        Assert.Equal(statusesBefore, await StatusesAsync(op, Id(a), Id(b), Id(s)));
        Assert.Equal(3, (await GetJsonAsync(op, "/api/shipments")).EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == shipmentId).GetProperty("readinessBlocking").GetInt32());

        // ── درست کردن: نگلهٔ خالی حذف، «همان اندازه» برای همه، نرخ «تبریز» ──
        await SendAsync(op, HttpMethod.Delete,
            $"/api/bales/{empty.GetProperty("id").GetGuid()}?version={empty.GetProperty("version").GetUInt32()}", null, HttpStatusCode.NoContent);
        var rows = await RowsAsync(op, shipmentId);
        var measured = await OpsAsync(op, shipmentId, [.. rows.Select(SameAsIntake)]);
        Assert.All(measured.GetProperty("results").EnumerateArray(), r => Assert.True(r.GetProperty("ok").GetBoolean(), r.ToString()));
        await AddRateAsync(merchant, "تبریز", 50m);
        Assert.DoesNotContain((await GetJsonAsync(op, $"/api/shipments/{shipmentId}/readiness")).EnumerateArray(),
            i => i.GetProperty("blocking").GetBoolean());

        var finalized = await SendAsync(merchant, HttpMethod.Post, $"/api/shipments/{shipmentId}/finalize",
            new { version = await ShipmentVersionAsync(merchant, shipmentId) });
        Assert.True(finalized.GetProperty("ok").GetBoolean(), finalized.ToString());
        Assert.Equal(1, finalized.GetProperty("shipment").GetProperty("status").GetInt32());
        foreach (var id in new[] { Id(a), Id(b), Id(s) })
        {
            var rug = await GetJsonAsync(op, $"/api/rugs/{id}");
            Assert.Equal(5, rug.GetProperty("status").GetInt32());   // صادر شده
            Assert.Equal(2, rug.GetProperty("location").GetInt32());
        }

        Assert.Equal(3, (await GetJsonAsync(op, $"/api/rugs?location=2&shipmentId={shipmentId}")).GetProperty("totalCount").GetInt32());

        // ── قفل روی هر مسیر نوشتنی (۴۰۹ + پیام فارسی + شمارهٔ محموله) ──
        var rugA = await GetJsonAsync(merchant, $"/api/rugs/{Id(a)}");

        // PUT فرش: ویرایش نقشه (ذخیرهٔ «فقط قفل»)
        AssertLocked(await SendAsync(merchant, HttpMethod.Put, $"/api/rugs/{Id(a)}",
            new { values = Values(("rug.pattern", "لچک ترنج")), version = Version(rugA) }, HttpStatusCode.Conflict), number);

        // PUT فرش: محل بافت (فیلد کلید ارزش گمرکی) — دوباره‌ارزش‌گذاری فقط در محمولهٔ باز است
        AssertLocked(await SendAsync(merchant, HttpMethod.Put, $"/api/rugs/{Id(a)}",
            new { values = Values(("rug.origin", "کاشان")), version = Version(rugA) }, HttpStatusCode.Conflict), number);

        // ویرایش گروهی با «بایگانی»
        AssertLocked(await SendAsync(merchant, HttpMethod.Put, "/api/rugs/bulk/fields",
            new { rugIds = new[] { Id(a), Id(s) }, status = 4 }, HttpStatusCode.Conflict), number);

        // اندازهٔ نهایی
        AssertLocked(await SendAsync(op, HttpMethod.Put, $"/api/rugs/{Id(a)}/final-size",
            new { length = 2.5m, width = 3m, version = Version(rugA) }, HttpStatusCode.Conflict), number);

        // گردش کار: پیش بردن مرحله، و تغییر مسیر (اعمال قالب روی فرش بی‌مسیر)
        var stepId = rugA.GetProperty("workflowSteps")[0].GetProperty("id").GetGuid();
        AssertLocked(await SendAsync(merchant, HttpMethod.Post, $"/api/rugs/{Id(a)}/steps/{stepId}/advance", new
        {
            serviceProviderId = (Guid?)null, manualCostOverride = (decimal?)null, pricingModel = (int?)null, unitRate = (decimal?)null,
            pricingConfigJson = (string?)null, fieldValuesJson = (string?)null, notes = (string?)null, markCompleted = true
        }, HttpStatusCode.Conflict), number);
        AssertLocked(await SendAsync(merchant, HttpMethod.Post, $"/api/rugs/{Id(s)}/workflow/apply-template",
            new { templateId, skippedOptionalStepIds = (Guid[]?)null }, HttpStatusCode.Conflict), number);

        // فروش
        var sale = await SendAsync(merchant, HttpMethod.Put, $"/api/sales/rug/{Id(a)}", new
        {
            buyerName = "خریدار", buyerPhone = (string?)null, salePrice = 2_000_000m, discount = 0m, receivedAmount = 0m,
            paymentMethod = 0, soldAt = (DateTimeOffset?)null, reference = (string?)null, notes = (string?)null
        }, HttpStatusCode.Conflict);
        Assert.Contains("قطعی", sale.GetProperty("message").GetString());

        // سطل زباله (MVC): ۳۰۲ به صفحهٔ همان فرش با پیام، نه ۵۰۰؛ فرش سر جایش
        using (var trash = await AntiforgeryHelper.PostFormAsync(merchant.Http, $"/Rugs/Details/{Id(a)}", $"/Rugs/Delete/{Id(a)}", []))
        {
            Assert.Equal(HttpStatusCode.Redirect, trash.StatusCode);
            Assert.Contains($"/Rugs/Details/{Id(a)}", trash.Headers.Location!.OriginalString, StringComparison.OrdinalIgnoreCase);
        }

        // خط‌ها: خارج کردن، نرخ دستی و افزودن، هر کدام در نتیجهٔ خودش
        var rowA = await RowAsync(op, shipmentId, Id(a));
        var lineOps = await OpsAsync(merchant, shipmentId,
            new { op = "remove", clientOpId = "rm", lineId = rowA.GetProperty("lineId").GetGuid(), lineVersion = rowA.GetProperty("lineVersion").GetUInt32() },
            new { op = "rate", clientOpId = "rt", lineId = rowA.GetProperty("lineId").GetGuid(), lineVersion = rowA.GetProperty("lineVersion").GetUInt32(), usdPerM2Override = 99m },
            new { op = "add", clientOpId = "ad", baleNumber = 9, serial = "4502" });
        Assert.All(lineOps.GetProperty("results").EnumerateArray(), r =>
        {
            Assert.False(r.GetProperty("ok").GetBoolean(), r.ToString());
            Assert.Equal("E_SHIPMENT_LOCKED", ErrorCode(r));
        });
        Assert.Single(lineOps.GetProperty("bales").EnumerateArray()); // نگلهٔ ۹ ساخته نشد

        // نگله: شماره‌گذاری دوباره (حتی وقتی چیزی عوض نمی‌شود)، وزن، ساختن
        var bale = (await BalesAsync(op, shipmentId))[0];
        await SendAsync(op, HttpMethod.Post, $"/api/shipments/{shipmentId}/bales/renumber",
            new { version = await ShipmentVersionAsync(op, shipmentId) }, HttpStatusCode.Conflict);
        var weight = await SendAsync(op, HttpMethod.Put, $"/api/bales/{bale.GetProperty("id").GetGuid()}",
            new { number = (int?)null, weightKg = 30m, notes = (string?)null, version = bale.GetProperty("version").GetUInt32() },
            HttpStatusCode.Conflict);
        Assert.Equal("این محموله قطعی شده؛ برای تغییر به بازرگان بگویید.", weight.GetProperty("message").GetString()); // متن انباردار
        await SendAsync(op, HttpMethod.Post, $"/api/shipments/{shipmentId}/bales", null, HttpStatusCode.Conflict);

        // سربرگ: تاریخ اظهار ۴۰۹؛ یادداشت محموله آزاد
        var header = await ShipmentAsync(merchant, shipmentId);
        var date = DateOnly.Parse(header.GetProperty("documentDate").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        var dateEdit = await SendAsync(merchant, HttpMethod.Put, $"/api/shipments/{shipmentId}",
            HeaderBody(header, date.AddDays(1), "یادداشت"), HttpStatusCode.Conflict);
        Assert.Contains("باز کردن دوباره", dateEdit.GetProperty("message").GetString());
        var notes = await SendAsync(merchant, HttpMethod.Put, $"/api/shipments/{shipmentId}", HeaderBody(header, date, "بار دوم دیر رسید"));
        Assert.Equal("بار دوم دیر رسید", notes.GetProperty("notes").GetString());
        Assert.Equal(1, notes.GetProperty("status").GetInt32());

        // یادداشت فرش آزاد است و فرش «صادر شده» می‌ماند
        rugA = await GetJsonAsync(merchant, $"/api/rugs/{Id(a)}");
        var noted = await SendAsync(merchant, HttpMethod.Put, $"/api/rugs/{Id(a)}",
            new { values = Values(("rug.notes", "گره‌ها محکم")), version = Version(rugA) });
        Assert.Equal(5, noted.GetProperty("status").GetInt32());

        // هیچ‌کدام از ۴۰۹ها چیزی ننوشت
        rugA = await GetJsonAsync(merchant, $"/api/rugs/{Id(a)}");
        Assert.Equal("ساده", rugA.GetProperty("pattern").GetString());
        Assert.Equal("گبه", rugA.GetProperty("origin").GetString());
        Assert.Equal(2m, Dec(rugA, "finalLength"));
        Assert.Equal(2, rugA.GetProperty("location").GetInt32());
        Assert.Equal(5, (await GetJsonAsync(merchant, $"/api/rugs/{Id(s)}")).GetProperty("status").GetInt32());
        Assert.Equal(3, (await RowsAsync(op, shipmentId)).Count);
    }

    private static object HeaderBody(JsonElement shipment, DateOnly date, string notes) => new
    {
        title = shipment.GetProperty("title").GetString(),
        documentDate = date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        destination = (string?)null,
        buyerName = (string?)null,
        declarationNo = (string?)null,
        customsOffice = shipment.GetProperty("customsOffice").GetString(),
        targetBaleAreaM2 = shipment.GetProperty("targetBaleAreaM2").ValueKind == JsonValueKind.Null ? (decimal?)null : Dec(shipment, "targetBaleAreaM2"),
        usdRate = shipment.GetProperty("usdRate").ValueKind == JsonValueKind.Null ? (decimal?)null : Dec(shipment, "usdRate"),
        notes,
        version = shipment.GetProperty("version").GetUInt32()
    };

    private static async Task<IReadOnlyList<int>> StatusesAsync(P1Client client, params Guid[] ids)
    {
        var statuses = new List<int>();
        foreach (var id in ids)
            statuses.Add((await GetJsonAsync(client, $"/api/rugs/{id}")).GetProperty("status").GetInt32());
        return statuses;
    }
}
