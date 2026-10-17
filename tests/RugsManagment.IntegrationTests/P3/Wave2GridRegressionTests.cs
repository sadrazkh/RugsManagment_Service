using Microsoft.EntityFrameworkCore;
using RugsManagment.Domain.Enums;
using RugsManagment.IntegrationTests.P1;
using RugsManagment.IntegrationTests.P2;
using RugsManagment.IntegrationTests.Support;
using static RugsManagment.IntegrationTests.P2.P2Http;
using static RugsManagment.IntegrationTests.P3.P3Grid;

namespace RugsManagment.IntegrationTests.P3;

public sealed class Wave2GridRegressionTests(RugsWebFactory factory) : IClassFixture<RugsWebFactory>
{
    [PostgresFact]
    public async Task LongSeller_DoesNotLeavePurchaseOrMutateInvalidRow_AndOtherRowsSave()
    {
        var tenant = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, tenant.Merchant);
        var invalid = P3Op.New(("rug.serial", "7002"), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m),
            ("batch.seller", new string('x', 250)), ("purchase.price", Price(100m, CostBasis.PerM2)));
        var response = Results(await SaveAsync(merchant, [
            P3Op.New(("rug.serial", "7001"), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m)), invalid,
            P3Op.New(("rug.serial", "7003"), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m))]));
        Assert.Equal([0, 1, 0], response.Select(Status));
        var id = RugId(response[0]);
        var edited = Results(await SaveAsync(merchant, [new P3Op(Guid.NewGuid(),
            Values(("rug.origin", "changed"), ("batch.seller", new string('x', 250))), id, RowVersion(response[0]))]))[0];
        Assert.Equal(1, Status(edited));
        await using var db = factory.CreateDbContext();
        Assert.Null((await db.Rugs.SingleAsync(r => r.Id == id)).Origin);
        Assert.Equal(2, await db.Rugs.CountAsync(r => r.TenantId == tenant.TenantId));
    }

    [PostgresFact]
    public async Task RetriedCreate_ReturnsCurrentRow_WithoutOverwritingLaterEdit()
    {
        var tenant = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, tenant.Merchant);
        var create = P3Op.New(("rug.serial", "9001"), ("rug.intakeLength", 2m), ("rug.intakeWidth", 3m), ("rug.origin", "original"));
        var first = Results(await SaveAsync(merchant, [create]))[0];
        AllSaved(await SaveAsync(merchant, [new P3Op(Guid.NewGuid(), Values(("rug.origin", "newer")),
            RugId(first), RowVersion(first), Values(("rug.origin", "original")))]));
        var retry = Results(await SaveAsync(merchant, [create]))[0];
        Assert.Equal("newer", Current(retry, "rug.origin").GetString());
        await using var db = factory.CreateDbContext();
        Assert.Equal("newer", (await db.Rugs.SingleAsync(r => r.Id == RugId(first))).Origin);
    }
}
