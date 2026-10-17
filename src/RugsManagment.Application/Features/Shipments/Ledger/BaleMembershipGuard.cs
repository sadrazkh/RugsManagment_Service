using RugsManagment.Application.Common;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Shipments.Ledger;

/// <summary>
/// قاعده‌های عضویت در نگله (spec C.8 «BaleMembershipGuard»، C.3، C.10) برای هر مسیر نوشتنی:
/// <list type="bullet">
/// <item>هر فرش حداکثر در یک خط (ایندکس یکتا هم هست؛ این‌جا پیام فارسی «این فرش در محمولهٔ ۷ / نگلهٔ ۳ است»)؛</item>
/// <item>خطِ تازه فقط برای فرشِ فعالِ فروخته‌نشده؛</item>
/// <item>نگلهٔ خط مال همان محمولهٔ خط است؛ خط فقط در محمولهٔ باز نوشته می‌شود؛</item>
/// <item>فرشِ داخل نگله به سطل زباله نمی‌رود و فروخته نمی‌شود («اول این فرش را از نگلهٔ ۱۲ خارج کنید.»)؛</item>
/// <item>نگله فقط خالی و محموله فقط باز و بی‌خط حذف می‌شود.</item>
/// </list>
/// خط‌هایی که در همین ذخیره حذف یا جابه‌جا می‌شوند به حساب می‌آیند (خارج کردن و بعد حذف فرش در یک ذخیره مجاز است).
/// قفل محمولهٔ قطعی پیش از این (<see cref="ShipmentLockGuard"/>، ثبت‌شده جلوتر) پیام خودش را داده است.
/// </summary>
public sealed class BaleMembershipGuard(IShipmentLedgerRepository repository) : ILedgerGuard
{
    public IReadOnlyCollection<Type> WatchedTypes { get; } =
        [typeof(ShipmentLine), typeof(Bale), typeof(Shipment), typeof(Rug), typeof(RugSale)];

    public async Task CheckAsync(LedgerContext context, CancellationToken cancellationToken)
    {
        var changes = context.Changes;
        var tenantId = context.TenantId;
        BaleTermWords? terms = null;
        async Task<BaleTermWords> TermsAsync()
            => terms ??= BaleTerms.For((await repository.SettingsAsync(tenantId, cancellationToken)).BaleTerm);

        // ── خط‌هایی که در این ذخیره از جایشان می‌روند (حذف یا انتقال) و خط‌هایی که می‌آیند ──
        var leaving = new HashSet<Guid>();
        var arriving = new List<ShipmentLine>();
        foreach (var change in changes.Of<ShipmentLine>())
        {
            var line = change.As<ShipmentLine>();
            switch (change.Kind)
            {
                case ChangeKind.Deleted:
                    leaving.Add(line.Id);
                    break;
                case ChangeKind.Added:
                case ChangeKind.Unknown:
                    arriving.Add(line);
                    break;
                case ChangeKind.Modified when change.HasAny(nameof(ShipmentLine.BaleId), nameof(ShipmentLine.ShipmentId), nameof(ShipmentLine.RugId)):
                    arriving.Add(line);
                    if (change.Has(nameof(ShipmentLine.BaleId)) || change.Has(nameof(ShipmentLine.ShipmentId)))
                        leaving.Add(line.Id);
                    break;
            }
        }

        var deletedBales = changes.Of<Bale>().Where(c => c.Kind == ChangeKind.Deleted).Select(c => c.As<Bale>().Id).ToHashSet();
        var deletedShipments = changes.Of<Shipment>().Where(c => c.Kind == ChangeKind.Deleted).ToList();

        // ── فرش‌هایی که به سطل زباله می‌روند، حذف می‌شوند یا فروخته می‌شوند ──
        var trashed = changes.Of<Rug>()
            .Where(c => c.Kind == ChangeKind.Deleted
                        || (c.Kind is ChangeKind.Modified && c.Has(nameof(Rug.DeletedAt)) && c.As<Rug>().DeletedAt is not null
                            && c.Original<DateTimeOffset?>(nameof(Rug.DeletedAt)) is null)
                        || (c.Kind == ChangeKind.Unknown && c.As<Rug>().DeletedAt is not null))
            .Select(c => c.As<Rug>().Id)
            .ToHashSet();
        var sold = changes.Of<RugSale>().Where(c => c.Kind == ChangeKind.Added).Select(c => c.As<RugSale>().RugId).ToHashSet();

        if (arriving.Count == 0 && deletedBales.Count == 0 && deletedShipments.Count == 0 && trashed.Count == 0 && sold.Count == 0)
            return;

        // ── یک پرس‌وجو: خط‌های موجودِ همهٔ فرش‌ها، نگله‌ها و محموله‌هایی که این ذخیره به آن‌ها می‌رسد ──
        var rugIds = arriving.Select(l => l.RugId).Concat(trashed).Concat(sold).ToHashSet();
        var existing = await repository.LinesAsync(
            tenantId,
            rugIds.Count > 0 ? rugIds : null,
            deletedBales.Count > 0 ? deletedBales : null,
            deletedShipments.Count > 0 ? deletedShipments.Select(c => c.As<Shipment>().Id).ToList() : null,
            cancellationToken);
        var staying = existing.Where(l => !leaving.Contains(l.LineId)).ToList();

        // ── خط‌های تازه یا جابه‌جاشده ──
        if (arriving.Count > 0)
        {
            var duplicate = arriving.GroupBy(l => l.RugId).FirstOrDefault(g => g.Count() > 1);
            if (duplicate is not null)
                throw new DomainConflictException(Messages.Conflict.Text, "rugId");

            foreach (var line in arriving)
            {
                var other = staying.FirstOrDefault(l => l.RugId == line.RugId && l.LineId != line.Id);
                if (other is not null)
                {
                    throw new DomainConflictException(
                        Messages.LineRugInOtherBale.Format(await TermsAsync(), ("shipment", other.ShipmentNumber), ("n", other.BaleNumber)),
                        "rugId");
                }
            }

            await CheckRugsAsync(tenantId, arriving, changes, TermsAsync, cancellationToken);
            await CheckBalesAsync(tenantId, arriving, changes, deletedBales, TermsAsync, cancellationToken);
            await CheckOpenAsync(tenantId, arriving, changes, cancellationToken);
        }

        // ── سطل زباله و فروش فرشِ داخل نگله ──
        foreach (var rugId in trashed)
        {
            if (staying.FirstOrDefault(l => l.RugId == rugId) is { } line)
                throw new DomainConflictException(Messages.TrashBaled.Format(await TermsAsync(), ("n", line.BaleNumber)), "rug.deletedAt");
            if (arriving.Any(l => l.RugId == rugId))
                throw new DomainConflictException(Messages.LineRugTrashed.Text, "rugId");
        }

        foreach (var rugId in sold)
        {
            var line = staying.FirstOrDefault(l => l.RugId == rugId);
            var bale = line?.BaleNumber;
            if (line is null && arriving.FirstOrDefault(l => l.RugId == rugId) is { } added)
                bale = changes.Of<Bale>().Select(c => c.As<Bale>()).FirstOrDefault(b => b.Id == added.BaleId)?.Number ?? 0;
            if (bale is not null)
                throw new DomainConflictException(Messages.SaleBaled.Format(await TermsAsync(), ("n", bale.Value)), "sale");
        }

        // ── حذف نگله: فقط خالی ──
        foreach (var baleId in deletedBales)
        {
            if (staying.FirstOrDefault(l => l.BaleId == baleId) is { } line)
                throw new DomainConflictException(Messages.BaleNotEmpty.Format(await TermsAsync(), ("n", line.BaleNumber)));
        }

        // ── حذف محموله: فقط باز و بی‌خط ──
        foreach (var change in deletedShipments)
        {
            if (change.Original<ShipmentStatus>(nameof(Shipment.Status)) == ShipmentStatus.Finalized)
                throw new DomainConflictException(Messages.ShipmentDeleteFinalized.Text);
            var shipmentId = change.As<Shipment>().Id;
            if (staying.Any(l => l.ShipmentId == shipmentId) || arriving.Any(l => l.ShipmentId == shipmentId))
                throw new DomainConflictException(Messages.ShipmentNotEmpty.Text);
        }
    }

    /// <summary>فرش خطِ تازه باید فعال و فروخته‌نشده باشد (فرشی که در همین ذخیره ساخته می‌شود، مثل برش دسته، آزاد است).</summary>
    private async Task CheckRugsAsync(
        Guid tenantId, IReadOnlyList<ShipmentLine> arriving, ChangeSet changes, Func<Task<BaleTermWords>> terms,
        CancellationToken cancellationToken)
    {
        var addedRugs = changes.Of<Rug>().Where(c => c.Kind == ChangeKind.Added).Select(c => c.As<Rug>().Id).ToHashSet();
        var check = arriving.Select(l => l.RugId).Where(id => !addedRugs.Contains(id)).Distinct().ToList();
        if (check.Count == 0) return;

        var stored = (await repository.RugValuesAsync(tenantId, check, cancellationToken)).ToDictionary(v => v.Id);
        foreach (var rugId in check)
        {
            if (!stored.TryGetValue(rugId, out var values))
                throw new KeyNotFoundException("فرش یافت نشد.");
            if (values.Values.GetValueOrDefault(nameof(Rug.DeletedAt)) is not null)
                throw new DomainConflictException(Messages.LineRugTrashed.Text, "rugId");
            if (values.HasSale)
                throw new DomainConflictException(Messages.LineRugSold.Format(await terms()), "rugId");
        }
    }

    /// <summary>
    /// خط فقط در محمولهٔ باز نوشته می‌شود (وضعیت «پیش از این ذخیره»؛ محمولهٔ تازه باز است). <see cref="ShipmentLockGuard"/>
    /// همین را با پیام قفل پیش‌تر سنجیده؛ این‌جا پشتوانه است تا قاعدهٔ عضویت بی آن هم برقرار بماند.
    /// </summary>
    private async Task CheckOpenAsync(
        Guid tenantId, IReadOnlyList<ShipmentLine> arriving, ChangeSet changes, CancellationToken cancellationToken)
    {
        var tracked = changes.Of<Shipment>().ToDictionary(c => c.As<Shipment>().Id);
        var lookup = arriving.Select(l => l.ShipmentId).Where(id => !tracked.ContainsKey(id)).Distinct().ToList();
        var states = lookup.Count == 0
            ? []
            : await repository.ShipmentStatesAsync(tenantId, lookup, cancellationToken);

        foreach (var line in arriving)
        {
            var finalized = tracked.TryGetValue(line.ShipmentId, out var change)
                ? change.Kind != ChangeKind.Added && change.Original<ShipmentStatus>(nameof(Shipment.Status)) == ShipmentStatus.Finalized
                : states.FirstOrDefault(s => s.Id == line.ShipmentId)?.Status == ShipmentStatus.Finalized;
            if (finalized)
            {
                var number = change?.As<Shipment>().Number ?? states.First(s => s.Id == line.ShipmentId).Number;
                throw new ShipmentLockedException(Messages.ShipmentLockedHeader.Format(("number", number)), line.ShipmentId, number, "line");
            }
        }
    }

    /// <summary>نگلهٔ خط مال همان محموله باشد و حذف نشود.</summary>
    private async Task CheckBalesAsync(
        Guid tenantId, IReadOnlyList<ShipmentLine> arriving, ChangeSet changes, IReadOnlySet<Guid> deletedBales,
        Func<Task<BaleTermWords>> terms, CancellationToken cancellationToken)
    {
        var tracked = changes.Of<Bale>().Where(c => c.Kind != ChangeKind.Deleted).Select(c => c.As<Bale>()).ToDictionary(b => b.Id);
        var lookup = arriving.Select(l => l.BaleId).Where(id => !tracked.ContainsKey(id)).Distinct().ToList();
        var stored = lookup.Count == 0
            ? new Dictionary<Guid, BaleState>()
            : (await repository.BaleStatesAsync(tenantId, lookup, cancellationToken)).ToDictionary(b => b.Id);

        foreach (var line in arriving)
        {
            if (deletedBales.Contains(line.BaleId))
                throw new DomainConflictException(Messages.LineBaleMismatch.Format(await terms()), "baleId");

            Guid? shipmentId = tracked.TryGetValue(line.BaleId, out var bale)
                ? bale.ShipmentId
                : stored.TryGetValue(line.BaleId, out var state) ? state.ShipmentId : null;
            if (shipmentId is null)
                throw new KeyNotFoundException("نگله یافت نشد.");
            if (shipmentId != line.ShipmentId)
                throw new DomainConflictException(Messages.LineBaleMismatch.Format(await terms()), "baleId");
        }
    }
}
