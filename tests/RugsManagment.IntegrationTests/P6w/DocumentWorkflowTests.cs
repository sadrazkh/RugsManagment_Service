using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.IntegrationTests.P1;
using RugsManagment.IntegrationTests.P2;
using RugsManagment.IntegrationTests.Support;
using static RugsManagment.IntegrationTests.P2.P2Http;
using static RugsManagment.IntegrationTests.P4a.P4aApi;
namespace RugsManagment.IntegrationTests.P6w;
public sealed class DocumentWorkflowTests(RugsWebFactory factory) : IClassFixture<RugsWebFactory>
{
    private static Task<JsonElement> Preset(P1Client admin) => SendAsync(admin,HttpMethod.Post,"/api/settings/presets/export-rugs",new { serialStart = 100,confirmSampleRates = true,baleTerm = 0 });
    [PostgresFact]
    public async Task FreshTenant_DefaultDeclarationWorksBeforePreset()
    {
        var t = await P2Tenant.CreateAsync(factory); using var merchant = P1Client.As(factory,t.Merchant);
        var rug = await RugAsync(merchant,"10",2,3,"گبه"); var shipment = await NewShipmentAsync(merchant);
        Result(await OpsAsync(merchant,shipment,Add(Id(rug),1)));
        using var xlsx = await merchant.GetAsync($"/shipments/{shipment}/export.xlsx"); Assert.Equal(HttpStatusCode.OK,xlsx.StatusCode);
        using var print = await merchant.GetAsync($"/shipments/{shipment}/print"); Assert.Equal(HttpStatusCode.OK,print.StatusCode);
    }
    [PostgresFact]
    public async Task XlsxAndPreview_UseDatabaseData_RecordHashes_AndDraftFilename()
    {
        var tenant = await P2Tenant.CreateAsync(factory); using var admin = P1Client.As(factory,tenant.Admin); using var merchant = P1Client.As(factory,tenant.Merchant);
        await Preset(admin); var rug = await RugAsync(merchant,"100 - 149",0.4m,0.4m,"گبه",50);
        var shipment = await NewShipmentAsync(merchant); Result(await OpsAsync(merchant,shipment,Add(Id(rug),29)));
        var preview = await GetJsonAsync(merchant,$"/api/shipments/{shipment}/documents/preview");
        Assert.True(preview.GetProperty("isDraft").GetBoolean());
        var cells = preview.GetProperty("sheets")[0].GetProperty("rows").EnumerateArray().SelectMany(r => r.GetProperty("cells").EnumerateArray()).ToArray();
        Assert.Contains(cells,c => c.GetProperty("display").GetString() == "50");
        Assert.Contains(cells,c => c.GetProperty("display").GetString() == "8");
        Assert.Contains(cells,c => c.GetProperty("display").GetString() == "360");
        using var response = await merchant.GetAsync($"/shipments/{shipment}/export.xlsx");
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var file = await response.Content.ReadAsByteArrayAsync(); Assert.True(file.Length > 1000);
        Assert.Contains("%D9%BE%DB%8C%D8%B4",response.Content.Headers.ContentDisposition!.ToString());
        await using var db = factory.CreateDbContext();
        var run = await db.Set<ExportRun>().SingleAsync(x => x.TenantId == tenant.TenantId && x.Format == ExportFormat.Xlsx);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(file)),run.FileSha256); Assert.Equal(64,run.DataHash.Length); Assert.Equal(1,run.RugCount);
        using var print = await merchant.GetAsync($"/shipments/{shipment}/print");
        Assert.Equal(HttpStatusCode.OK,print.StatusCode); Assert.Contains("document-print.css",await print.Content.ReadAsStringAsync());
    }
    [PostgresFact]
    public async Task ForeignIds_Return404_AndNeverCreateRun()
    {
        var a = await P2Tenant.CreateAsync(factory); var b = await P2Tenant.CreateAsync(factory);
        using var ca = P1Client.As(factory,a.Admin); using var cb = P1Client.As(factory,b.Admin);
        await Preset(ca); await Preset(cb); var shipment = await NewShipmentAsync(cb);
        var foreignTemplate = (await GetJsonAsync(cb,"/api/document-templates"))[0].GetProperty("id").GetGuid();
        var ownShipment = await NewShipmentAsync(ca);
        foreach (var url in new[] { $"/shipments/{shipment}/export.xlsx",$"/shipments/{shipment}/print",$"/api/shipments/{shipment}/exports",$"/api/shipments/{shipment}/documents/preview",$"/shipments/{ownShipment}/export.xlsx?template={foreignTemplate}",$"/api/document-templates/{foreignTemplate}" })
        { using var r = await ca.GetAsync(url); Assert.Equal(HttpStatusCode.NotFound,r.StatusCode); }
        var foreignRug = await RugAsync(cb,"100",2,3,"گبه");
        var ownList = (await GetJsonAsync(ca,"/api/document-templates")).EnumerateArray().Single(x => x.GetProperty("kind").GetInt32() == 2).GetProperty("id").GetGuid();
        await SendAsync(ca,HttpMethod.Post,"/api/documents/preview",new { templateId = ownList,rugIds = new[] { Id(foreignRug) } },HttpStatusCode.NotFound);
        await using var db = factory.CreateDbContext(); Assert.False(await db.Set<ExportRun>().AnyAsync(x => x.TenantId == a.TenantId));
    }
    [PostgresFact]
    public async Task Operator_CannotSeeOrExportMoneyTemplates_OrWriteDesigner()
    {
        var t = await P2Tenant.CreateAsync(factory); using var admin = P1Client.As(factory,t.Admin); using var op = P1Client.As(factory,t.Operator); using var merchant = P1Client.As(factory,t.Merchant);
        await Preset(admin); var all = await GetJsonAsync(admin,"/api/document-templates");
        var cost = all.EnumerateArray().Single(x => x.GetProperty("kind").GetInt32() == 1); var id = cost.GetProperty("id").GetGuid();
        Assert.DoesNotContain((await GetJsonAsync(op,"/api/document-templates")).EnumerateArray(),x => x.GetProperty("usesMoneyFields").GetBoolean());
        using var forbidden = await op.GetAsync($"/api/document-templates/{id}"); Assert.Equal(HttpStatusCode.Forbidden,forbidden.StatusCode);
        var shipment = await NewShipmentAsync(merchant);
        using var xlsx = await op.GetAsync($"/shipments/{shipment}/export.xlsx?template={id}"); Assert.Equal(HttpStatusCode.Forbidden,xlsx.StatusCode);
        using var allowed = await merchant.GetAsync($"/shipments/{shipment}/export.xlsx?template={id}"); Assert.Equal(HttpStatusCode.OK,allowed.StatusCode);
        await SendAsync(op,HttpMethod.Post,"/api/document-templates/validate",new { layout = cost.GetProperty("layout") },HttpStatusCode.Forbidden);
        await SendAsync(op,HttpMethod.Post,"/api/documents/preview",new { layout = cost.GetProperty("layout"),shipmentId = shipment },HttpStatusCode.Forbidden);
    }
    [PostgresFact]
    public async Task FinalizedExports_UseSnapshotAndItsHash()
    {
        var t = await P2Tenant.CreateAsync(factory); using var admin = P1Client.As(factory,t.Admin); using var merchant = P1Client.As(factory,t.Merchant);
        await Preset(admin); var rug = await RugAsync(merchant,"100",2,3,"گبه"); var id = await NewShipmentAsync(merchant);
        Result(await OpsAsync(merchant,id,Add(Id(rug),1))); Result(await OpsAsync(merchant,id,SameAsIntake(await RowAsync(merchant,id,Id(rug)))));
        await SendAsync(merchant,HttpMethod.Post,$"/api/shipments/{id}/finalize",new { version = await ShipmentVersionAsync(merchant,id) });
        var output = await GetJsonAsync(merchant,$"/api/shipments/{id}/documents/preview"); Assert.False(output.GetProperty("isDraft").GetBoolean());
        await using var db = factory.CreateDbContext(); var s = await db.Set<Shipment>().SingleAsync(x => x.Id == id);
        var run = await db.Set<ExportRun>().SingleAsync(x => x.ShipmentId == id); Assert.Equal(s.SnapshotHash,run.DataHash);
        Assert.Contains(output.GetProperty("sheets")[0].GetProperty("rows").EnumerateArray().SelectMany(r => r.GetProperty("cells").EnumerateArray()),c => c.GetProperty("display").GetString() == "270");
    }
}
