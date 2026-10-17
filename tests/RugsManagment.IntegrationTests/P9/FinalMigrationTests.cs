using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using RugsManagment.Application.Abstractions;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.Infrastructure.Persistence.Seed;
using RugsManagment.IntegrationTests.Support;
namespace RugsManagment.IntegrationTests.P9;

public sealed class FinalMigrationTests(TestDatabase database) : IClassFixture<TestDatabase>
{
    private const string M6 = "20261017045204_Import";
    private const string M7 = "20261017091136_DropRugPurchaseCost";
    [PostgresFact]
    public async Task M7_IsBackupGated_AndDownRebuildsPurchaseColumnBeforeReapplying()
    {
        await using var db = database.CreateContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(M6);
        var tenant = new Tenant { Name = "migration", Slug = Guid.NewGuid().ToString("N") };
        var rug = new Rug { TenantId = tenant.Id, Sku = "M7-1" }; rug.SetIntakeSize(2m, 3m);
        db.Tenants.Add(tenant); db.Rugs.Add(rug);
        db.Set<RugPurchaseLine>().Add(new RugPurchaseLine { TenantId = tenant.Id, RugId = rug.Id, Label = "purchase", Basis = CostBasis.Total, Rate = 1250m, Amount = 1250m });
        await db.SaveChangesAsync();
        var migrations = db.GetService<IMigrationsAssembly>().Migrations;
        var gates = Migrator.FindBackupGates(migrations, await db.Database.GetPendingMigrationsAsync());
        Assert.Contains(gates, g => g.MigrationId == M7 && g.Table == "Rugs");
        await Assert.ThrowsAsync<MigrationBackupRequiredException>(() => Migrator.CheckBackupGatesAsync(db, migrations,
            [M7], null));
        await migrator.MigrateAsync();
        Assert.False(await Exists(db, "PurchaseCost"));
        await migrator.MigrateAsync(M6);
        Assert.True(await Exists(db, "PurchaseCost"));
        var amount = await db.Database.SqlQuery<decimal>($"""SELECT "PurchaseCost" AS "Value" FROM "Rugs" WHERE "Id" = {rug.Id}""").SingleAsync();
        Assert.Equal(1250m, amount);
        await migrator.MigrateAsync();
        Assert.False(await Exists(db, "PurchaseCost"));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }
    private static Task<bool> Exists(Infrastructure.Persistence.AppDbContext db, string column)
        => db.Database.SqlQuery<bool>($"""
            SELECT EXISTS(SELECT 1 FROM information_schema.columns
              WHERE table_schema='public' AND table_name='Rugs' AND column_name={column}) AS "Value"
            """).SingleAsync();
}
