using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Ledger;
using RugsManagment.Application.Ledger.Contributors;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.Infrastructure.Persistence;
using RugsManagment.IntegrationTests.Support;

namespace RugsManagment.IntegrationTests.Ledger;

/// <summary>
/// تفاوت مقدار (spec D.4 گام ۱، L.1 «ChangeSetBuilder»، K.2/P2 «Real diffs only»): <c>rugs.Update(rug)</c> روی فرش ردیابی‌شده
/// فقط ستون‌هایی را می‌آورد که واقعاً عوض شده‌اند (IsModified نادیده)؛ فقط UpdatedAt → هیچ؛ decimal با مقدار؛ موجودیتِ
/// وصل‌شده بی بارگذاری → «نامعلوم» و اثر کامل؛ ردیف مشتق‌شده هرگز در ChangeSet نیست.
/// </summary>
public sealed class ChangeSetBuilderTests(LedgerFixture fixture) : IClassFixture<LedgerFixture>
{
    private async Task<(Guid Tenant, Rug Rug)> SeedAsync()
    {
        var tenant = await fixture.NewTenantAsync();
        var rug = LedgerFixture.NewRug(tenant, 2.10m, 1.50m, "55");
        rug.Notes = "قبلی";
        LedgerFixture.Line(rug, CostBasis.Total, 10m);
        await fixture.SaveNewAsync(tenant, rug);
        return (tenant, rug);
    }

    private static async Task<ChangeSet> BuildAsync(IServiceProvider services)
        => await services.GetRequiredService<ILedgerSession>().BuildChangeSetAsync(CancellationToken.None);

    [PostgresFact]
    public async Task UpdateOnATrackedRug_WithOnlyNotesChanged_ListsOnlyNotes_AndHasNoImpact()
    {
        var (tenant, seeded) = await SeedAsync();
        await using var scope = fixture.Scope(tenant);
        var rugs = scope.ServiceProvider.GetRequiredService<IRugRepository>();
        var rug = (await rugs.GetWithWorkflowAsync(seeded.Id, tenant))!;

        rug.Notes = "تازه";
        rug.UpdatedAt = DateTimeOffset.UtcNow;
        rugs.Update(rug); // همهٔ ستون‌ها IsModified می‌شوند؛ تفاوت مقدار نباید فریب بخورد

        var changes = await BuildAsync(scope.ServiceProvider);
        var entry = Assert.Single(changes.Entries);
        Assert.Equal(ChangeKind.Modified, entry.Kind);
        Assert.Equal([nameof(Rug.Notes)], entry.Changes.Keys);
        Assert.Equal(tenant, entry.TenantId);

        var context = new LedgerContext(tenant, changes, DateTimeOffset.UtcNow);
        await new RugImpactContributor().ContributeAsync(context, CancellationToken.None);
        Assert.True(context.Impact.IsEmpty);
    }

    [PostgresFact]
    public async Task OnlyUpdatedAt_IsAnEmptyChangeSet_AndDecimalsCompareByValue()
    {
        var (tenant, seeded) = await SeedAsync();
        await using var scope = fixture.Scope(tenant);
        var rugs = scope.ServiceProvider.GetRequiredService<IRugRepository>();
        var rug = (await rugs.GetWithWorkflowAsync(seeded.Id, tenant))!;

        rug.UpdatedAt = DateTimeOffset.UtcNow;
        rug.SetIntakeSize(2.1m, 1.500m); // همان مقدارها با مقیاس دیگر
        rugs.Update(rug);

        Assert.True((await BuildAsync(scope.ServiceProvider)).IsEmpty);
    }

    [PostgresFact]
    public async Task AnEntityAttachedWithoutATrackedLoad_IsUnknown()
    {
        var (tenant, seeded) = await SeedAsync();
        await using var scope = fixture.Scope(tenant);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _ = scope.ServiceProvider.GetRequiredService<ILedgerSession>(); // ردیاب پیش از وصل کردن ساخته شده باشد (مثل UnitOfWork)

        var detached = await db.Set<RugPurchaseLine>().AsNoTracking().SingleAsync(l => l.RugId == seeded.Id);
        detached.Rate = 20m;
        db.Update(detached);

        var entry = Assert.Single((await BuildAsync(scope.ServiceProvider)).Entries);
        Assert.Equal(ChangeKind.Unknown, entry.Kind);
        Assert.Equal(tenant, entry.TenantId);
    }

    [PostgresFact]
    public async Task UpdateOnAnUntrackedRug_IsRefused()
    {
        var (tenant, seeded) = await SeedAsync();
        await using var scope = fixture.Scope(tenant);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var untracked = await db.Rugs.AsNoTracking().SingleAsync(r => r.Id == seeded.Id);

        Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IRugRepository>().Update(untracked));
    }

    [PostgresFact]
    public async Task StepTenant_IsResolvedThroughItsRug_AndDerivedRowsAreNeverListed()
    {
        var (tenant, seeded) = await SeedAsync();
        await using var scope = fixture.Scope(tenant);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // مرحله‌ای که فرشش ردیابی نمی‌شود: کارگاه از دیتابیس
        db.RugWorkflowSteps.Add(new RugWorkflowStep { RugId = seeded.Id, ProcessStepTypeId = fixture.WashTypeId, OrderIndex = 0 });
        // ردیف مشتق‌شدهٔ ردیابی‌شده و عوض‌شده: هرگز در ChangeSet نیست (پاس به خودش برنمی‌گردد)
        var total = await db.Set<RugCostTotal>().IgnoreQueryFilters().SingleAsync(t => t.RugId == seeded.Id);
        total.Total = 999m;

        var changes = await BuildAsync(scope.ServiceProvider);
        var entry = Assert.Single(changes.Entries);
        Assert.IsType<RugWorkflowStep>(entry.Entity);
        Assert.Equal(tenant, entry.TenantId);
    }
}
