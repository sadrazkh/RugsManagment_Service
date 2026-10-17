using RugsManagment.Application.Common;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Shipments.Ledger;

/// <summary>
/// قفل محمولهٔ قطعی (spec C.8) در پاس سازگاری، پس هر مسیر نوشتنی پوشیده است: PUT فرش، ویرایش گروهی (حتی «بایگانی»)،
/// جدول ورود، گردش کار (پیش بردن، برگشت، فعال کردن، تغییر مسیر)، فروش، سطل زباله، برش دسته، خط‌ها، نگله‌ها و سربرگ.
///
/// <list type="bullet">
/// <item><b>فرشِ خط‌دار در محمولهٔ قطعی:</b> اندازهٔ اول و نهایی، تعداد، شماره، مشخصات (عنوان، محل بافت، نقشه، مواد، رجشمار)،
/// فیلدهای سفارشی «خروجی‌پذیر» و فیلد کلید ارزش گمرکی، وضعیت، سطل زباله و حذف، و مسیر گردش کار → ۴۰۹. آزاد: یادداشت، عکس،
/// قیمت هدف، ورودی، سطرهای خرید، مبلغ مراحل.</item>
/// <item><b>مراحل گردش کار</b> چنین فرشی: افزودن، حذف، و تغییر وضعیت/ترتیب/نوع/زمان‌ها → ۴۰۹ (پیش بردن، برگشت و فعال کردن
/// وضعیت را از نو حل می‌کنند و «صادر شده» را بازنویسی می‌کردند). تغییر فقط مبلغ (هزینهٔ دیرهنگام) آزاد است.</item>
/// <item><b>فروش</b> چنین فرشی → ۴۰۹.</item>
/// <item><b>خط‌ها و نگله‌ها</b> (افزودن، حذف، انتقال، ترتیب، نرخ دستی، شماره، وزن) و <b>سربرگ</b> (عنوان، تاریخ، مقصد، خریدار،
/// شمارهٔ اظهار، گمرک، نرخ دلار، عکس) محمولهٔ قطعی → ۴۰۹. آزاد: یادداشت، متراژ هدف، درآمد، یادداشت نگله.</item>
/// <item><b>«قطعی» و «باز کردن دوباره»</b> خودشان رد نمی‌شوند: قفل با وضعیت «پیش از این ذخیره» سنجیده می‌شود، و وقتی وضعیت خودِ
/// محموله در همین ذخیره عوض می‌شود، تغییر وضعیت فرش‌هایش (و ستون‌های عکس) پذیرفته است.</item>
/// </list>
/// فقط «تغییر واقعی» دیده می‌شود (تفاوت مقدار D.4)، پس <c>rugs.Update(rug)</c> که فقط یادداشت فرش صادرشده را عوض کرده
/// رد نمی‌شود؛ فرشی با مقدار اصلیِ نامعلوم با مقدار تازهٔ دیتابیس سنجیده می‌شود. پیام برای بازرگان «باز کردن دوباره» را نام
/// می‌برد و برای انباردار بازرگان را.
/// </summary>
public sealed class ShipmentLockGuard(
    IShipmentLedgerRepository repository,
    IEnumerable<IMoneyVisibility> visibility) : ILedgerGuard
{
    /// <summary>ستون‌های قفل‌دار فرش (جز <see cref="Rug.MetadataJson"/> که کلید به کلید سنجیده می‌شود).</summary>
    public static readonly string[] RugLockedFields =
    [
        nameof(Rug.IntakeLength), nameof(Rug.IntakeWidth), nameof(Rug.FinalLength), nameof(Rug.FinalWidth),
        nameof(Rug.FinalMeasuredAt), nameof(Rug.FinalMeasuredBy),
        nameof(Rug.LengthMeters), nameof(Rug.WidthMeters), nameof(Rug.Quantity),
        nameof(Rug.SerialNo), nameof(Rug.SerialFrom), nameof(Rug.SerialTo), nameof(Rug.SerialSort),
        nameof(Rug.Title), nameof(Rug.Origin), nameof(Rug.Pattern), nameof(Rug.Material), nameof(Rug.KnotDensity),
        nameof(Rug.Status), nameof(Rug.DeletedAt), nameof(Rug.WorkflowTemplateId), nameof(Rug.CurrentStepIndex)
    ];

    /// <summary>ستون‌های قفل‌دار مرحلهٔ گردش کار.</summary>
    public static readonly string[] StepLockedFields =
    [
        nameof(RugWorkflowStep.Status), nameof(RugWorkflowStep.OrderIndex), nameof(RugWorkflowStep.ProcessStepTypeId),
        nameof(RugWorkflowStep.IsOptional), nameof(RugWorkflowStep.StartedAt), nameof(RugWorkflowStep.CompletedAt),
        nameof(RugWorkflowStep.RugId)
    ];

    /// <summary>ستون‌های قفل‌دار سربرگ (عکس هم: فقط «قطعی» و «باز کردن دوباره» آن را می‌نویسند).</summary>
    public static readonly string[] ShipmentLockedFields =
    [
        nameof(Shipment.Title), nameof(Shipment.DocumentDate), nameof(Shipment.Destination), nameof(Shipment.BuyerName),
        nameof(Shipment.DeclarationNo), nameof(Shipment.CustomsOffice), nameof(Shipment.UsdRate),
        nameof(Shipment.Status), nameof(Shipment.FinalizeCount), nameof(Shipment.FinalizedAt), nameof(Shipment.FinalizedBy),
        nameof(Shipment.SnapshotJson), nameof(Shipment.SnapshotHash), nameof(Shipment.LastSnapshotJson), nameof(Shipment.Number)
    ];

    /// <summary>ستون‌هایی که «باز کردن دوباره» در همان ذخیره می‌نویسد.</summary>
    private static readonly HashSet<string> ReopenFields = new(StringComparer.Ordinal)
    {
        nameof(Shipment.Status), nameof(Shipment.SnapshotJson), nameof(Shipment.SnapshotHash), nameof(Shipment.LastSnapshotJson)
    };

    /// <summary>ستون‌های قفل‌دار نگله.</summary>
    public static readonly string[] BaleLockedFields = [nameof(Bale.Number), nameof(Bale.WeightKg), nameof(Bale.ShipmentId)];

    /// <summary>ستون‌های قفل‌دار خط.</summary>
    public static readonly string[] LineLockedFields =
    [
        nameof(ShipmentLine.ShipmentId), nameof(ShipmentLine.BaleId), nameof(ShipmentLine.RugId), nameof(ShipmentLine.Seq),
        nameof(ShipmentLine.UsdPerM2Override)
    ];

    public IReadOnlyCollection<Type> WatchedTypes { get; } =
        [typeof(Rug), typeof(RugWorkflowStep), typeof(RugSale), typeof(Shipment), typeof(Bale), typeof(ShipmentLine)];

    public async Task CheckAsync(LedgerContext context, CancellationToken cancellationToken)
    {
        var changes = context.Changes;

        // ── وضعیت «پیش از این ذخیره» محموله‌هایی که در خودِ ذخیره‌اند ──
        var knownStatus = new Dictionary<Guid, ShipmentStatus>();
        var statusChanging = new HashSet<Guid>();
        var unknownShipments = new List<Guid>();
        foreach (var change in changes.Of<Shipment>())
        {
            var shipment = change.As<Shipment>();
            switch (change.Kind)
            {
                case ChangeKind.Added:
                    knownStatus[shipment.Id] = ShipmentStatus.Open;
                    break;
                case ChangeKind.Modified:
                case ChangeKind.Deleted:
                    knownStatus[shipment.Id] = change.Original<ShipmentStatus>(nameof(Shipment.Status));
                    if (change.Kind == ChangeKind.Modified && change.Has(nameof(Shipment.Status)))
                        statusChanging.Add(shipment.Id);
                    break;
                case ChangeKind.Unknown:
                    unknownShipments.Add(shipment.Id);
                    break;
            }
        }

        // ── محموله‌هایی که نگله یا خطشان در این ذخیره عوض می‌شود ──
        var shipmentIds = new HashSet<Guid>(unknownShipments);
        foreach (var change in changes.Of<Bale>())
        {
            shipmentIds.Add(change.As<Bale>().ShipmentId);
            if (change.Kind is ChangeKind.Modified or ChangeKind.Deleted)
                shipmentIds.Add(change.Original<Guid>(nameof(Bale.ShipmentId)));
        }

        foreach (var change in changes.Of<ShipmentLine>())
        {
            shipmentIds.Add(change.As<ShipmentLine>().ShipmentId);
            if (change.Kind is ChangeKind.Modified or ChangeKind.Deleted)
                shipmentIds.Add(change.Original<Guid>(nameof(ShipmentLine.ShipmentId)));
        }

        // ── فرش‌هایی که چیزی از آن‌ها عوض می‌شود که شاید قفل باشد ──
        var rugIds = new HashSet<Guid>();
        foreach (var change in changes.Of<Rug>())
        {
            if (change.Kind == ChangeKind.Added) continue;
            if (change.Kind is ChangeKind.Deleted or ChangeKind.Unknown
                || change.HasAny(RugLockedFields) || change.Has(nameof(Rug.MetadataJson)))
                rugIds.Add(change.As<Rug>().Id);
        }

        foreach (var change in changes.Of<RugWorkflowStep>())
        {
            if (change.Kind is ChangeKind.Added or ChangeKind.Deleted or ChangeKind.Unknown || change.HasAny(StepLockedFields))
            {
                rugIds.Add(change.As<RugWorkflowStep>().RugId);
                if (change.Kind is ChangeKind.Modified or ChangeKind.Deleted)
                    rugIds.Add(change.Original<Guid>(nameof(RugWorkflowStep.RugId)));
            }
        }

        foreach (var change in changes.Of<RugSale>())
        {
            if (change.Kind is ChangeKind.Added or ChangeKind.Deleted or ChangeKind.Unknown)
                rugIds.Add(change.As<RugSale>().RugId);
        }

        rugIds.Remove(Guid.Empty);
        shipmentIds.Remove(Guid.Empty);

        // ── یک پرس‌وجو برای خط فرش‌ها و یکی برای وضعیت محموله‌ها ──
        var rugLines = rugIds.Count == 0
            ? new Dictionary<Guid, LinePlacement>()
            : new Dictionary<Guid, LinePlacement>(await RugLineCache.LinesOfRugsAsync(context, repository, rugIds, cancellationToken));

        var lookup = shipmentIds.Where(id => !knownStatus.ContainsKey(id) || unknownShipments.Contains(id)).ToList();
        var states = new Dictionary<Guid, ShipmentState>();
        if (lookup.Count > 0)
        {
            foreach (var state in await repository.ShipmentStatesAsync(context.TenantId, lookup, cancellationToken))
                states[state.Id] = state;
        }

        foreach (var placement in rugLines.Values)
            states.TryAdd(placement.ShipmentId, new ShipmentState(placement.ShipmentId, placement.ShipmentNumber, placement.Status));

        bool WasFinalized(Guid shipmentId)
            => knownStatus.TryGetValue(shipmentId, out var status)
                ? status == ShipmentStatus.Finalized
                : states.TryGetValue(shipmentId, out var state) && state.Status == ShipmentStatus.Finalized;

        int NumberOf(Guid shipmentId)
            => changes.Of<Shipment>().Select(c => c.As<Shipment>()).FirstOrDefault(s => s.Id == shipmentId) is { } tracked
                ? tracked.Number
                : states.TryGetValue(shipmentId, out var state) ? state.Number : 0;

        // ── سربرگ ──
        foreach (var change in changes.Of<Shipment>())
        {
            var shipment = change.As<Shipment>();
            if (!WasFinalized(shipment.Id)) continue;

            switch (change.Kind)
            {
                case ChangeKind.Deleted:
                case ChangeKind.Unknown:
                    throw HeaderLocked(shipment.Id, shipment.Number, null);
                case ChangeKind.Modified:
                    var reopening = statusChanging.Contains(shipment.Id);
                    var offending = ShipmentLockedFields.FirstOrDefault(f => change.Has(f) && !(reopening && ReopenFields.Contains(f)));
                    if (offending is not null)
                        throw HeaderLocked(shipment.Id, shipment.Number, Camel(offending));
                    break;
            }
        }

        // ── نگله‌ها ──
        foreach (var change in changes.Of<Bale>())
        {
            var bale = change.As<Bale>();
            var touched = change.Kind is ChangeKind.Added or ChangeKind.Deleted or ChangeKind.Unknown || change.HasAny(BaleLockedFields);
            if (!touched) continue;

            foreach (var shipmentId in Shipments(change, bale.ShipmentId, nameof(Bale.ShipmentId)))
            {
                if (WasFinalized(shipmentId))
                    throw HeaderLocked(shipmentId, NumberOf(shipmentId), "bale");
            }
        }

        // ── خط‌ها ──
        foreach (var change in changes.Of<ShipmentLine>())
        {
            var line = change.As<ShipmentLine>();
            var touched = change.Kind is ChangeKind.Added or ChangeKind.Deleted or ChangeKind.Unknown || change.HasAny(LineLockedFields);
            if (!touched) continue;

            foreach (var shipmentId in Shipments(change, line.ShipmentId, nameof(ShipmentLine.ShipmentId)))
            {
                if (WasFinalized(shipmentId))
                    throw HeaderLocked(shipmentId, NumberOf(shipmentId), "line");
            }
        }

        if (rugLines.Count == 0)
            return;

        // ── فرش‌ها، مراحل و فروش فرش‌های خط‌دارِ محمولهٔ قطعی ──
        LinePlacement? LockedLine(Guid rugId)
            => rugLines.TryGetValue(rugId, out var placement) && WasFinalized(placement.ShipmentId) ? placement : null;

        IReadOnlySet<string>? lockedCustomKeys = null;
        Dictionary<Guid, RugLockedValues>? stored = null;

        foreach (var change in changes.Of<Rug>())
        {
            var rug = change.As<Rug>();
            if (change.Kind == ChangeKind.Added || LockedLine(rug.Id) is not { } placement)
                continue;

            var reopening = statusChanging.Contains(placement.ShipmentId);
            switch (change.Kind)
            {
                case ChangeKind.Deleted:
                    throw RugLocked(placement, nameof(Rug.DeletedAt));

                case ChangeKind.Modified:
                {
                    var offending = RugLockedFields.FirstOrDefault(f => change.Has(f) && !(reopening && f == nameof(Rug.Status)));
                    if (offending is not null)
                        throw RugLocked(placement, offending);

                    if (change.Changes.TryGetValue(nameof(Rug.MetadataJson), out var metadata))
                    {
                        // مقدار تازه از خودِ تفاوت (همان که ذخیره می‌شود)، نه از موجودیت
                        lockedCustomKeys ??= await LockedCustomKeysAsync(context.TenantId, cancellationToken);
                        if (CustomChanged(metadata.Original as string, metadata.Current as string, lockedCustomKeys))
                            throw RugLocked(placement, nameof(Rug.MetadataJson));
                    }

                    break;
                }

                case ChangeKind.Unknown:
                {
                    stored ??= (await repository.RugValuesAsync(context.TenantId, rugLines.Keys.ToList(), cancellationToken))
                        .ToDictionary(v => v.Id);
                    if (!stored.TryGetValue(rug.Id, out var values))
                        throw RugLocked(placement, null);

                    var current = CurrentValues(rug);
                    var offending = RugLockedFields.FirstOrDefault(f =>
                        !(reopening && f == nameof(Rug.Status))
                        && !Equals(Normalize(values.Values.GetValueOrDefault(f)), Normalize(current[f])));
                    if (offending is not null)
                        throw RugLocked(placement, offending);

                    lockedCustomKeys ??= await LockedCustomKeysAsync(context.TenantId, cancellationToken);
                    if (CustomChanged(values.Values.GetValueOrDefault(nameof(Rug.MetadataJson)) as string, rug.MetadataJson, lockedCustomKeys))
                        throw RugLocked(placement, nameof(Rug.MetadataJson));
                    break;
                }
            }
        }

        foreach (var change in changes.Of<RugWorkflowStep>())
        {
            var step = change.As<RugWorkflowStep>();
            var touched = change.Kind is ChangeKind.Added or ChangeKind.Deleted or ChangeKind.Unknown || change.HasAny(StepLockedFields);
            if (!touched) continue;

            foreach (var rugId in new[] { step.RugId, change.Kind is ChangeKind.Modified or ChangeKind.Deleted
                         ? change.Original<Guid>(nameof(RugWorkflowStep.RugId)) : step.RugId }.Distinct())
            {
                if (LockedLine(rugId) is { } placement)
                    throw RugLocked(placement, "workflow");
            }
        }

        foreach (var change in changes.Of<RugSale>())
        {
            if (change.Kind is not (ChangeKind.Added or ChangeKind.Deleted or ChangeKind.Unknown)) continue;
            if (LockedLine(change.As<RugSale>().RugId) is { } placement)
                throw RugLocked(placement, "sale");
        }
    }

    // ═════════════════════════════════════════════════════════════════════

    /// <summary>محموله(های) یک نگله یا خط: فعلی و، برای ویرایش و حذف، اصلی.</summary>
    private static IEnumerable<Guid> Shipments(EntityChange change, Guid current, string property)
    {
        yield return current;
        if (change.Kind is ChangeKind.Modified or ChangeKind.Deleted)
        {
            var original = change.Original<Guid>(property);
            if (original != Guid.Empty && original != current)
                yield return original;
        }
    }

    /// <summary>کلیدهای سفارشیِ قفل‌دار: فیلدهای «خروجی‌پذیر» و فیلد کلید ارزش گمرکی اگر سفارشی است.</summary>
    private async Task<IReadOnlySet<string>> LockedCustomKeysAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var keys = (await repository.ExportableCustomKeysAsync(tenantId, cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var settings = await repository.SettingsAsync(tenantId, cancellationToken);
        if (DeclaredValueMath.CustomKeyOf(settings.KeyField) is { } keyField)
            keys.Add(keyField);
        return keys;
    }

    /// <summary>آیا یکی از کلیدهای قفل‌دار متادیتا مقدار دیگری گرفته (کلیدهای دیگر آزادند).</summary>
    public static bool CustomChanged(string? originalJson, string? currentJson, IReadOnlySet<string> lockedKeys)
        => lockedKeys.Any(key => !string.Equals(
            DeclaredValueMath.MetadataValue(originalJson, key),
            DeclaredValueMath.MetadataValue(currentJson, key),
            StringComparison.Ordinal));

    /// <summary>مقدار فعلی ستون‌های قفل‌دار یک فرش (برای سنجش «نامعلوم» با دیتابیس).</summary>
    public static IReadOnlyDictionary<string, object?> CurrentValues(Rug rug) => new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [nameof(Rug.IntakeLength)] = rug.IntakeLength,
        [nameof(Rug.IntakeWidth)] = rug.IntakeWidth,
        [nameof(Rug.FinalLength)] = rug.FinalLength,
        [nameof(Rug.FinalWidth)] = rug.FinalWidth,
        [nameof(Rug.FinalMeasuredAt)] = rug.FinalMeasuredAt,
        [nameof(Rug.FinalMeasuredBy)] = rug.FinalMeasuredBy,
        [nameof(Rug.LengthMeters)] = rug.LengthMeters,
        [nameof(Rug.WidthMeters)] = rug.WidthMeters,
        [nameof(Rug.Quantity)] = rug.Quantity,
        [nameof(Rug.SerialNo)] = rug.SerialNo,
        [nameof(Rug.SerialFrom)] = rug.SerialFrom,
        [nameof(Rug.SerialTo)] = rug.SerialTo,
        [nameof(Rug.SerialSort)] = rug.SerialSort,
        [nameof(Rug.Title)] = rug.Title,
        [nameof(Rug.Origin)] = rug.Origin,
        [nameof(Rug.Pattern)] = rug.Pattern,
        [nameof(Rug.Material)] = rug.Material,
        [nameof(Rug.KnotDensity)] = rug.KnotDensity,
        [nameof(Rug.Status)] = rug.Status,
        [nameof(Rug.DeletedAt)] = rug.DeletedAt,
        [nameof(Rug.WorkflowTemplateId)] = rug.WorkflowTemplateId,
        [nameof(Rug.CurrentStepIndex)] = rug.CurrentStepIndex,
        [nameof(Rug.MetadataJson)] = rug.MetadataJson
    };

    /// <summary>
    /// هم‌شکل کردن مقدار برای مقایسه: enum با عدد و زمان با UTC (decimal خودش با مقدار مقایسه می‌شود: ۲٫۱۰ = ۲٫۱).
    /// </summary>
    private static object? Normalize(object? value) => value switch
    {
        Enum e => Convert.ToInt32(e, System.Globalization.CultureInfo.InvariantCulture),
        DateTimeOffset t => t.UtcTicks,
        _ => value
    };

    private bool Business => visibility.Select(v => v.CanSeeMoney).DefaultIfEmpty(true).First();

    private ShipmentLockedException RugLocked(LinePlacement placement, string? field)
        => new(Business ? Messages.ShipmentLocked.Format(("number", placement.ShipmentNumber)) : Messages.ShipmentLockedOperator.Text,
            placement.ShipmentId, placement.ShipmentNumber, field is null ? null : "rug." + Camel(field));

    private ShipmentLockedException HeaderLocked(Guid shipmentId, int number, string? field)
        => new(Business ? Messages.ShipmentLockedHeader.Format(("number", number)) : Messages.ShipmentLockedOperator.Text,
            shipmentId, number, field);

    private static string Camel(string name) => name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
