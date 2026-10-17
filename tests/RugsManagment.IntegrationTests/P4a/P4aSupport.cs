using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.IntegrationTests.P1;
using RugsManagment.IntegrationTests.Support;
using static RugsManagment.IntegrationTests.P2.P2Http;

namespace RugsManagment.IntegrationTests.P4a;

/// <summary>
/// کمک‌های آزمون‌های دامنهٔ محموله (فاز ۴a): ساختن فرش، محموله، عمل‌های خط، جدول ارزش گمرکی و ترجیحات، همه از راه همان
/// API که جزیره‌ها صدا می‌زنند (دادهٔ فارسی فقط در بدنهٔ JSON، هرگز از پوسته). هر آزمون کارگاه تازهٔ خودش را دارد.
/// </summary>
internal static class P4aApi
{
    /// <summary>فرش تازه به شکل فاز ۲ (فقط values): شماره، اندازهٔ اول، محل بافت و تعداد.</summary>
    public static Task<JsonElement> RugAsync(
        P1Client client, string serial, decimal length, decimal width, string? origin, int quantity = 1, string? pattern = null)
    {
        var values = Values(("rug.serial", serial), ("rug.qty", quantity), ("rug.intakeLength", length), ("rug.intakeWidth", width));
        if (origin is not null) values["rug.origin"] = origin;
        if (pattern is not null) values["rug.pattern"] = pattern;
        return CreateRugAsync(client, values);
    }

    /// <summary>«محمولهٔ تازه» با یک ضربه (بی بدنه).</summary>
    public static async Task<Guid> NewShipmentAsync(P1Client merchant)
        => (await SendAsync(merchant, HttpMethod.Post, "/api/shipments", null)).GetProperty("id").GetGuid();

    public static Task<JsonElement> ShipmentAsync(P1Client client, Guid shipmentId)
        => GetJsonAsync(client, $"/api/shipments/{shipmentId}");

    public static async Task<uint> ShipmentVersionAsync(P1Client client, Guid shipmentId)
        => (await ShipmentAsync(client, shipmentId)).GetProperty("version").GetUInt32();

    /// <summary><c>POST /api/shipments/{id}/lines/batch</c> با این عمل‌ها (پاسخ ۲۰۰؛ شکست هر عمل در نتیجهٔ خودش).</summary>
    public static Task<JsonElement> OpsAsync(P1Client client, Guid shipmentId, params object[] ops)
        => SendAsync(client, HttpMethod.Post, $"/api/shipments/{shipmentId}/lines/batch", new { ops });

    /// <summary>نتیجهٔ عمل iام؛ با <paramref name="ok"/> موفق یا ناموفق بودنش هم سنجیده می‌شود.</summary>
    public static JsonElement Result(JsonElement batch, int index = 0, bool? ok = true)
    {
        var result = batch.GetProperty("results")[index];
        if (ok is { } expected)
            Assert.True(result.GetProperty("ok").GetBoolean() == expected, result.ToString());
        return result;
    }

    /// <summary>کد خطای اول یک عمل ناموفق.</summary>
    public static string ErrorCode(JsonElement result) => result.GetProperty("errors")[0].GetProperty("code").GetString()!;

    /// <summary>نگله‌های محموله با ردیف‌هایشان.</summary>
    public static Task<JsonElement> BalesAsync(P1Client client, Guid shipmentId)
        => GetJsonAsync(client, $"/api/shipments/{shipmentId}/bales");

    /// <summary>ردیف یک فرش در نگله‌های محموله.</summary>
    public static async Task<JsonElement> RowAsync(P1Client client, Guid shipmentId, Guid rugId)
        => (await BalesAsync(client, shipmentId)).EnumerateArray()
            .SelectMany(b => b.GetProperty("rows").EnumerateArray())
            .Single(r => r.GetProperty("rugId").GetGuid() == rugId);

    /// <summary>همهٔ ردیف‌های محموله.</summary>
    public static async Task<IReadOnlyList<JsonElement>> RowsAsync(P1Client client, Guid shipmentId)
        => (await BalesAsync(client, shipmentId)).EnumerateArray()
            .SelectMany(b => b.GetProperty("rows").EnumerateArray())
            .ToList();

    /// <summary>عمل «افزودن» با شناسهٔ فرش.</summary>
    public static object Add(Guid rugId, int baleNumber, string? clientOpId = null)
        => new { op = "add", clientOpId = clientOpId ?? rugId.ToString("N")[..8], baleNumber, rugId };

    /// <summary>عمل «همان اندازه» برای ردیف یک فرش (نسخهٔ فرش از همان ردیف).</summary>
    public static object SameAsIntake(JsonElement row)
        => new
        {
            op = "measure",
            clientOpId = "m-" + row.GetProperty("rugId").GetGuid().ToString("N")[..8],
            rugId = row.GetProperty("rugId").GetGuid(),
            rugVersion = row.GetProperty("rugVersion").GetUInt32(),
            sameAsIntake = true
        };

    /// <summary>مهر فعلی جدول ارزش گمرکی.</summary>
    public static async Task<int> StampAsync(P1Client client)
        => (await GetJsonAsync(client, "/api/declared-values")).GetProperty("stamp").GetInt32();

    /// <summary>بازرگان یک کلید تازه به جدول اضافه می‌کند؛ پاسخ <c>{affectedOpenLines, stamp}</c>.</summary>
    public static async Task<JsonElement> AddRateAsync(P1Client merchant, string key, decimal usdPerM2)
        => await SendAsync(merchant, HttpMethod.Post, "/api/declared-values",
            new { keyValue = key, usdPerM2, stamp = await StampAsync(merchant) });

    /// <summary>تنظیم‌کننده کل جدول را جایگزین می‌کند؛ پاسخ <c>{affectedOpenLines, stamp}</c>.</summary>
    public static async Task<JsonElement> ReplaceRatesAsync(P1Client admin, params (string Key, decimal Usd)[] rates)
        => await SendAsync(admin, HttpMethod.Put, "/api/settings/declared-values", new
        {
            rates = rates.Select(r => new { id = (Guid?)null, keyValue = r.Key, usdPerM2 = r.Usd }).ToArray(),
            stamp = await StampAsync(admin)
        });

    /// <summary>ترجیحات کارگاه را می‌خواند، عوض می‌کند و با همان نسخه برمی‌گرداند (Configure).</summary>
    public static async Task SetPreferencesAsync(P1Client admin, Action<JsonObject> change)
    {
        var prefs = await GetJsonAsync(admin, "/api/settings/preferences");
        var body = JsonNode.Parse(prefs.GetRawText())!.AsObject();
        change(body);
        using var put = await admin.SendRawJsonAsync(HttpMethod.Put, "/api/settings/preferences", body.ToJsonString());
        await JsonAsync(put, put.StatusCode == HttpStatusCode.NoContent ? HttpStatusCode.NoContent : HttpStatusCode.OK);
    }

    /// <summary>
    /// پاسخ ۴۰۹ قفل «قطعی» (C.8): کد <c>E_SHIPMENT_LOCKED</c>، شمارهٔ محموله و پیام فارسی که «قطعی» را می‌گوید.
    /// </summary>
    public static void AssertLocked(JsonElement conflict, int shipmentNumber)
    {
        Assert.Equal("E_SHIPMENT_LOCKED", conflict.GetProperty("code").GetString());
        Assert.Equal(shipmentNumber, conflict.GetProperty("shipmentNumber").GetInt32());
        Assert.Contains("قطعی", conflict.GetProperty("message").GetString());
    }

    /// <summary>
    /// نوع مرحله و قالب گردش کارِ یک‌مرحله‌ای در همین کارگاه، مستقیم در دیتابیس (API قالب‌ها موضوع این آزمون نیست).
    /// </summary>
    public static async Task<Guid> WorkflowTemplateAsync(RugsWebFactory factory, Guid tenantId)
    {
        var step = new ProcessStepType
        {
            TenantId = tenantId,
            Code = $"p4a-{Guid.NewGuid():N}"[..20],
            NameFa = "شستشوی آزمون",
            NameEn = "IT washing",
            DefaultPricingModel = StepPricingModel.Fixed,
            DefaultUnitRate = 0m
        };
        var template = new WorkflowTemplate { TenantId = tenantId, Name = $"قالب {Guid.NewGuid():N}"[..20] };
        template.Steps.Add(new WorkflowTemplateStep { ProcessStepTypeId = step.Id, OrderIndex = 0 });

        await using var db = factory.CreateDbContext();
        db.ProcessStepTypes.Add(step);
        db.WorkflowTemplates.Add(template);
        await db.SaveChangesAsync();
        return template.Id;
    }
}
