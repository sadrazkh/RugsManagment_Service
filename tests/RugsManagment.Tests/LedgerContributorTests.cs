using RugsManagment.Application.Common;
using RugsManagment.Application.Ledger;
using RugsManagment.Application.Ledger.Contributors;
using RugsManagment.Application.Ledger.Guards;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Tests;

/// <summary>
/// contributorها و قفل فاز ۲ روی ChangeSetهای ساختگی (spec L.1 «Impact contributors»، «Guards»؛ ردیف‌های D.5 ۱ تا ۱۱ و ۲۲،
/// با E1): هر ردیف همان مجموعه‌های اثر را می‌سازد که جدول می‌گوید، و ستون‌های توصیفی هیچ اثری ندارند.
/// </summary>
public class LedgerContributorTests
{
    private static readonly Guid TenantGuid = Guid.Parse("00000000-0000-0000-0000-0000000000b1");

    private static Rug NewRug()
    {
        var rug = new Rug { TenantId = TenantGuid, Sku = "RUG-T-1" };
        rug.SetIntakeSize(2.08m, 2.10m);
        return rug;
    }

    private static EntityChange Modified(object entity, params (string Name, object? Original, object? Current)[] changes)
        => new(entity, ChangeKind.Modified, TenantGuid,
            changes.ToDictionary(c => c.Name, c => new PropertyChange(c.Name, c.Original, c.Current), StringComparer.Ordinal),
            changes.ToDictionary(c => c.Name, c => c.Original, StringComparer.Ordinal));

    private static async Task<LedgerImpact> ContributeAsync(IChangeImpactContributor contributor, params EntityChange[] changes)
    {
        var context = new LedgerContext(TenantGuid, new ChangeSet(changes), DateTimeOffset.UtcNow);
        await contributor.ContributeAsync(context, CancellationToken.None);
        return context.Impact;
    }

    // ── RugImpact ──

    [Fact]
    public async Task RugAdded_GetsFullImpact()
    {
        var rug = NewRug();
        var impact = await ContributeAsync(new RugImpactContributor(), new EntityChange(rug, ChangeKind.Added, TenantGuid));

        Assert.Contains(rug.Id, impact.CostTotalRugs);
        Assert.Contains(rug.Id, impact.PurchaseLineRugs);
        Assert.Contains(rug.Id, impact.SerialCheckRugs);
    }

    [Fact]
    public async Task UnknownRug_GetsFullImpact()
    {
        var rug = NewRug();
        var impact = await ContributeAsync(new RugImpactContributor(), new EntityChange(rug, ChangeKind.Unknown, TenantGuid));

        Assert.Contains(rug.Id, impact.PurchaseLineRugs);
        Assert.Contains(rug.Id, impact.CostTotalRugs);
        Assert.Contains(rug.Id, impact.SerialCheckRugs);
    }

    [Theory]
    [InlineData(nameof(Rug.IntakeLength))]
    [InlineData(nameof(Rug.IntakeWidth))]
    [InlineData(nameof(Rug.Quantity))]
    public async Task IntakeSizeOrQuantity_RecomputesPurchaseLinesAndTotal(string property)
    {
        var rug = NewRug();
        var impact = await ContributeAsync(new RugImpactContributor(), Modified(rug, (property, 1m, 2m)));

        Assert.Contains(rug.Id, impact.PurchaseLineRugs);
        Assert.Contains(rug.Id, impact.CostTotalRugs);
        Assert.Contains(rug.Id, impact.MemberRugs);
        Assert.Empty(impact.SerialCheckRugs);
    }

    [Fact]
    public async Task FinalSize_RecomputesTheTotalOnly()
    {
        var rug = NewRug();
        var impact = await ContributeAsync(new RugImpactContributor(),
            Modified(rug, (nameof(Rug.FinalLength), null, 3.30m), (nameof(Rug.LengthMeters), 3.35m, 3.30m)));

        Assert.Contains(rug.Id, impact.CostTotalRugs);
        Assert.Empty(impact.PurchaseLineRugs); // هر متر روی اندازهٔ اول است (M.2 #12)
    }

    /// <summary>E1: تغییر شماره به‌تنهایی سنجش همپوشانی می‌خواهد (قبلاً فقط «قفل‌ها» بود و مرحله‌ای اجرا نمی‌شد).</summary>
    [Fact]
    public async Task SerialChange_IsOverlapChecked()
    {
        var rug = NewRug();
        var impact = await ContributeAsync(new RugImpactContributor(), Modified(rug, (nameof(Rug.SerialNo), "3778", "3779")));

        Assert.Contains(rug.Id, impact.SerialCheckRugs);
        Assert.Contains(rug.Id, impact.MemberRugs);
        Assert.False(impact.IsEmpty);
    }

    [Fact]
    public async Task Restore_RechecksTheSerial_TrashDoesNot()
    {
        var rug = NewRug();
        var restored = await ContributeAsync(new RugImpactContributor(),
            Modified(rug, (nameof(Rug.DeletedAt), DateTimeOffset.UtcNow, null)));
        Assert.Contains(rug.Id, restored.SerialCheckRugs);

        rug.DeletedAt = DateTimeOffset.UtcNow;
        var trashed = await ContributeAsync(new RugImpactContributor(),
            Modified(rug, (nameof(Rug.DeletedAt), null, rug.DeletedAt)));
        Assert.Empty(trashed.SerialCheckRugs);
        Assert.Contains(rug.Id, trashed.MemberRugs);
    }

    [Fact]
    public async Task BatchMove_ListsTheOldAndNewBatch()
    {
        var rug = NewRug();
        var oldBatch = Guid.NewGuid();
        rug.BatchId = Guid.NewGuid();
        var impact = await ContributeAsync(new RugImpactContributor(), Modified(rug, (nameof(Rug.BatchId), oldBatch, rug.BatchId)));

        Assert.Equal(new[] { oldBatch, rug.BatchId!.Value }.Order(), impact.Batches.Order());
        Assert.Empty(impact.CostTotalRugs);
    }

    /// <summary>D.5 #8: توصیف، وضعیت و یادداشت فقط از قفل‌ها می‌گذرند؛ هیچ مرحله‌ای اجرا نمی‌شود.</summary>
    [Fact]
    public async Task DescriptiveFields_HaveNoImpact()
    {
        var rug = NewRug();
        var impact = await ContributeAsync(new RugImpactContributor(),
            Modified(rug, (nameof(Rug.Notes), "a", "b"), (nameof(Rug.Title), "x", "y"), (nameof(Rug.Status), RugStatus.Draft, RugStatus.Archived)));

        Assert.True(impact.IsEmpty);
    }

    // ── سطر خرید، مرحله، فروش ──

    [Fact]
    public async Task PurchaseLineCrud_RecomputesItsRug_ButLabelEditsDoNot()
    {
        var rugId = Guid.NewGuid();
        var line = new RugPurchaseLine { RugId = rugId, TenantId = TenantGuid, Basis = CostBasis.PerM2, Rate = 1m };

        var added = await ContributeAsync(new PurchaseLineImpactContributor(), new EntityChange(line, ChangeKind.Added, TenantGuid));
        Assert.Contains(rugId, added.PurchaseLineRugs);
        Assert.Contains(rugId, added.CostTotalRugs);

        var deleted = await ContributeAsync(new PurchaseLineImpactContributor(), new EntityChange(line, ChangeKind.Deleted, TenantGuid));
        Assert.Contains(rugId, deleted.CostTotalRugs);

        var rate = await ContributeAsync(new PurchaseLineImpactContributor(), Modified(line, (nameof(RugPurchaseLine.Rate), 1m, 2m)));
        Assert.Contains(rugId, rate.PurchaseLineRugs);

        var label = await ContributeAsync(new PurchaseLineImpactContributor(), Modified(line, (nameof(RugPurchaseLine.Label), "a", "b")));
        Assert.True(label.IsEmpty);
    }

    [Fact]
    public async Task StepCostOrStatus_RecomputesTheTotal_NotesDoNot()
    {
        var rugId = Guid.NewGuid();
        var step = new RugWorkflowStep { RugId = rugId };

        var status = await ContributeAsync(new StepImpactContributor(),
            Modified(step, (nameof(RugWorkflowStep.Status), WorkflowStepStatus.Pending, WorkflowStepStatus.InProgress)));
        Assert.Contains(rugId, status.CostTotalRugs);

        var cost = await ContributeAsync(new StepImpactContributor(),
            Modified(step, (nameof(RugWorkflowStep.ManualCostOverride), null, 5m)));
        Assert.Contains(rugId, cost.CostTotalRugs);

        var notes = await ContributeAsync(new StepImpactContributor(), Modified(step, (nameof(RugWorkflowStep.Notes), null, "x")));
        Assert.True(notes.IsEmpty);

        var removed = await ContributeAsync(new StepImpactContributor(), new EntityChange(step, ChangeKind.Deleted, TenantGuid));
        Assert.Contains(rugId, removed.CostTotalRugs);
    }

    [Fact]
    public async Task SaleAdded_SnapshotsTheCost_EditDoesNot()
    {
        var rugId = Guid.NewGuid();
        var sale = new RugSale { RugId = rugId, TenantId = TenantGuid };

        var added = await ContributeAsync(new SaleImpactContributor(), new EntityChange(sale, ChangeKind.Added, TenantGuid));
        Assert.Contains(rugId, added.SaleAddedRugs);
        Assert.Contains(rugId, added.CostTotalRugs);

        var edited = await ContributeAsync(new SaleImpactContributor(), Modified(sale, (nameof(RugSale.SalePrice), 1m, 2m)));
        Assert.True(edited.IsEmpty);

        var cancelled = await ContributeAsync(new SaleImpactContributor(), new EntityChange(sale, ChangeKind.Deleted, TenantGuid));
        Assert.Empty(cancelled.SaleAddedRugs);
        Assert.Contains(rugId, cancelled.CostTotalRugs);
    }

    // ── قفل رقم اعشار و واحد پول (D.5 #22) ──

    private sealed class Probe(bool any) : IStoredMoneyProbe
    {
        public Task<bool> AnyAsync(Guid tenantId, CancellationToken cancellationToken) => Task.FromResult(any);
    }

    private static Task CheckAsync(bool moneyExists, params EntityChange[] changes)
    {
        var context = new LedgerContext(TenantGuid, new ChangeSet(changes), DateTimeOffset.UtcNow);
        return new PreferencesLockGuard([new Probe(false), new Probe(moneyExists)]).CheckAsync(context, CancellationToken.None);
    }

    [Fact]
    public async Task MoneyScaleChange_IsRefusedOnceMoneyExists()
    {
        var preferences = new TenantPreferences { TenantId = TenantGuid, MoneyScale = 2 };
        var change = Modified(preferences, (nameof(TenantPreferences.MoneyScale), (short)0, (short)2));

        var ex = await Assert.ThrowsAsync<DomainConflictException>(() => CheckAsync(moneyExists: true, change));
        Assert.Equal(Messages.MoneyLocked.Text, ex.Message);
        Assert.Equal("moneyScale", ex.Field);

        await CheckAsync(moneyExists: false, change); // هنوز مبلغی نیست: آزاد
    }

    [Fact]
    public async Task CurrencyChange_IsRefusedOnceMoneyExists()
    {
        var tenant = new Tenant { Currency = CurrencyUnit.Rial };
        var change = new EntityChange(tenant, ChangeKind.Modified, tenant.Id,
            new Dictionary<string, PropertyChange>
            {
                [nameof(Tenant.Currency)] = new(nameof(Tenant.Currency), CurrencyUnit.Toman, CurrencyUnit.Rial)
            });
        var context = new LedgerContext(tenant.Id, new ChangeSet([change]), DateTimeOffset.UtcNow);

        var ex = await Assert.ThrowsAsync<DomainConflictException>(
            () => new PreferencesLockGuard([new Probe(true)]).CheckAsync(context, CancellationToken.None));
        Assert.Equal("currency", ex.Field);
    }

    [Fact]
    public async Task OtherPreferenceChanges_PassEvenWithMoney()
        => await CheckAsync(moneyExists: true,
            Modified(new TenantPreferences { TenantId = TenantGuid }, (nameof(TenantPreferences.SkuPrefix), "RUG", "FRS")));
}
