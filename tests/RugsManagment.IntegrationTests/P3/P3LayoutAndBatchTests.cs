using System.Net;
using System.Text.Json;
using RugsManagment.Application.Features.Intake;
using RugsManagment.IntegrationTests.P1;
using RugsManagment.IntegrationTests.P2;
using RugsManagment.IntegrationTests.Support;
using static RugsManagment.IntegrationTests.P2.P2Http;
using static RugsManagment.IntegrationTests.P3.P3Grid;

namespace RugsManagment.IntegrationTests.P3;

/// <summary>
/// چیدمان ستون‌ها (G.2، I.2 «Grid layout»: نسخهٔ کهنه ۴۰۹، ستون پنهان، ستون لازم، برگشت به پیش‌فرض، فقط Configure)،
/// «ورودی امروز» و «ورودی‌های اخیر» (B.5، H.1)، و صفحه‌های تازه.
/// </summary>
public sealed class P3LayoutAndBatchTests(RugsWebFactory factory) : IClassFixture<RugsWebFactory>
{
    [PostgresFact]
    public async Task GridLayout_HidesAColumn_StalePutIs409_RequiredColumnStays_AndResetRestoresDefaults()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var admin = P1Client.As(factory, t.Admin);
        using var merchant = P1Client.As(factory, t.Merchant);

        var layout = await GetJsonAsync(admin, "/api/settings/grid-layout/intake");
        Assert.Equal(0u, layout.GetProperty("version").GetUInt32());
        var columns = layout.GetProperty("columns").EnumerateArray().ToList();
        Assert.Equal(columns.Count, layout.GetProperty("available").GetArrayLength());

        // «یادداشت» پنهان، «تعداد» اول با برچسب و پهنای تازه
        var qty = columns.Single(c => Key(c) == "rug.qty");
        var reordered = new List<object> { WithLabel(qty, "تعداد تخته", 9m) };
        reordered.AddRange(columns.Where(c => Key(c) is not "rug.qty" and not "rug.notes").Select(Echo));
        var saved = await SendAsync(admin, HttpMethod.Put, "/api/settings/grid-layout/intake",
            new { gridId = "intake", columns = reordered, version = 0u });
        var version = saved.GetProperty("version").GetUInt32();
        Assert.NotEqual(0u, version);
        Assert.Equal("rug.qty", Key(saved.GetProperty("columns")[0]));
        Assert.Equal("تعداد تخته", saved.GetProperty("columns")[0].GetProperty("label").GetString());

        var grid = await GetJsonAsync(merchant, "/api/grid/intake");
        Assert.DoesNotContain("rug.notes", ColumnKeys(grid));
        Assert.Equal("rug.qty", ColumnKeys(grid)[0]);

        // نسخهٔ کهنه → ۴۰۹
        await SendAsync(admin, HttpMethod.Put, "/api/settings/grid-layout/intake",
            new { gridId = "intake", columns = reordered, version = 0u }, HttpStatusCode.Conflict);

        // طول (لازم) را نمی‌شود پنهان کرد؛ کلید ناشناخته ۴۰۰
        var withoutLength = columns.Where(c => Key(c) != "rug.intakeLength").Select(Echo).ToList();
        await SendAsync(admin, HttpMethod.Put, "/api/settings/grid-layout/intake",
            new { gridId = "intake", columns = withoutLength, version }, HttpStatusCode.BadRequest);
        await SendAsync(admin, HttpMethod.Put, "/api/settings/grid-layout/intake",
            new { gridId = "intake", columns = new[] { new { key = "rug.nope", label = "x", type = "text", width = 10m, sticky = false, readOnly = false, required = false, options = (string[]?)null, money = false } }, version },
            HttpStatusCode.BadRequest);

        // فقط تنظیم‌کننده؛ جدول ناشناخته ۴۰۴
        await GetJsonAsync(merchant, "/api/settings/grid-layout/intake", HttpStatusCode.Forbidden);
        await GetJsonAsync(admin, "/api/settings/grid-layout/nope", HttpStatusCode.NotFound);

        // برگشت به پیش‌فرض (با نسخه)
        await SendAsync(admin, HttpMethod.Delete, "/api/settings/grid-layout/intake?version=1", null, HttpStatusCode.Conflict);
        var reset = await SendAsync(admin, HttpMethod.Delete, $"/api/settings/grid-layout/intake?version={version}", null);
        Assert.Equal(0u, reset.GetProperty("version").GetUInt32());
        Assert.Contains("rug.notes", ColumnKeys(await GetJsonAsync(merchant, "/api/grid/intake")));
    }

    [PostgresFact]
    public async Task TodayBatch_IsFoundOrCreatedPerSeller_AndRecentListsOnlyBatchesWithRugs()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var op = P1Client.As(factory, t.Operator);

        var first = await SendAsync(op, HttpMethod.Post, "/api/batches/today", new { sellerName = "حاج رضا", receivedAt = (string?)null });
        var same = await SendAsync(op, HttpMethod.Post, "/api/batches/today", new { sellerName = "حاج‌رضا", receivedAt = (string?)null });
        var other = await SendAsync(op, HttpMethod.Post, "/api/batches/today", new { sellerName = "کریمی", receivedAt = (string?)null });
        var nobody = await SendAsync(op, HttpMethod.Post, "/api/batches/today", new { sellerName = (string?)null, receivedAt = (string?)null });

        Assert.Equal(first.GetProperty("id").GetGuid(), same.GetProperty("id").GetGuid());
        Assert.NotEqual(first.GetProperty("id").GetGuid(), other.GetProperty("id").GetGuid());
        Assert.NotEqual(first.GetProperty("number").GetInt32(), other.GetProperty("number").GetInt32());
        Assert.Equal(IranDay.Today().ToString("yyyy-MM-dd"), first.GetProperty("receivedOn").GetString());
        Assert.StartsWith("ورودی ", first.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, nobody.GetProperty("sellerName").ValueKind);

        await SendAsync(op, HttpMethod.Post, "/api/batches/today", new { sellerName = "x", receivedAt = "1405-99-99" }, HttpStatusCode.BadRequest);

        // «ورودی‌های اخیر» فقط ورودی‌هایی که فرش دارند
        Assert.Equal(0, (await GetJsonAsync(op, "/api/batches/recent")).GetArrayLength());
        AllSaved(await SaveAsync(op, [
            P3Op.New(("rug.serial", "1 - 2"), ("rug.qty", 2), ("rug.intakeLength", 1m), ("rug.intakeWidth", 1m))
        ], seller: "کریمی"));
        var recent = await GetJsonAsync(op, "/api/batches/recent?days=7");
        var only = Assert.Single(recent.EnumerateArray());
        Assert.Equal(other.GetProperty("id").GetGuid(), only.GetProperty("id").GetGuid());
        Assert.Equal(2, only.GetProperty("pieces").GetInt32());
        Assert.Equal(1, only.GetProperty("rugCount").GetInt32());

        // فهرست ورودی‌ها (RugBatchDto، spec I.2) هم فروشنده و شماره را دارد
        var listed = (await GetJsonAsync(op, "/api/batches")).EnumerateArray()
            .Single(b => b.GetProperty("id").GetGuid() == other.GetProperty("id").GetGuid());
        Assert.Equal("کریمی", listed.GetProperty("sellerName").GetString());
        Assert.Equal(other.GetProperty("number").GetInt32(), listed.GetProperty("number").GetInt32());
    }

    [PostgresFact]
    public async Task Pages_Intake_GridSettings_AndRugsGridView_Render_WithTheRightGates()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var op = P1Client.As(factory, t.Operator);
        using var admin = P1Client.As(factory, t.Admin);

        var intake = await op.GetAsync("/Intake");
        Assert.Equal(HttpStatusCode.OK, intake.StatusCode);
        var html = await intake.Content.ReadAsStringAsync();
        Assert.Contains("data-island=\"intake-grid\"", html);
        Assert.Contains(IranDay.Today().ToString("yyyy-MM-dd"), html);

        var rugsGrid = await op.GetAsync("/Rugs?view=grid&search=x");
        Assert.Equal(HttpStatusCode.OK, rugsGrid.StatusCode);
        Assert.Contains("data-island=\"rugs-grid\"", await rugsGrid.Content.ReadAsStringAsync());

        var settings = await admin.GetAsync("/settings/grid/rugs");
        Assert.Equal(HttpStatusCode.OK, settings.StatusCode);
        Assert.Contains("data-island=\"grid-columns-editor\"", await settings.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/settings/grid/nope")).StatusCode);

        var denied = await op.GetAsync("/settings/grid");
        Assert.True(denied.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.Forbidden,
            $"/settings/grid برای انباردار: {(int)denied.StatusCode}");
    }

    private static string Key(JsonElement column) => column.GetProperty("key").GetString()!;

    private static object Echo(JsonElement column) => JsonSerializer.Deserialize<JsonElement>(column.GetRawText());

    private static object WithLabel(JsonElement column, string label, decimal width)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(column.GetRawText())!.AsObject();
        node["label"] = label;
        node["width"] = width;
        return node;
    }
}
