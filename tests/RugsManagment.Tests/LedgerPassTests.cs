using RugsManagment.Application.Ledger;
using RugsManagment.Application.Ledger.Contributors;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Tests;

/// <summary>
/// ترتیب پاس سازگاری (spec D.4) با جلسه و repository ساختگی: fast path، قفل پیش از contributorها، flush پیش از مرحله‌ها،
/// مرحله‌ها به ترتیب <see cref="LedgerStageOrder"/>، هیچ مرحله‌ای برای اثر خالی، و برگرداندن و پاک کردن ردیابی با هر خطا.
/// </summary>
public class LedgerPassTests
{
    private static readonly Guid TenantA = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid TenantB = Guid.Parse("00000000-0000-0000-0000-0000000000b2");

    private sealed class FakeSession(ChangeSet changes, List<string> log) : ILedgerSession
    {
        public bool InTransaction => false;

        public Task<ChangeSet> BuildChangeSetAsync(CancellationToken cancellationToken) => Task.FromResult(changes);

        public Task<ILedgerTransaction> BeginAsync(CancellationToken cancellationToken)
        {
            log.Add("begin");
            return Task.FromResult<ILedgerTransaction>(new Tx(log));
        }

        public Task LockTenantAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            log.Add($"lock {tenantId:N}");
            return Task.CompletedTask;
        }

        public Task<int> SaveWithoutPassAsync(CancellationToken cancellationToken)
        {
            log.Add("plain-save");
            return Task.FromResult(1);
        }

        public Task<int> FlushAsync(CancellationToken cancellationToken)
        {
            log.Add("flush");
            return Task.FromResult(changes.Entries.Count);
        }

        public Task RefreshTrackedAsync(IReadOnlyList<LedgerContext> contexts, CancellationToken cancellationToken)
        {
            log.Add("refresh");
            return Task.CompletedTask;
        }

        public void AcceptAllChanges() => log.Add("accept");

        public void Reset() => log.Add("reset");

        private sealed class Tx(List<string> log) : ILedgerTransaction
        {
            public bool Owned => true;

            public Task CommitAsync(CancellationToken cancellationToken)
            {
                log.Add("commit");
                return Task.CompletedTask;
            }

            public Task RollbackAsync(CancellationToken cancellationToken)
            {
                log.Add("rollback");
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class FakeRepository : ILedgerRepository
    {
        public int? MaxSerial { get; init; }

        public Task<LedgerTenantSettings> GetSettingsAsync(Guid tenantId, CancellationToken cancellationToken)
            => Task.FromResult(new LedgerTenantSettings(0, CurrencyUnit.Toman, 1000));

        public Task<IReadOnlyList<Guid>> ListTenantIdsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<SerialConflict>> FindSerialOverlapsAsync(Guid tenantId, IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<int?> MaxSerialAsync(Guid tenantId, IReadOnlyCollection<Guid> excludingRugIds, CancellationToken cancellationToken)
            => Task.FromResult(MaxSerial);

        public Task<int> RecomputePurchaseLinesAsync(Guid tenantId, IReadOnlyCollection<Guid> rugIds, int moneyScale, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> RollupPurchaseCostAsync(Guid tenantId, IReadOnlyCollection<Guid> rugIds, IReadOnlyDictionary<Guid, RugCostSources> sources, DateTimeOffset now, long ledgerVersion, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<int> UpsertCostTotalsAsync(Guid tenantId, IReadOnlyCollection<Guid> rugIds, IReadOnlyDictionary<Guid, RugCostSources> sources, int moneyScale, DateTimeOffset now, long ledgerVersion, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<int> SnapshotCostAtSaleAsync(Guid tenantId, IReadOnlyCollection<Guid> rugIds, DateTimeOffset now, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> ListRugIdsAsync(Guid tenantId, int? sample, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<LedgerHealthCounts> GetHealthCountsAsync(Guid tenantId, long ledgerVersion, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<LedgerMismatchRow>> VerifyAsync(Guid tenantId, IReadOnlyCollection<Guid> rugIds, IReadOnlyDictionary<Guid, RugCostSources> sources, int moneyScale, long ledgerVersion, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class RecordingStage(int order, List<string> log, Exception? fail = null) : ILedgerStage
    {
        public int Order => order;

        public Task RunAsync(LedgerContext context, CancellationToken cancellationToken)
        {
            log.Add($"stage {order}");
            if (fail is not null) throw fail;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingContributor(List<string> log) : IChangeImpactContributor
    {
        private readonly RugImpactContributor _inner = new();

        public IReadOnlyCollection<Type> WatchedTypes => _inner.WatchedTypes;

        public Task ContributeAsync(LedgerContext context, CancellationToken cancellationToken)
        {
            log.Add($"contribute {context.TenantId:N}");
            return _inner.ContributeAsync(context, cancellationToken);
        }
    }

    private sealed class NoTotals : IRugCostTotalReader
    {
        public int Clears { get; private set; }
        public RugCostTotalSnapshot? Find(Guid tenantId, Guid rugId) => null;
        public Task PreloadAsync(IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PreloadTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public int MoneyScale(Guid tenantId) => 0;
        public Task<int> MoneyScaleAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public void Invalidate(IEnumerable<Guid> rugIds) { }
        public void Clear() => Clears++;
    }

    private static Rug RugOf(Guid tenant)
    {
        var rug = new Rug { TenantId = tenant, Sku = "RUG-1" };
        rug.SetIntakeSize(1m, 1m);
        return rug;
    }

    private static (LedgerPass Pass, List<string> Log, NoTotals Totals) Build(
        ChangeSet changes, IEnumerable<ILedgerStage>? stages = null, FakeRepository? repository = null)
    {
        var log = new List<string>();
        var totals = new NoTotals();
        var pass = new LedgerPass(
            new FakeSession(changes, log),
            repository ?? new FakeRepository(),
            [new RecordingContributor(log)],
            [],
            stages ?? [new RecordingStage(LedgerStageOrder.RugCostTotal, log), new RecordingStage(LedgerStageOrder.SerialOverlap, log)],
            totals);
        return (pass, log, totals);
    }

    [Fact]
    public async Task UnwatchedChanges_TakeTheFastPath()
    {
        // تغییر فقط در نوعی که هیچ contributor یا قفلی به آن گوش نمی‌دهد (مثلاً برچسب چاپ): بی قفل و بی تراکنش
        var change = new EntityChange(new LabelTemplate { TenantId = TenantA }, ChangeKind.Added, TenantA);
        var (pass, log, _) = Build(new ChangeSet([change]));

        await pass.SaveChangesAsync(CancellationToken.None);

        Assert.Equal(["plain-save"], log);
    }

    [Fact]
    public async Task LockedPath_LocksThenContributesThenFlushesThenRunsStagesInOrder()
    {
        var change = new EntityChange(RugOf(TenantA), ChangeKind.Added, TenantA);
        var (pass, log, _) = Build(new ChangeSet([change]));

        await pass.SaveChangesAsync(CancellationToken.None);

        Assert.Equal(
            ["begin", $"lock {TenantA:N}", $"contribute {TenantA:N}", "flush", "stage 100", "stage 1000", "refresh", "accept", "commit"],
            log);
    }

    [Fact]
    public async Task GuardOnlySave_RunsNoStage()
    {
        // فقط یادداشت: راه قفل‌دار (قفل‌ها می‌سنجند) ولی اثری نیست، پس هیچ مرحله‌ای (D.4 گام ۷)
        var rug = RugOf(TenantA);
        var change = new EntityChange(rug, ChangeKind.Modified, TenantA,
            new Dictionary<string, PropertyChange> { ["Notes"] = new("Notes", "a", "b") });
        var (pass, log, _) = Build(new ChangeSet([change]));

        await pass.SaveChangesAsync(CancellationToken.None);

        Assert.DoesNotContain(log, l => l.StartsWith("stage", StringComparison.Ordinal));
        Assert.Contains("flush", log);
        Assert.Contains("commit", log);
    }

    [Fact]
    public async Task SeveralTenants_AreLockedInAFixedOrder()
    {
        var changes = new ChangeSet(
        [
            new EntityChange(RugOf(TenantB), ChangeKind.Added, TenantB),
            new EntityChange(RugOf(TenantA), ChangeKind.Added, TenantA)
        ]);
        var (pass, log, _) = Build(changes);

        await pass.SaveChangesAsync(CancellationToken.None);

        var locks = log.Where(l => l.StartsWith("lock", StringComparison.Ordinal)).ToList();
        Assert.Equal([$"lock {TenantA:N}", $"lock {TenantB:N}"], locks);
    }

    [Fact]
    public async Task AFailingStage_RollsBackAndClearsTheTracker()
    {
        var change = new EntityChange(RugOf(TenantA), ChangeKind.Added, TenantA);
        var log = new List<string>();
        var (pass, passLog, totals) = Build(new ChangeSet([change]),
            [new RecordingStage(LedgerStageOrder.SerialOverlap, log, new InvalidOperationException("boom"))]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => pass.SaveChangesAsync(CancellationToken.None));

        Assert.Contains("rollback", passLog);
        Assert.Contains("reset", passLog);
        Assert.DoesNotContain("commit", passLog);
        Assert.Equal(1, totals.Clears);
    }

    [Fact]
    public async Task AnOverlapFromTheUniqueIndex_GetsNextFreeAfterTheRollback()
    {
        var change = new EntityChange(RugOf(TenantA), ChangeKind.Added, TenantA);
        var (pass, _, _) = Build(new ChangeSet([change]),
            [new RecordingStage(LedgerStageOrder.SerialOverlap, [], new SerialOverlapException("3778", null, null))],
            new FakeRepository { MaxSerial = 4003 });

        var ex = await Assert.ThrowsAsync<SerialOverlapException>(() => pass.SaveChangesAsync(CancellationToken.None));

        Assert.Equal(4004, ex.NextFree);
        Assert.Equal("rug.serial", ex.Field);
        Assert.Equal(4004, ex.Extensions!["nextFree"]);
    }

    [Theory]
    [InlineData(null, null, 1)]
    [InlineData(null, 4003, 4004)]   // G.4: بعد از «3985 - 4003» ← ۴۰۰۴
    [InlineData(5000, 4003, 5000)]   // کف SerialStart
    [InlineData(100, 4003, 4004)]
    public void NextFree_FollowsTheNextSerialRule(int? serialStart, int? max, int expected)
        => Assert.Equal(expected, LedgerPass.NextFree(serialStart, max));

    [Fact]
    public void StageOrders_AreUniqueAndTheTotalIsLast()
    {
        var orders = typeof(LedgerStageOrder).GetFields()
            .Where(f => f.IsLiteral)
            .Select(f => (int)f.GetRawConstantValue()!)
            .ToList();

        Assert.Equal(orders.Count, orders.Distinct().Count());
        Assert.Equal(LedgerStageOrder.RugCostTotal, orders.Max());
    }
}
