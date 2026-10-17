using System.Net;
using Microsoft.EntityFrameworkCore;
using RugsManagment.IntegrationTests.P1;
using RugsManagment.IntegrationTests.P2;
using RugsManagment.IntegrationTests.Support;
using static RugsManagment.IntegrationTests.P2.P2Http;
using static RugsManagment.IntegrationTests.P4a.P4aApi;

namespace RugsManagment.IntegrationTests.P4b;

public sealed class Wave2BaleRegressionTests(RugsWebFactory factory) : IClassFixture<RugsWebFactory>
{
    [PostgresFact]
    public async Task SplittingBaledLot_KeepsBothPartsInTheSameBale_AndPreservesTotals()
    {
        var tenant = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, tenant.Merchant);
        await AddRateAsync(merchant, "origin", 45.55m);
        var lot = await RugAsync(merchant, "3911 - 3960", 0.4m, 0.4m, "origin", 50);
        var shipment = await NewShipmentAsync(merchant);
        Result(await OpsAsync(merchant, shipment, Add(Id(lot), 1)));
        var current = await GetJsonAsync(merchant, $"/api/rugs/{Id(lot)}");
        var split = await SendAsync(merchant, HttpMethod.Post, $"/api/rugs/{Id(lot)}/split",
            new { quantity = 20, version = Version(current) });
        var rows = await RowsAsync(merchant, shipment);
        Assert.Equal(2, rows.Count);
        Assert.Equal([Id(lot), Id(split.GetProperty("created"))], rows.Select(r => r.GetProperty("rugId").GetGuid()));
        var totals = (await ShipmentAsync(merchant, shipment)).GetProperty("totals");
        Assert.Equal(50, totals.GetProperty("pieces").GetInt32());
        Assert.Equal(8m, Dec(totals, "areaM2"));
        Assert.Equal(364.50m, Dec(totals, "usdTotal"));
    }

    [PostgresFact]
    public async Task ConcurrentBaleCreation_AllocatesDistinctNumbers_WithoutServerErrors()
    {
        var tenant = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, tenant.Merchant);
        var shipment = await NewShipmentAsync(merchant);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            using var parallel = P1Client.As(factory, tenant.Merchant);
            await SendAsync(parallel, HttpMethod.Post, $"/api/shipments/{shipment}/bales", new { }, HttpStatusCode.OK);
        }));
        var bales = await BalesAsync(merchant, shipment);
        Assert.Equal(Enumerable.Range(1, 8), bales.EnumerateArray().Select(b => b.GetProperty("number").GetInt32()));
    }
}
