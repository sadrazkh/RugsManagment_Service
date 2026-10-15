using RugsManagment.Application.Common;

namespace RugsManagment.Application.Ledger;

/// <summary>
/// ترتیب پاس سازگاری (spec D.4). چرا دو مرحله‌ای: SQLی که پیش از flush اجرا شود ردیف‌های در انتظارِ change tracker را
/// نمی‌بیند؛ پس پاس پیش از flush فقط می‌خواند و می‌سنجد (contributorها و قفل‌ها، بعد از گرفتن قفل کارگاه) و بعد از flush،
/// در همان تراکنش، ردیف‌های مشتق‌شده را با SQL مجموعه‌ای از نو می‌سازد. نتیجه: یک درخواست، عددهای تازه — DTOیی که
/// بعد از ذخیره ساخته می‌شود همان بهای تازه را دارد و هیچ مسیر نوشتنی (قدیمی یا تازه) نمی‌تواند از حساب‌ها جا بماند.
///
/// <list type="number">
/// <item>تفاوت مقدار (<see cref="ILedgerSession.BuildChangeSetAsync"/>)؛</item>
/// <item>fast path: اگر هیچ تغییری به نوعی که contributor یا guardی به آن گوش می‌دهد نرسیده، همان SaveChanges ساده؛</item>
/// <item>تراکنش (یا تراکنش بیرونی) و قفل advisory هر کارگاه، به ترتیب ثابت؛</item>
/// <item>contributorها (همهٔ خواندن‌ها همین‌جا، بعد از قفل)؛</item>
/// <item>قفل‌ها (guardها)؛</item>
/// <item>flush (<c>acceptAllChangesOnSuccess: false</c>)؛</item>
/// <item>مرحله‌ها به ترتیب <see cref="LedgerStageOrder"/>، فقط برای contextی که اثر دارد؛</item>
/// <item>تازه‌کردن موجودیت‌های ردیابی‌شدهٔ مشتق‌شده؛</item>
/// <item><c>AcceptAllChanges</c> و commit.</item>
/// </list>
/// هر خطا: rollback (یا برگشت به savepoint در تراکنش بیرونی) و پاک کردن ردیابی.
/// </summary>
public sealed class LedgerPass(
    ILedgerSession session,
    ILedgerRepository repository,
    IEnumerable<IChangeImpactContributor> contributors,
    IEnumerable<ILedgerGuard> guards,
    IEnumerable<ILedgerStage> stages,
    IRugCostTotalReader costTotals,
    TimeProvider? time = null) : ILedgerPass
{
    private readonly IReadOnlyList<IChangeImpactContributor> _contributors = contributors.ToList();
    private readonly IReadOnlyList<ILedgerGuard> _guards = guards.ToList();
    private readonly IReadOnlyList<ILedgerStage> _stages = stages.OrderBy(s => s.Order).ToList();
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    private HashSet<Type>? _watched;
    private bool _running;

    /// <summary>
    /// نوع‌هایی که دست‌کم یک contributor یا guard به آن گوش می‌دهد. ذخیره‌ای که به هیچ‌کدام نرسد، fast path است.
    /// </summary>
    public IReadOnlySet<Type> WatchedTypes
        => _watched ??= _contributors.SelectMany(c => c.WatchedTypes)
            .Concat(_guards.SelectMany(g => g.WatchedTypes))
            .ToHashSet();

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        // flush خودِ پاس هرگز به این‌جا برنمی‌گردد (مستقیم DbContext را صدا می‌زند)؛ این پرچم جلوی کدی را می‌گیرد که
        // از درون یک contributor یا مرحله IUnitOfWork.SaveChangesAsync را صدا بزند
        if (_running)
            throw new InvalidOperationException("SaveChanges از درون پاس سازگاری صدا زده شد؛ این کار پاس تو در تو می‌سازد.");

        var changes = await session.BuildChangeSetAsync(cancellationToken);
        var relevant = changes.Entries.Where(IsWatched).ToList();
        if (relevant.Count == 0)
            return await session.SaveWithoutPassAsync(cancellationToken);

        _running = true;
        var transaction = await session.BeginAsync(cancellationToken);
        LedgerContext? failedContext = null;
        try
        {
            var contexts = await PrepareAsync(changes, relevant, cancellationToken);

            var written = await session.FlushAsync(cancellationToken);

            foreach (var context in contexts)
            {
                failedContext = context;
                if (!context.Impact.IsEmpty)
                    await RunStagesAsync(context, cancellationToken);
            }

            failedContext = null;
            await session.RefreshTrackedAsync(contexts, cancellationToken);
            session.AcceptAllChanges();
            await transaction.CommitAsync(cancellationToken);
            return written;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            session.Reset();
            costTotals.Clear();

            // مبلغی که مرحله‌ها نوشتند در ستون جا نشد (سرریز عددی): خطای کاربر است، نه ۵۰۰ (پشتیبان سنجش نویسنده‌ها)
            if (session.IsNumericOverflow(ex))
                throw new AppValidationException(Messages.AmountOverflow.Text);

            // «شمارهٔ آزاد بعدی» وقتی همپوشانی از ایندکس یکتا (در flush) آمد هنوز حساب نشده؛ بعد از برگرداندن، اتصال
            // دوباره قابل استفاده است. در تراکنش بیرونیِ شکسته این پرس‌وجو ممکن نیست و بی آن برمی‌گردد.
            if (ex is SerialOverlapException { NextFree: null } overlap && transaction.Owned
                && (failedContext?.TenantId ?? SingleTenant(relevant)) is Guid tenantId)
            {
                var nextFree = await TryNextFreeAsync(tenantId, CancellationToken.None);
                if (nextFree is not null)
                    throw overlap.WithNextFree(nextFree);
            }

            throw;
        }
        finally
        {
            _running = false;
            await transaction.DisposeAsync();
        }
    }

    public async Task RunStagesAsync(LedgerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        foreach (var stage in _stages)
            await stage.RunAsync(context, cancellationToken);
    }

    /// <summary>
    /// قفل همهٔ کارگاه‌ها (ترتیب ثابت، بی بن‌بست میان دو ذخیرهٔ چندکارگاهی)، تنظیم‌ها، contributorها و قفل‌ها.
    /// </summary>
    private async Task<IReadOnlyList<LedgerContext>> PrepareAsync(
        ChangeSet changes, IReadOnlyList<EntityChange> relevant, CancellationToken cancellationToken)
    {
        var tenantIds = relevant
            .Where(e => e.TenantId is not null)
            .Select(e => e.TenantId!.Value)
            .Distinct()
            .OrderBy(id => id.ToString("D"), StringComparer.Ordinal)
            .ToList();

        foreach (var tenantId in tenantIds)
            await session.LockTenantAsync(tenantId, cancellationToken);

        var now = _time.GetUtcNow();
        var contexts = new List<LedgerContext>(tenantIds.Count);
        foreach (var tenantId in tenantIds)
        {
            var context = new LedgerContext(tenantId, changes.ForTenant(tenantId), now);
            context.Apply(await repository.GetSettingsAsync(tenantId, cancellationToken));
            contexts.Add(context);
        }

        foreach (var context in contexts)
        {
            foreach (var contributor in _contributors)
            {
                if (Touches(context.Changes, contributor))
                    await contributor.ContributeAsync(context, cancellationToken);
            }
        }

        foreach (var context in contexts)
        {
            foreach (var guard in _guards)
            {
                if (Touches(context.Changes, guard))
                    await guard.CheckAsync(context, cancellationToken);
            }
        }

        return contexts;
    }

    private bool IsWatched(EntityChange change) => WatchedTypes.Any(t => t.IsAssignableFrom(change.EntityType));

    private static bool Touches(ChangeSet changes, ILedgerParticipant participant)
        => changes.Entries.Any(e => participant.WatchedTypes.Any(t => t.IsAssignableFrom(e.EntityType)));

    private static Guid? SingleTenant(IReadOnlyList<EntityChange> relevant)
    {
        var tenants = relevant.Where(e => e.TenantId is not null).Select(e => e.TenantId!.Value).Distinct().ToList();
        return tenants.Count == 1 ? tenants[0] : null;
    }

    private async Task<int?> TryNextFreeAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        try
        {
            var settings = await repository.GetSettingsAsync(tenantId, cancellationToken);
            var max = await repository.MaxSerialAsync(tenantId, [], cancellationToken);
            return NextFree(settings.SerialStart, max);
        }
        catch (Exception)
        {
            // پیام اصلی مهم‌تر است؛ بی nextFree هم ۴۰۹ درست است
            return null;
        }
    }

    /// <summary>قاعدهٔ «شمارهٔ بعدی» (G.4): max(SerialStart ?? 1، بزرگ‌ترین شماره + ۱).</summary>
    public static int NextFree(int? serialStart, int? maxSerial)
    {
        var floor = serialStart ?? 1;
        if (maxSerial is not { } max) return floor;
        return max >= int.MaxValue ? floor : Math.Max(floor, max + 1);
    }
}
