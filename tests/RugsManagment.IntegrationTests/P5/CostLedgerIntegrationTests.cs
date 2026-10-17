using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Ledger;
using RugsManagment.Infrastructure.Persistence;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.IntegrationTests.P1;
using RugsManagment.IntegrationTests.P2;
using RugsManagment.IntegrationTests.Support;
using static RugsManagment.IntegrationTests.P2.P2Http;
using static RugsManagment.IntegrationTests.P4a.P4aApi;

namespace RugsManagment.IntegrationTests.P5;

public sealed class CostLedgerIntegrationTests(RugsWebFactory factory) : IClassFixture<RugsWebFactory>
{
    private static async Task<Guid> Category(P1Client client, string name, CostGroup group = CostGroup.Expense)
        => Id(await SendAsync(client, HttpMethod.Post, "/api/cost-categories/quick", new { name, group }));
    private static object Request(Guid category, Guid[] rugs, decimal amount, AllocationMethod method = AllocationMethod.ByArea,
        CostBasis basis = CostBasis.Total, AreaBasis areaBasis = AreaBasis.Effective, Guid? provider = null, Guid? shipment = null, Guid? clientKey = null,
        object[]? pins = null, CurrencyUnit currency = CurrencyUnit.Toman, decimal? fx = null)
        => new { categoryId = category, date = "2026-10-17", basis, amount = basis == CostBasis.Total ? amount : (decimal?)null,
            rate = basis == CostBasis.Total ? null : (decimal?)amount, currency, fxRate = fx, areaBasis, method,
            scope = shipment == null ? CostScope.Selection : CostScope.Shipment, shipmentId = shipment, rugIds = rugs,
            serviceProviderId = provider, clientKey, pins };
    private static async Task<JsonElement> Cost(P1Client client, object request)
        => await SendAsync(client, HttpMethod.Post, "/api/costs", request);
    private static void Shares(JsonElement entry, params (Guid Id, decimal Amount)[] expected)
    {
        var rows = entry.GetProperty("result").GetProperty("shares").EnumerateArray().ToList();
        Assert.Equal(expected.Length, rows.Count);
        foreach (var (id, amount) in expected) Assert.Equal(amount, Dec(rows.Single(r => r.GetProperty("rugId").GetGuid() == id), "amount"));
    }
    private static async Task<JsonElement> Read(P1Client client, Guid id) => await GetJsonAsync(client, $"/api/costs/{id}");
    private async Task<decimal[]> Totals(params Guid[] ids)
    {
        await using var db = factory.CreateDbContext();
        var rows = await db.Set<RugCostTotal>().IgnoreQueryFilters().Where(r => ids.Contains(r.RugId)).ToDictionaryAsync(r => r.RugId, r => r.Total);
        return ids.Select(id => rows[id]).ToArray();
    }
    private static async Task Purchase(P1Client client, Guid id, CostBasis basis, decimal rate)
    {
        var current = await GetJsonAsync(client, $"/api/rugs/{id}/purchase-lines");
        await SendAsync(client, HttpMethod.Put, $"/api/rugs/{id}/purchase-lines", new { version = Version(current), lines = new object[] {
            new { label = "price", basis, rate, sortOrder = 1 }, new { label = "broker", basis = CostBasis.Percent, rate = 2, sortOrder = 2 } } });
    }
    private static async Task Final(P1Client client, Guid id, decimal? length = null, decimal? width = null)
    {
        var rug = await GetJsonAsync(client, $"/api/rugs/{id}");
        await SendAsync(client, HttpMethod.Put, $"/api/rugs/{id}/final-size", new { version = Version(rug), length, width, sameAsIntake = length == null });
    }
    private static async Task Trash(P1Client client, Guid id)
    {
        var response = await P3.P3Grid.SaveAsync(client, [new P3.P3Op(Guid.NewGuid(), Values(), id, Delete: true)], "rugs");
        var result = response.GetProperty("results")[0];
        Assert.Equal(3, result.GetProperty("status").GetInt32());
    }

    [PostgresFact]
    public async Task D7_ThroughApi_AllTwelveSteps_ReceiptFreezeProfitAndLateCostAreExact()
    {
        var tenant = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, tenant.Merchant);
        using var admin = P1Client.As(factory, tenant.Admin);
        var a = Id(await RugAsync(merchant, "3778", 2.08m, 2.10m, "گبه"));
        var b = Id(await RugAsync(merchant, "3911 - 3960", .4m, .4m, "گبه", 50));
        var c = Id(await RugAsync(merchant, "3782", 3.35m, 2.52m, "گبه"));
        var ids = new[] { a, b, c };
        await Purchase(merchant, a, CostBasis.PerM2, 9_000_000m); await Purchase(merchant, b, CostBasis.PerPiece, 300_000m); await Purchase(merchant, c, CostBasis.PerM2, 8_500_000m);
        Assert.Equal(new[] { 40_116_600m, 15_300_000m, 73_174_800m }, await Totals(ids));
        var provider = new RugsManagment.Domain.Entities.ServiceProvider { TenantId = tenant.TenantId, Name = "کارگاه شستشو" };
        await using (var db = factory.CreateDbContext()) { db.ServiceProviders.Add(provider); await db.SaveChangesAsync(); }
        var washing = await Cost(merchant, Request(await Category(merchant, "شستشو"), ids, 85_000m, basis: CostBasis.PerM2, areaBasis: AreaBasis.Intake, provider: provider.Id));
        Shares(washing, (a, 371_450m), (b, 680_000m), (c, 717_400m)); Assert.NotEqual(JsonValueKind.Null, washing.GetProperty("frozenAt").ValueKind);
        var freight = await Cost(merchant, Request(await Category(merchant, "کرایه خارجی"), ids, 10_000_000m));
        Shares(freight, (a, 2_099_952m), (b, 3_844_306m), (c, 4_055_742m));
        Assert.Equal(new[] { 42_588_002m, 19_824_306m, 77_947_942m }, await Totals(ids));
        await Final(merchant, c, 3.30m, 2.50m);
        Shares(await Read(merchant, Id(freight)), (a, 2_119_302m), (b, 3_879_728m), (c, 4_000_970m));
        Assert.Equal(new[] { 42_607_352m, 19_859_728m, 77_893_170m }, await Totals(ids));
        await Trash(merchant, b);
        Shares(await Read(merchant, Id(freight)), (a, 3_462_758m), (c, 6_537_242m));
        Shares(await Read(merchant, Id(washing)), (a, 603_425m), (c, 1_165_425m));
        Assert.Equal(new[] { 44_182_783m, 80_877_467m }, await Totals(a, c));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var statements = await scope.ServiceProvider.GetRequiredService<IServiceProviderRepository>().ListWorkAsync(tenant.TenantId, provider.Id);
            Assert.Equal(1_768_850m, statements.Single().Cost);
        }
        await SendAsync(merchant, HttpMethod.Post, $"/api/grid/rugs/{b}/restore", null);
        Assert.Equal(new[] { 42_607_352m, 19_859_728m, 77_893_170m }, await Totals(ids));
        await AddRateAsync(merchant, "گبه", 45m);
        var shipment = await NewShipmentAsync(merchant);
        Result(await OpsAsync(merchant, shipment, Add(a, 1), Add(b, 1), Add(c, 2)), 2);
        await Final(merchant, a); await Final(merchant, b);
        var customs = await Cost(merchant, Request(await Category(merchant, "گمرک"), [], 3_000_000m, AllocationMethod.ByDeclaredValue, shipment: shipment));
        Shares(customs, (a, 635_790m), (b, 1_163_919m), (c, 1_200_291m));
        var profit = await SendAsync(merchant, HttpMethod.Put, $"/api/shipments/{shipment}/revenue", new { amount = 1800m, currency = CurrencyUnit.Usd, fx = 105_000m, version = await ShipmentVersionAsync(merchant, shipment) });
        Assert.Equal(143_360_250m, Dec(profit, "total")); Assert.Equal(45_639_750m, Dec(profit, "profit")); Assert.Equal(927.90m, Dec(profit, "usdTotal"));
        var finalized = await SendAsync(merchant, HttpMethod.Post, $"/api/shipments/{shipment}/finalize", new { version = await ShipmentVersionAsync(merchant, shipment) });
        Assert.True(finalized.GetProperty("ok").GetBoolean(), finalized.ToString());
        string? hash;
        await using (var db = factory.CreateDbContext()) hash = (await db.Set<Shipment>().SingleAsync(s => s.Id == shipment)).SnapshotHash;
        var late = await Cost(merchant, Request(await Category(merchant, "انبارداری"), [], 600_000m, shipment: shipment));
        Shares(late, (a, 127_158m), (b, 232_784m), (c, 240_058m));
        Assert.Contains(late.GetProperty("result").GetProperty("warnings").EnumerateArray(), w => w.GetProperty("code").GetString() == "W_LATE_COST");
        profit = await GetJsonAsync(merchant, $"/api/shipments/{shipment}/profit"); Assert.Equal(45_039_750m, Dec(profit, "profit"));
        await using (var db = factory.CreateDbContext()) Assert.Equal(hash, (await db.Set<Shipment>().SingleAsync(s => s.Id == shipment)).SnapshotHash);
        var health = await GetJsonAsync(admin, "/api/diagnostics/cost-consistency"); Assert.Empty(health.GetProperty("mismatches").EnumerateArray());
    }

    [PostgresFact]
    public async Task SecurityValidationIdempotencySoftDeleteAndRestore_AreEnforced()
    {
        var tenant = await P2Tenant.CreateAsync(factory); var other = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, tenant.Merchant); using var foreign = P1Client.As(factory, other.Merchant); using var floor = P1Client.As(factory, tenant.Operator);
        var a = Id(await RugAsync(merchant, "1", 1, 1, null)); var b = Id(await RugAsync(merchant, "2", 1, 1, null)); var c = Id(await RugAsync(merchant, "3", 1, 1, null));
        var category = await Category(merchant, "Expense"); var key = Guid.NewGuid();
        var request = Request(category, [a, b, c], -100m, AllocationMethod.Equal, clientKey: key);
        var cost = await Cost(merchant, request); var id = Id(cost); Shares(cost, (a, -34), (b, -33), (c, -33));
        Assert.Equal(id, Id(await Cost(merchant, request)));
        await GetJsonAsync(foreign, $"/api/costs/{id}", HttpStatusCode.NotFound);
        await GetJsonAsync(floor, $"/api/costs/{id}", HttpStatusCode.Forbidden);
        await SendAsync(foreign, HttpMethod.Post, "/api/costs", request, HttpStatusCode.NotFound);
        var invalid = await SendAsync(merchant, HttpMethod.Post, "/api/costs", Request(category, [a], 10, basis: CostBasis.PerM2, pins: [new { rugId = a, amount = 1 }]), HttpStatusCode.BadRequest);
        Assert.Equal("E_PINS_BASIS", invalid.GetProperty("code").GetString());
        invalid = await SendAsync(merchant, HttpMethod.Post, "/api/costs", Request(category, [a], 85, currency: CurrencyUnit.Rial, pins: [new { rugId = a, amount = 10 }]), HttpStatusCode.BadRequest);
        Assert.Equal("E_PINS_EXCEED", invalid.GetProperty("code").GetString());
        invalid = await SendAsync(merchant, HttpMethod.Post, "/api/costs", Request(category, [a], 10, currency: CurrencyUnit.Usd), HttpStatusCode.BadRequest);
        Assert.Equal("E_FX_REQUIRED", invalid.GetProperty("code").GetString());
        var purchase = await Category(merchant, "Purchase", CostGroup.Purchase);
        invalid = await SendAsync(merchant, HttpMethod.Post, "/api/costs", Request(purchase, [a], 100, AllocationMethod.ByPurchaseCost), HttpStatusCode.BadRequest);
        Assert.Equal("E_CIRCULAR", invalid.GetProperty("code").GetString());
        var manual = await Cost(merchant, Request(category, [a], 100, pins: [new { rugId = a, amount = 40 }]));
        Assert.Equal(60, Dec(manual.GetProperty("result"), "unallocated"));
        var deleted = await SendAsync(merchant, HttpMethod.Delete, $"/api/costs/{id}?version={Version(cost)}", null);
        Assert.True(deleted.GetProperty("deleted").GetBoolean()); Assert.Equal(new[] { 40m, 0m, 0m }, await Totals(a, b, c));
        await SendAsync(merchant, HttpMethod.Post, $"/api/costs/{id}/restore", null);
        Assert.Equal(new[] { 6m, -33m, -33m }, await Totals(a, b, c));
    }

    [PostgresFact]
    public async Task BatchExpenseIsDynamic_PurchaseKeepsSnapshot_AfterMoveAndBatchDelete()
    {
        var tenant = await P2Tenant.CreateAsync(factory); using var merchant = P1Client.As(factory, tenant.Merchant); using var admin = P1Client.As(factory, tenant.Admin);
        var a = Id(await RugAsync(merchant, "10", 1, 1, null)); var b = Id(await RugAsync(merchant, "20", 1, 1, null));
        var batch = new RugBatch { TenantId = tenant.TenantId, Number = 100, Name = "batch-cost-test" };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RugBatches.Add(batch); (await db.Rugs.SingleAsync(r => r.Id == a)).BatchId = batch.Id; await uow.SaveChangesAsync();
        }
        var expenseCategory = await Category(merchant, "batch-expense"); var purchaseCategory = await Category(merchant, "batch-purchase", CostGroup.Purchase);
        object BatchRequest(Guid category, decimal amount) => new { categoryId = category, date = "2026-10-17", basis = CostBasis.Total, amount, currency = CurrencyUnit.Toman,
            areaBasis = AreaBasis.Intake, method = AllocationMethod.ByCount, scope = CostScope.IntakeBatch, batchId = batch.Id };
        var expense = await Cost(merchant, BatchRequest(expenseCategory, 100)); var purchase = await Cost(merchant, BatchRequest(purchaseCategory, 1000));
        Shares(expense, (a, 100)); Shares(purchase, (a, 1000));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Rugs.SingleAsync(r => r.Id == b)).BatchId = batch.Id; await uow.SaveChangesAsync();
        }
        Shares(await Read(merchant, Id(expense)), (a, 50), (b, 50)); Shares(await Read(merchant, Id(purchase)), (a, 1000));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RugBatches.Remove(await db.RugBatches.SingleAsync(x => x.Id == batch.Id)); await uow.SaveChangesAsync();
        }
        expense = await Read(merchant, Id(expense)); Assert.Empty(expense.GetProperty("result").GetProperty("shares").EnumerateArray());
        Assert.Equal(100m, Dec(expense.GetProperty("result"), "unallocated"));
        Assert.Contains(expense.GetProperty("result").GetProperty("warnings").EnumerateArray(), w => w.GetProperty("code").GetString() == "W_TARGET_DELETED");
        Shares(await Read(merchant, Id(purchase)), (a, 1000)); Assert.Equal(new[] { 1000m, 0m }, await Totals(a, b));
        var report = await GetJsonAsync(admin, "/api/diagnostics/cost-consistency"); Assert.Empty(report.GetProperty("mismatches").EnumerateArray());
    }

    [PostgresFact]
    public async Task TwoHundredRandomOperations_LeaveZeroCostConsistencyMismatches()
    {
        var tenant = await P2Tenant.CreateAsync(factory); using var merchant = P1Client.As(factory, tenant.Merchant); using var admin = P1Client.As(factory, tenant.Admin);
        var rugs = new List<Guid>(); for (var i = 1; i <= 8; i++) rugs.Add(Id(await RugAsync(merchant, i.ToString(), i / 2m, 1, null)));
        var category = await Category(merchant, "random-expense"); var purchaseCategory = await Category(merchant, "random-purchase", CostGroup.Purchase);
        var ids = new List<Guid>(); var deleted = new HashSet<Guid>(); var random = new Random(20261017);
        await Cost(merchant, Request(category, rugs.ToArray(), 100_000m, AllocationMethod.ByPurchaseCost));
        for (var i = 0; i < 200; i++)
        {
            var rug = rugs[random.Next(rugs.Count)];
            switch (random.Next(5))
            {
                case 0: await Purchase(merchant, rug, CostBasis.PerM2, random.Next(1, 100_000)); break;
                case 1: await Final(merchant, rug, random.Next(5, 500) / 100m, random.Next(5, 300) / 100m); break;
                case 2:
                    var group = random.Next(3) == 0 ? purchaseCategory : category;
                    ids.Add(Id(await Cost(merchant, Request(group, rugs.Where(_ => random.Next(2) == 0).Append(rug).Distinct().ToArray(), random.Next(-100_000, 100_001), (AllocationMethod)random.Next(3)))));
                    break;
                default:
                    if (ids.Count == 0) break;
                    var entry = ids[random.Next(ids.Count)];
                    if (deleted.Remove(entry)) await SendAsync(merchant, HttpMethod.Post, $"/api/costs/{entry}/restore", null);
                    else { await SendAsync(merchant, HttpMethod.Delete, $"/api/costs/{entry}", null); deleted.Add(entry); }
                    break;
            }
            if (i % 20 == 19) { var health = await GetJsonAsync(admin, "/api/diagnostics/cost-consistency"); Assert.Empty(health.GetProperty("mismatches").EnumerateArray()); }
        }
        var report = await GetJsonAsync(admin, "/api/diagnostics/cost-consistency"); Assert.Empty(report.GetProperty("mismatches").EnumerateArray());
    }
}
