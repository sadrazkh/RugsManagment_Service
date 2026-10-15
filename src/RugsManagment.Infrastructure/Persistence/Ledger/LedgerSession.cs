using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Npgsql;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Infrastructure.Persistence.Ledger;

/// <summary>
/// کارهای EFیِ پاس سازگاری (پیاده‌سازی <see cref="ILedgerSession"/> و <see cref="ILedgerLock"/>).
///
/// <list type="bullet">
/// <item><b>تراکنش:</b> اگر تراکنشی باز نیست، پاس تراکنش خودش را می‌سازد؛ در تراکنش بیرونی (<c>ExecuteInTransactionAsync</c>)
/// یک savepoint می‌سازد تا شکست یک ذخیره فقط کار همان ذخیره را برگرداند و تراکنش بیرونی قابل ادامه بماند.</item>
/// <item><b>قفل:</b> <c>pg_advisory_xact_lock(hashtext('rugs:ledger:' || tenantId))</c>، یک بار برای هر کارگاه در هر تراکنش؛
/// با پایان تراکنش آزاد می‌شود. برگشت به savepoint قفل گرفته‌شده در آن را هم آزاد می‌کند، پس حافظهٔ «قفل گرفته شد» هم پاک
/// می‌شود و ذخیرهٔ بعدی دوباره می‌گیرد.</item>
/// <item><b>خطاها:</b> هم‌زمانی → <see cref="ConcurrencyConflictException"/>؛ نقض ایندکس یکتای شماره → <see cref="SerialOverlapException"/>؛
/// هر کلید تکراری دیگر → <see cref="DuplicateKeyException"/>.</item>
/// </list>
/// </summary>
public sealed class LedgerSession(
    AppDbContext db,
    ChangeSetBuilder builder,
    LedgerTracking tracking,
    IRugCostTotalReader costTotals,
    IOptions<LedgerOptions> options) : ILedgerSession, ILedgerLock
{
    /// <summary>کد خطای PostgreSQL برای نقض قید یکتایی.</summary>
    private const string UniqueViolation = "23505";

    /// <summary>نام ایندکس یکتای شمارهٔ فرش (EntityConfigurations، M2).</summary>
    private const string SerialIndex = "IX_Rugs_TenantId_SerialNo";

    private readonly HashSet<Guid> _lockedTenants = [];
    private DbTransaction? _lockedIn;

    public Task<ChangeSet> BuildChangeSetAsync(CancellationToken cancellationToken) => builder.BuildAsync(cancellationToken);

    public bool InTransaction => db.Database.CurrentTransaction is not null;

    public async Task<ILedgerTransaction> BeginAsync(CancellationToken cancellationToken)
    {
        var current = db.Database.CurrentTransaction;
        if (current is null)
            return new OwnedTransaction(this, await db.Database.BeginTransactionAsync(cancellationToken));

        var savepoint = "ledger_" + Guid.NewGuid().ToString("N")[..12];
        await current.CreateSavepointAsync(savepoint, cancellationToken);
        return new NestedTransaction(this, current, savepoint);
    }

    public async Task LockTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var transaction = db.Database.CurrentTransaction?.GetDbTransaction()
                          ?? throw new InvalidOperationException("قفل حساب‌های کارگاه فقط درون تراکنش گرفته می‌شود.");
        if (!ReferenceEquals(_lockedIn, transaction))
        {
            _lockedIn = transaction;
            _lockedTenants.Clear();
        }

        if (!_lockedTenants.Add(tenantId))
            return;

        var key = "rugs:ledger:" + tenantId.ToString("D");
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))", cancellationToken);
    }

    /// <summary><see cref="ILedgerLock"/>: همان قفل، برای سرویسی که پیش از flush زیر قفل کار می‌کند (E2).</summary>
    public Task AcquireAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => LockTenantAsync(tenantId, cancellationToken);

    public async Task<int> SaveWithoutPassAsync(CancellationToken cancellationToken)
    {
        var written = await SaveAsync(acceptAll: true, cancellationToken);
        tracking.Accept();
        return written;
    }

    public Task<int> FlushAsync(CancellationToken cancellationToken) => SaveAsync(acceptAll: false, cancellationToken);

    public async Task RefreshTrackedAsync(IReadOnlyList<LedgerContext> contexts, CancellationToken cancellationToken)
    {
        var lineRugs = contexts.SelectMany(c => c.Impact.PurchaseLineRugs).ToHashSet();
        var saleRugs = contexts.Where(c => !c.IsRebuild).SelectMany(c => c.Impact.SaleAddedRugs).ToHashSet();
        var totalRugs = contexts
            .SelectMany(c => c.Impact.CostTotalRugs.Concat(c.PurchaseCostChangedRugs).Concat(c.Impact.SaleAddedRugs))
            .ToHashSet();

        // ۱) ردیف‌های مشتق‌شدهٔ ردیابی‌شده جدا می‌شوند (هیچ بارگذاری‌ای نباید ردیابی‌شان کند؛ این احتیاط است)
        var derived = options.Value.DerivedTypes;
        foreach (var entry in db.ChangeTracker.Entries().ToList())
        {
            if (derived.Any(t => t.IsAssignableFrom(entry.Metadata.ClrType)))
                entry.State = EntityState.Detached;
        }

        // ۲) مبلغ سطرهای خرید ردیابی‌شده (که مرحلهٔ ۲۰۰ با SQL نوشت) از دیتابیس
        if (lineRugs.Count > 0)
        {
            var tracked = db.ChangeTracker.Entries<RugPurchaseLine>()
                .Where(e => e.State != EntityState.Deleted && lineRugs.Contains(e.Entity.RugId))
                .ToList();
            if (tracked.Count > 0)
            {
                var ids = tracked.Select(e => e.Entity.Id).ToList();
                var amounts = await db.Set<RugPurchaseLine>()
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(l => ids.Contains(l.Id))
                    .Select(l => new { l.Id, l.Amount })
                    .ToDictionaryAsync(l => l.Id, l => l.Amount, cancellationToken);
                foreach (var entry in tracked)
                {
                    if (amounts.TryGetValue(entry.Entity.Id, out var amount))
                        SetDerived(entry.Property(l => l.Amount), amount);
                }
            }
        }

        // ۳) عکس بهای فروش‌های تازه (مرحلهٔ ۱۰۰۰)
        if (saleRugs.Count > 0)
        {
            var tracked = db.ChangeTracker.Entries<RugSale>()
                .Where(e => e.State != EntityState.Deleted && saleRugs.Contains(e.Entity.RugId))
                .ToList();
            if (tracked.Count > 0)
            {
                var ids = tracked.Select(e => e.Entity.Id).ToList();
                var snapshots = await db.RugSales
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(s => ids.Contains(s.Id))
                    .Select(s => new { s.Id, s.CostAtSale, s.CostAtSaleAt })
                    .ToDictionaryAsync(s => s.Id, cancellationToken);
                foreach (var entry in tracked)
                {
                    if (!snapshots.TryGetValue(entry.Entity.Id, out var snapshot)) continue;
                    SetDerived(entry.Property(s => s.CostAtSale), snapshot.CostAtSale);
                    SetDerived(entry.Property(s => s.CostAtSaleAt), snapshot.CostAtSaleAt);
                }
            }
        }

        // ۴) خوانندهٔ جمع: عدد تازهٔ همین فرش‌ها (در همین تراکنش؛ همان که commit می‌شود)
        if (totalRugs.Count > 0)
        {
            costTotals.Invalidate(totalRugs);
            await costTotals.PreloadAsync(totalRugs, cancellationToken);
        }
    }

    public void AcceptAllChanges()
    {
        db.ChangeTracker.AcceptAllChanges();
        tracking.Accept();
    }

    public void Reset()
    {
        db.ChangeTracker.Clear();
        tracking.Accept();
        costTotals.Clear();
    }

    /// <summary>
    /// مقدار ستونی که مرحله‌ها با SQL نوشتند روی موجودیت ردیابی‌شده: هم «فعلی» و هم «اصلی»، تا نه DTO کهنه بخواند و نه
    /// تفاوت‌سنجیِ ذخیرهٔ بعدی آن را تغییر کاربر بپندارد.
    /// </summary>
    private static void SetDerived<TEntity, T>(
        Microsoft.EntityFrameworkCore.ChangeTracking.PropertyEntry<TEntity, T> property, T value)
        where TEntity : class
    {
        property.CurrentValue = value;
        property.OriginalValue = value;
    }

    private async Task<int> SaveAsync(bool acceptAll, CancellationToken cancellationToken)
    {
        try
        {
            return await db.SaveChangesAsync(acceptAll, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolation } pg)
        {
            if (string.Equals(pg.ConstraintName, SerialIndex, StringComparison.Ordinal))
                throw new SerialOverlapException(ChangedSerial() ?? string.Empty, conflictingSerial: null, nextFree: null, ex);
            throw new DuplicateKeyException();
        }
    }

    /// <summary>
    /// شمارهٔ فرشی از همین ذخیره که ایندکس یکتا را شکسته (برای پیام): اول فرش تازه یا فرشی که شماره‌اش عوض شده، بعد فرشی
    /// که از سطل زباله برگشته (شماره‌اش همان است ولی حالا دوباره در ایندکس است؛ M.2 #4)، و آخر هر فرش دیگرِ این ذخیره که
    /// شماره دارد. بی این ترتیب، بازگردانیِ فرشی که شماره‌اش را فرش تازه‌ای گرفته پیام «شمارهٔ «» مال فرش دیگری است» می‌داد.
    /// </summary>
    private string? ChangedSerial()
    {
        var candidates = db.ChangeTracker.Entries<Rug>()
            .Where(e => e.Entity.SerialNo is not null && e.State is EntityState.Added or EntityState.Modified)
            .ToList();

        return candidates.FirstOrDefault(e => e.State == EntityState.Added
                                              || !Equals(e.Property(r => r.SerialNo).OriginalValue, e.Entity.SerialNo))?.Entity.SerialNo
               ?? candidates.FirstOrDefault(e => e.Entity.DeletedAt is null
                                                 && e.Property(r => r.DeletedAt).OriginalValue is not null)?.Entity.SerialNo
               ?? candidates.FirstOrDefault()?.Entity.SerialNo;
    }

    /// <inheritdoc />
    public bool IsNumericOverflow(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.NumericValueOutOfRange })
                return true;
        }

        return false;
    }

    private void ForgetLocks()
    {
        _lockedTenants.Clear();
        _lockedIn = null;
    }

    private sealed class OwnedTransaction(LedgerSession session, IDbContextTransaction transaction) : ILedgerTransaction
    {
        private bool _completed;

        public bool Owned => true;

        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            await transaction.CommitAsync(cancellationToken);
            _completed = true;
            session.ForgetLocks();
        }

        public async Task RollbackAsync(CancellationToken cancellationToken)
        {
            if (_completed) return;
            _completed = true;
            try
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            catch (InvalidOperationException)
            {
                // تراکنش پیش از این بسته شده (مثلاً اتصال افتاد)؛ چیزی برای برگرداندن نمانده
            }

            session.ForgetLocks();
        }

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }

    private sealed class NestedTransaction(LedgerSession session, IDbContextTransaction outer, string savepoint) : ILedgerTransaction
    {
        private bool _completed;

        public bool Owned => false;

        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            await outer.ReleaseSavepointAsync(savepoint, cancellationToken);
            _completed = true;
        }

        public async Task RollbackAsync(CancellationToken cancellationToken)
        {
            if (_completed) return;
            _completed = true;
            try
            {
                await outer.RollbackToSavepointAsync(savepoint, cancellationToken);
            }
            catch (Exception ex) when (ex is InvalidOperationException or DbException)
            {
                // تراکنش بیرونی دیگر قابل استفاده نیست؛ صاحبش آن را برمی‌گرداند
            }

            // قفلی که درون savepoint گرفته شده بود با برگشت آزاد شد
            session.ForgetLocks();
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
