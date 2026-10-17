using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RugsManagment.Application.DTOs.Import;
using RugsManagment.Application.Features.Import;
using RugsManagment.Domain.Entities;
using RugsManagment.Infrastructure.Persistence;
using RugsManagment.Application.Abstractions;
using RugsManagment.Infrastructure.Features.Import;
using RugsManagment.IntegrationTests.P1;
using RugsManagment.IntegrationTests.P2;
using RugsManagment.IntegrationTests.P8.Fixtures;
using RugsManagment.IntegrationTests.Support;
using static RugsManagment.IntegrationTests.P2.P2Http;
namespace RugsManagment.IntegrationTests.P8;

public sealed class ImportApiTests(RugsWebFactory factory) : IClassFixture<RugsWebFactory>
{
    private static async Task<JsonElement> Upload(P1Client client)
    {
        var tokens = await AntiforgeryHelper.FetchAsync(client.Http, "/Account/Profile");
        using var input = SampleSheetBuilder.Build();
        using var body = new MultipartFormDataContent();
        var content = new ByteArrayContent(input.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        body.Add(content, "file", "sample.xlsx");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/imports") { Content = body };
        request.Headers.Add("X-CSRF-TOKEN", tokens.FormToken);
        if (tokens.CookieHeader.Length > 0) request.Headers.Add("Cookie", tokens.CookieHeader);
        using var response = await client.Http.SendAsync(request);
        return await JsonAsync(response);
    }
    [PostgresFact]
    public async Task MerchantSamplePreviewCommitDoubleCommitAndUndo_AreAtomic()
    {
        var tenant = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, tenant.Merchant);
        var job = await Upload(merchant); var id = Id(job);
        var preview = await SendAsync(merchant, HttpMethod.Post, $"/api/imports/{id}/preview", new { sheet = "Sheet1" });
        var counts = preview.GetProperty("counts");
        Assert.Equal(161, counts.GetProperty("lines").GetInt32()); Assert.Equal(242, counts.GetProperty("pieces").GetInt32());
        Assert.Equal(464.74m, counts.GetProperty("areaM2").GetDecimal()); Assert.Equal(20322.50m, counts.GetProperty("usdTotal").GetDecimal());
        Assert.Equal(0, counts.GetProperty("errors").GetInt32());
        var version = Version(preview);
        var result = await SendAsync(merchant, HttpMethod.Post, $"/api/imports/{id}/commit", new { saveMapping = true, version });
        Assert.Equal(161, result.GetProperty("createdRugs").GetInt32());
        var shipment = await GetJsonAsync(merchant, $"/api/shipments/{result.GetProperty("shipmentId").GetGuid()}");
        Assert.Equal("2026-09-06", shipment.GetProperty("documentDate").GetString());
        using (var exported = await merchant.GetAsync($"/shipments/{result.GetProperty("shipmentId").GetGuid()}/export.xlsx"))
        {
            Assert.Equal(HttpStatusCode.OK, exported.StatusCode);
            using var bytes = new MemoryStream(await exported.Content.ReadAsByteArrayAsync());
            var sheets = await new ClosedXmlWorkbookReader().ReadAsync(bytes);
            var main = sheets[0]; var header = HeaderDictionary.Detect(main)!.Value;
            var roundtrip = ImportPlanner.Plan(main, header, HeaderDictionary.Map(main, header), new ImportOptions());
            Assert.Equal(161, roundtrip.Counts.Lines); Assert.Equal(242, roundtrip.Counts.Pieces);
            Assert.Equal(464.74m, roundtrip.Counts.AreaM2); Assert.Equal(20322.50m, roundtrip.Counts.UsdTotal);
            Assert.Equal(0, roundtrip.Counts.Errors);
            Assert.All(roundtrip.Reconciliation, row => Assert.True(row.Ok, $"roundtrip bale {row.Bale}"));
        }
        await SendAsync(merchant, HttpMethod.Post, $"/api/imports/{id}/commit", new { saveMapping = true, version }, HttpStatusCode.Conflict);
        var undone = await SendAsync(merchant, HttpMethod.Post, $"/api/imports/{id}/undo", new { });
        Assert.True(undone.GetProperty("ok").GetBoolean());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db.Rugs.IgnoreQueryFilters().CountAsync(r => r.TenantId == tenant.TenantId));
        Assert.Equal(0, await db.Set<Shipment>().CountAsync(r => r.TenantId == tenant.TenantId));
        Assert.Equal(0, await db.Set<ShipmentLine>().IgnoreQueryFilters().CountAsync(r => r.TenantId == tenant.TenantId));
    }
    [PostgresFact]
    public async Task ImportEndpointsRequireBusiness_AndForeignIdsAre404()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var op = P1Client.As(factory, t.Operator);
        await GetJsonAsync(op, "/api/imports", HttpStatusCode.Forbidden);
        await SendAsync(op, HttpMethod.Post, $"/api/imports/{Guid.NewGuid()}/undo", new { }, HttpStatusCode.Forbidden);
        var job = await Upload(merchant);
        var foreign = await P2Tenant.CreateAsync(factory);
        using var other = P1Client.As(factory, foreign.Merchant);
        await GetJsonAsync(other, $"/api/imports/{Id(job)}", HttpStatusCode.NotFound);
        await SendAsync(other, HttpMethod.Post, $"/api/imports/{Id(job)}/preview", new { sheet = "Sheet1" }, HttpStatusCode.NotFound);
        await SendAsync(other, HttpMethod.Post, $"/api/imports/{Id(job)}/undo", new { }, HttpStatusCode.NotFound);
    }
    [PostgresFact]
    public async Task StockOnlyUndoDeletesItsBatch()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        var job = await Upload(merchant); var id = Id(job);
        var p = await SendAsync(merchant, HttpMethod.Post, $"/api/imports/{id}/preview", new { sheet = "Sheet1", options = new { mode = 1 } });
        var result = await SendAsync(merchant, HttpMethod.Post, $"/api/imports/{id}/commit", new { saveMapping = false, version = Version(p) });
        Assert.Equal(JsonValueKind.Null, result.GetProperty("shipmentId").ValueKind);
        var undo = await SendAsync(merchant, HttpMethod.Post, $"/api/imports/{id}/undo", new { });
        Assert.True(undo.GetProperty("ok").GetBoolean());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.RugBatches.AnyAsync(b => b.TenantId == t.TenantId));
    }
    [PostgresFact]
    public async Task AttachedRug_RetainsIntakeAndUndoRestoresPriorFinalSize()
    {
        var t = await P2Tenant.CreateAsync(factory); using var merchant = P1Client.As(factory, t.Merchant);
        var original = await CreateRugAsync(merchant, Values(("rug.serial", "3778"), ("rug.intakeLength", 3m), ("rug.intakeWidth", 2m), ("rug.origin", "گبه")));
        var rugId = Id(original);
        original = await SendAsync(merchant, HttpMethod.Put, $"/api/rugs/{rugId}/final-size", new { length = 2.5m, width = 1.5m, version = Version(original) });
        var job = await Upload(merchant); var id = Id(job);
        var p = await SendAsync(merchant, HttpMethod.Post, $"/api/imports/{id}/preview", new { sheet = "Sheet1" });
        Assert.Equal(1, p.GetProperty("counts").GetProperty("attach").GetInt32());
        var result = await SendAsync(merchant, HttpMethod.Post, $"/api/imports/{id}/commit", new { saveMapping = false, version = Version(p) });
        Assert.Equal(160, result.GetProperty("createdRugs").GetInt32());
        var after = await GetJsonAsync(merchant, $"/api/rugs/{rugId}");
        Assert.Equal(3m, Dec(after, "intakeLength")); Assert.Equal(2.08m, Dec(after, "finalLength"));
        Assert.True((await SendAsync(merchant, HttpMethod.Post, $"/api/imports/{id}/undo", new { })).GetProperty("ok").GetBoolean());
        var restored = await GetJsonAsync(merchant, $"/api/rugs/{rugId}");
        Assert.Equal(2.5m, Dec(restored, "finalLength")); Assert.Equal(1.5m, Dec(restored, "finalWidth"));
    }
    [PostgresFact]
    public async Task RevenueLineEditsAndNewPhotosBlockUndoWithoutLosingData()
    {
        var t = await P2Tenant.CreateAsync(factory); using var merchant = P1Client.As(factory, t.Merchant);
        var job = await Upload(merchant); var id = Id(job);
        var p = await SendAsync(merchant, HttpMethod.Post, $"/api/imports/{id}/preview", new { sheet = "Sheet1" });
        var result = await SendAsync(merchant, HttpMethod.Post, $"/api/imports/{id}/commit", new { saveMapping = false, version = Version(p) });
        var shipmentId = result.GetProperty("shipmentId").GetGuid();
        var shipment = await GetJsonAsync(merchant, $"/api/shipments/{shipmentId}");
        await SendAsync(merchant, HttpMethod.Put, $"/api/shipments/{shipmentId}/revenue", new { amount = 1000m, currency = 2, fx = 100000m, version = Version(shipment) });
        Assert.False((await SendAsync(merchant, HttpMethod.Post, $"/api/imports/{id}/undo", new { })).GetProperty("ok").GetBoolean());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1000m, (await db.Set<Shipment>().SingleAsync(s => s.Id == shipmentId)).RevenueAmount);
        Assert.Equal(161, await db.Rugs.CountAsync(r => r.TenantId == t.TenantId));

        var other = await P2Tenant.CreateAsync(factory); using var otherMerchant = P1Client.As(factory, other.Merchant);
        var photoJob = await Upload(otherMerchant); var photoId = Id(photoJob);
        var photoPreview = await SendAsync(otherMerchant, HttpMethod.Post, $"/api/imports/{photoId}/preview", new { sheet = "Sheet1", options = new { mode = 1 } });
        await SendAsync(otherMerchant, HttpMethod.Post, $"/api/imports/{photoId}/commit", new { saveMapping = false, version = Version(photoPreview) });
        var rug = await db.Rugs.FirstAsync(r => r.TenantId == other.TenantId);
        var version = db.Entry(rug).Property<uint>("xmin").CurrentValue;
        var image = new RugImage { TenantId = other.TenantId, RugId = rug.Id, FileName = "preserve.webp" };
        db.RugImages.Add(image); await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        Assert.Equal(version, db.Entry(rug).Property<uint>("xmin").CurrentValue);
        Assert.False((await SendAsync(otherMerchant, HttpMethod.Post, $"/api/imports/{photoId}/undo", new { })).GetProperty("ok").GetBoolean());
        Assert.True(await db.RugImages.AnyAsync(i => i.Id == image.Id));
    }
}
