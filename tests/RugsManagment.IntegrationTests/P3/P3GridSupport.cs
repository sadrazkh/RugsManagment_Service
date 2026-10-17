using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using RugsManagment.Domain.Enums;
using RugsManagment.IntegrationTests.P1;
using static RugsManagment.IntegrationTests.P2.P2Http;

namespace RugsManagment.IntegrationTests.P3;

/// <summary>یک ردیف ارسالی جدول (همان شکل <c>GridOp</c>).</summary>
internal sealed record P3Op(
    Guid ClientRowId,
    Dictionary<string, object?> Values,
    Guid? RugId = null,
    uint? Version = null,
    Dictionary<string, object?>? BaseValues = null,
    bool Delete = false)
{
    public static P3Op New(params (string Key, object? Value)[] values) => new(Guid.NewGuid(), P2.P2Http.Values(values));

    public object Body => new
    {
        clientRowId = ClientRowId,
        rugId = RugId,
        version = Version,
        values = Values,
        baseValues = BaseValues,
        delete = Delete
    };
}

/// <summary>کمک‌های HTTP آزمون‌های جدول (فاز ۳).</summary>
internal static class P3Grid
{
    /// <summary><c>POST /api/grid/rugs/batch</c> → بدنهٔ پاسخ.</summary>
    public static Task<JsonElement> SaveAsync(
        P1Client client, IEnumerable<P3Op> ops, string gridId = "intake", string? seller = null, DateOnly? day = null,
        Guid? batchId = null, HttpStatusCode expected = HttpStatusCode.OK)
        => SendAsync(client, HttpMethod.Post, "/api/grid/rugs/batch", new
        {
            gridId,
            batch = new { id = batchId, sellerName = seller, receivedAt = day?.ToString("yyyy-MM-dd") },
            shipmentId = (Guid?)null,
            ops = ops.Select(o => o.Body).ToList()
        }, expected);

    /// <summary>نتیجه‌های ذخیره، به ترتیب ردیف‌ها.</summary>
    public static IReadOnlyList<JsonElement> Results(JsonElement response)
        => response.GetProperty("results").EnumerateArray().ToList();

    public static int Status(JsonElement result) => result.GetProperty("status").GetInt32();

    public static Guid RugId(JsonElement result) => result.GetProperty("rugId").GetGuid();

    public static uint RowVersion(JsonElement result) => result.GetProperty("version").GetUInt32();

    public static JsonElement Current(JsonElement result, string key) => result.GetProperty("current").GetProperty(key);

    public static JsonElement Derived(JsonElement result, string key) => result.GetProperty("derived").GetProperty(key);

    /// <summary>همهٔ ردیف‌ها «ذخیره شد» (وگرنه پیام کامل شکست).</summary>
    public static void AllSaved(JsonElement response)
    {
        foreach (var result in Results(response))
            Assert.True(Status(result) == 0, "ردیف ذخیره نشد: " + result.GetRawText());
    }

    /// <summary>کلیدهای ستون‌های یک جدول.</summary>
    public static IReadOnlyList<string> ColumnKeys(JsonElement grid)
        => grid.GetProperty("columns").EnumerateArray().Select(c => c.GetProperty("key").GetString()!).ToList();

    public static IReadOnlyList<JsonElement> Rows(JsonElement grid) => grid.GetProperty("rows").EnumerateArray().ToList();

    /// <summary>ردیفِ یک فرش در داده‌های جدول.</summary>
    public static JsonElement Row(JsonElement grid, Guid rugId)
        => Rows(grid).Single(r => r.GetProperty("rugId").GetGuid() == rugId);

    /// <summary>ترجیحات کارگاه از راه API (مثل آزمون‌های فاز ۲).</summary>
    public static async Task SetPreferencesAsync(P1Client admin, Action<JsonObject> change)
    {
        var prefs = await GetJsonAsync(admin, "/api/settings/preferences");
        var body = JsonNode.Parse(prefs.GetRawText())!.AsObject();
        change(body);
        using var put = await admin.SendRawJsonAsync(HttpMethod.Put, "/api/settings/preferences", body.ToJsonString());
        await JsonAsync(put, put.StatusCode == HttpStatusCode.NoContent ? HttpStatusCode.NoContent : HttpStatusCode.OK);
    }

    /// <summary>خانهٔ «قیمت خرید» با واحد.</summary>
    public static object Price(decimal rate, CostBasis basis) => new { rate, basis = (int)basis };
}
