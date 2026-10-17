using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using RugsManagment.Domain.Entities;
using RugsManagment.IntegrationTests.P1;
using RugsManagment.IntegrationTests.P2;
using RugsManagment.IntegrationTests.Support;
using static RugsManagment.IntegrationTests.P2.P2Http;
using static RugsManagment.IntegrationTests.P4a.P4aApi;
namespace RugsManagment.IntegrationTests.P7;
public sealed class DesignerSetupTests(RugsWebFactory factory) : IClassFixture<RugsWebFactory>
{
    [PostgresFact]
    public async Task ComputedColumn_HasRealSamples_LiveXlsxFormula_AndTemplatePreflight()
    {
        var t = await P2Tenant.CreateAsync(factory); using var admin = P1Client.As(factory,t.Admin);
        await SendAsync(admin,HttpMethod.Post,"/api/settings/presets/export-rugs",new { serialStart = 1,confirmSampleRates = true });
        var rug = await RugAsync(admin,"1",2,3,"گبه"); var shipmentId = await NewShipmentAsync(admin);
        Result(await OpsAsync(admin,shipmentId,Add(Id(rug),1)));
        var header = await ShipmentAsync(admin,shipmentId);
        await SendAsync(admin,HttpMethod.Put,$"/api/shipments/{shipmentId}",new { title = "آزمون قالب",documentDate = header.GetProperty("documentDate").GetString(),usdRate = 100m,version = Version(header) });
        var seed = (await GetJsonAsync(admin,"/api/document-templates")).EnumerateArray().Single(x => x.GetProperty("kind").GetInt32() == 0);
        var layout = JsonNode.Parse(seed.GetProperty("layout").GetRawText())!;
        layout["sheets"]![0]!["columns"]!.AsArray().Add(JsonSerializer.SerializeToNode(new { key = "rialTotal",label = "جمع ریالی",width = 14,expr = "round(col.usd * shipment.usdRate, 0)" }));
        var expr = await SendAsync(admin,HttpMethod.Post,"/api/document-templates/validate-expression",new { expression = "round(col.usd * shipment.usdRate, 0)",layout,columnKey = "rialTotal",shipmentId });
        Assert.Equal("Number",expr.GetProperty("type").GetString()); Assert.Contains("27000",expr.GetProperty("samples").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("ROUND",expr.GetProperty("excel").GetString());
        var template = await SendAsync(admin,HttpMethod.Post,"/api/document-templates",new { name = "قالب با جمع ریالی",layout }); var templateId = Id(template);
        var readiness = await GetJsonAsync(admin,$"/api/shipments/{shipmentId}/readiness?templateId={templateId}");
        Assert.Contains(readiness.EnumerateArray(),x => x.GetProperty("code").GetString() == "W_TEMPLATE_FIELD_EMPTY");
        using var file = await admin.GetAsync($"/shipments/{shipmentId}/export.xlsx?template={templateId}"); Assert.Equal(HttpStatusCode.OK,file.StatusCode);
        using var stream = new MemoryStream(await file.Content.ReadAsByteArrayAsync()); using var zip = new ZipArchive(stream);
        using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()); var xml = await reader.ReadToEndAsync();
        Assert.Contains("ROUND(L3*100, 0)",xml); Assert.Contains("27000",xml);
    }
    [PostgresFact]
    public async Task Preset_IsIdempotent_LeavesExistingLabels_AndSetupListsMissingRate()
    {
        var t = await P2Tenant.CreateAsync(factory); using var admin = P1Client.As(factory,t.Admin); using var merchant = P1Client.As(factory,t.Merchant);
        await SendAsync(merchant,HttpMethod.Put,"/api/me/preferences",new { uiMode = 0,textScale = 1 },HttpStatusCode.NoContent);
        var request = new { serialStart = 900,baleTerm = 1,confirmSampleRates = true };
        var first = await SendAsync(admin,HttpMethod.Post,"/api/settings/presets/export-rugs",request);
        var second = await SendAsync(admin,HttpMethod.Post,"/api/settings/presets/export-rugs",request);
        Assert.Contains(first.GetProperty("items").EnumerateArray(),x => x.GetString()!.Contains("موجود بود: قیمت خرید",StringComparison.Ordinal));
        Assert.All(second.GetProperty("items").EnumerateArray(),x => Assert.StartsWith("موجود بود:",x.GetString()));
        await using var db = factory.CreateDbContext();
        Assert.Equal(12,await db.Set<CostCategory>().CountAsync(x => x.TenantId == t.TenantId));
        Assert.Equal(2,await db.CustomFieldDefinitions.CountAsync(x => x.TenantId == t.TenantId));
        Assert.Single(await db.LabelTemplates.Where(x => x.TenantId == t.TenantId).ToListAsync());
        using var sprite = await admin.GetAsync("/icons.svg"); var svg = await sprite.Content.ReadAsStringAsync();
        foreach (var icon in await db.Set<CostCategory>().Where(x => x.TenantId == t.TenantId && x.Icon != null).Select(x => x.Icon).Distinct().ToListAsync())
            Assert.Contains($"id=\"{icon}\"",svg);
        var status = await GetJsonAsync(admin,"/api/settings/setup-status"); Assert.True(status.GetProperty("ready").GetBoolean());
        await RugAsync(admin,"900",2,3,"بدون نرخ");
        status = await GetJsonAsync(admin,"/api/settings/setup-status");
        var rates = status.GetProperty("steps").EnumerateArray().Single(x => x.GetProperty("key").GetString() == "declaredValues");
        Assert.False(rates.GetProperty("done").GetBoolean()); Assert.Contains(rates.GetProperty("missing").EnumerateArray(),x => x.GetString() == "بدون نرخ");
    }
    [PostgresFact]
    public async Task Designer_SaveRevision_Stale409_Restore_Reset_AndDefault()
    {
        var t = await P2Tenant.CreateAsync(factory); using var admin = P1Client.As(factory,t.Admin);
        await SendAsync(admin,HttpMethod.Post,"/api/settings/presets/export-rugs",new { serialStart = 1 });
        var template = (await GetJsonAsync(admin,"/api/document-templates")).EnumerateArray().Single(x => x.GetProperty("kind").GetInt32() == 0);
        var id = Id(template); var version = Version(template); var layout = JsonNode.Parse(template.GetProperty("layout").GetRawText())!;
        layout["sheets"]![0]!["columns"]![1]!["label"] = "شمارهٔ تغییرکرده";
        var saved = await SendAsync(admin,HttpMethod.Put,$"/api/document-templates/{id}",new { name = "قالب آزمایشی",layout,version }); Assert.Equal(2,saved.GetProperty("revision").GetInt32());
        await SendAsync(admin,HttpMethod.Put,$"/api/document-templates/{id}",new { name = "ذخیرهٔ کهنه",layout,version },HttpStatusCode.Conflict);
        var restored = await SendAsync(admin,HttpMethod.Post,$"/api/document-templates/{id}/revisions/1/restore",new { version = Version(saved) }); Assert.Equal(3,restored.GetProperty("revision").GetInt32());
        Assert.Equal(template.GetProperty("layout").GetProperty("sheets")[0].GetProperty("columns")[1].GetProperty("label").GetString(), restored.GetProperty("layout").GetProperty("sheets")[0].GetProperty("columns")[1].GetProperty("label").GetString());
        var reset = await SendAsync(admin,HttpMethod.Post,$"/api/document-templates/{id}/reset",new { version = Version(restored) }); Assert.Equal(4,reset.GetProperty("revision").GetInt32());
        var duplicate = await SendAsync(admin,HttpMethod.Post,$"/api/document-templates/{id}/duplicate",null);
        var defaultTemplate = await SendAsync(admin,HttpMethod.Post,$"/api/document-templates/{Id(duplicate)}/set-default",new { version = Version(duplicate) }); Assert.True(defaultTemplate.GetProperty("isDefault").GetBoolean());
        var revisions = await GetJsonAsync(admin,$"/api/document-templates/{id}/revisions"); Assert.Equal(4,revisions.GetArrayLength());
    }
    [PostgresFact]
    public async Task InvalidExpression_Cycle_AndMissingLotQty_ReturnPersianJsonPaths()
    {
        var t = await P2Tenant.CreateAsync(factory); using var admin = P1Client.As(factory,t.Admin);
        var seed = (await GetJsonAsync(admin,"/api/document-templates")).EnumerateArray().Single(x => x.GetProperty("kind").GetInt32() == 2);
        foreach (var mode in new[] { "expression","cycle","quantity" })
        {
            var layout = JsonNode.Parse(seed.GetProperty("layout").GetRawText())!;
            var columns = layout["sheets"]![0]!["columns"]!.AsArray();
            if (mode == "quantity") columns.RemoveAt(1);
            else { columns[4]!["expr"] = mode == "cycle" ? "col.size" : "round("; }
            var errors = await SendAsync(admin,HttpMethod.Post,"/api/document-templates/validate",new { layout },HttpStatusCode.BadRequest);
            Assert.All(errors.GetProperty("errors").EnumerateArray(),e => { Assert.StartsWith("$",e.GetProperty("path").GetString()); Assert.Matches("[؀-ۿ]",e.GetProperty("message").GetString()!); });
        }
    }
}
