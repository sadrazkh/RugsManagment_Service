using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Shipments;
using RugsManagment.Application.Features.Rugs;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Shipments;

/// <summary>«قطعی کن» و «باز کردن دوباره» (spec C.7، C.9).</summary>
public interface IFinalizeService
{
    /// <summary>
    /// با کمبود بازدارنده هیچ چیز عوض نمی‌شود و <see cref="FinalizeResultDto.Ok"/> = false با فهرست کامل؛ وگرنه در یک پاس:
    /// وضعیت قطعی، شمار قطعی + ۱، فرش‌های خط‌دار «صادر شده»، عکس متعارف و هش آن. نسخهٔ کهنه ۴۰۹.
    /// </summary>
    Task<FinalizeResultDto> FinalizeAsync(Guid tenantId, Guid shipmentId, FinalizeShipmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// باز: فرش‌ها به وضعیتی که <see cref="RugStatusResolver"/> می‌گوید برمی‌گردند، عکس به «عکس قبلی» می‌رود و ارزش خط‌ها از
    /// منبع‌های فعلی (جدول، نرخ پیش‌فرض، فیلد کلید) در همان پاس دوباره حساب می‌شوند. دلیل در تاریخچه.
    /// </summary>
    Task<ShipmentDto> ReopenAsync(Guid tenantId, Guid shipmentId, ReopenShipmentRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// «قطعی» و «باز کردن دوباره». هر دو در یک تراکنش و زیر قفل حساب‌های کارگاه کار می‌کنند (همان قفل پاس سازگاری)، پس
/// آمادگی و عکس همان داده‌ای را می‌بینند که ذخیره می‌شود و افزودنِ هم‌زمانِ یک فرش نمی‌تواند وسطش بنشیند. فرش‌ها با یک
/// پرس‌وجوی تقسیم‌شده (همهٔ فرش‌های خط‌دار با مراحل و فروش) بار و وضعیتشان در حافظه حل می‌شود (D.4 «Performance»).
/// <c>ShipmentLockGuard</c> تغییر وضعیت همین فرش‌ها را فقط وقتی می‌پذیرد که وضعیت خودِ محموله در همان ذخیره عوض شود.
/// </summary>
public sealed class FinalizeService(
    IShipmentRepository shipments,
    IShipmentSettingsReader settingsReader,
    IConcurrencyGuard concurrency,
    ILedgerLock ledgerLock,
    IUnitOfWork unitOfWork,
    IAuditLog audit,
    ICurrentUser currentUser,
    TimeProvider? time = null) : IFinalizeService
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public async Task<FinalizeResultDto> FinalizeAsync(
        Guid tenantId, Guid shipmentId, FinalizeShipmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        FinalizeResultDto? result = null;

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await ledgerLock.AcquireAsync(tenantId, ct);
            var shipment = await shipments.GetTrackedAsync(tenantId, shipmentId, ct)
                           ?? throw new KeyNotFoundException("محموله یافت نشد.");
            concurrency.Expect(shipment, request.Version);
            if (shipment.IsFinalized)
                throw new DomainConflictException(Messages.ShipmentAlreadyFinalized.Text);

            var settings = await settingsReader.GetAsync(tenantId, ct);
            var bales = await shipments.ListBalesAsync(tenantId, shipmentId, ct);
            var rows = await shipments.ListRowsAsync(tenantId, shipmentId, settings.KeyField, cancellationToken: ct);
            var customFields = await shipments.CustomFieldsAsync(tenantId, ct);
            var keyLabel = KeyLabel(settings, customFields);

            var issues = ReadinessService.Build(bales, rows, settings, shipment.TargetBaleAreaM2, keyLabel);
            if (issues.Any(i => i.Blocking))
            {
                // هیچ چیز عوض نشده؛ پاسخ از همین داده‌ای که آمادگی را با آن سنجیدیم
                result = new FinalizeResultDto(false, issues,
                    ShipmentDtoBuilder.Compose(HeaderOf(shipment, concurrency.VersionOf(shipment)), settings, bales, rows, keyLabel));
                return;
            }

            var header = HeaderOf(shipment, concurrency.VersionOf(shipment));
            var json = SnapshotBuilder.Build(new SnapshotInput(header, settings.Preferences, settings.KeyField, bales, rows, customFields));
            var now = _time.GetUtcNow();
            shipment.Status = ShipmentStatus.Finalized;
            shipment.FinalizeCount++;
            shipment.FinalizedAt = now;
            shipment.FinalizedBy = currentUser.UserId;
            shipment.SnapshotJson = json;
            shipment.SnapshotHash = SnapshotBuilder.Hash(json);
            shipment.UpdatedAt = now;

            var rugs = await shipments.ListLineRugsTrackedAsync(tenantId, shipmentId, ct);
            foreach (var rug in rugs)
            {
                var status = RugStatusResolver.Resolve(rug.Sale is not null, exported: true, RugStatusResolver.FromWorkflow(rug));
                if (rug.Status == status) continue;
                rug.Status = status;
                rug.UpdatedAt = now;
            }

            audit.Record(AuditAction.ShipmentFinalized, nameof(Shipment), shipment.Id,
                $"محمولهٔ {Persian(shipment.Number)} قطعی شد ({Persian(rows.Sum(r => r.Quantity))} تخته صادر شده).",
                shipment.Title, new { count = shipment.FinalizeCount, hash = shipment.SnapshotHash, lines = rows.Count });
            await unitOfWork.SaveChangesAsync(ct);

            // پاسخ بی خواندن دوباره: «قطعی» نگله‌ها، خط‌ها و ارزش‌ها را دست نمی‌زند (منجمد)؛ فقط نسخهٔ محموله و فرش‌هایی که
            // وضعیتشان عوض شد تازه است و EF آن‌ها را بعد از ذخیره دارد
            var versions = rugs.ToDictionary(r => r.Id, r => concurrency.VersionOf(r));
            var fresh = rows
                .Select(r => versions.TryGetValue(r.RugId, out var version) ? r with { RugVersion = version } : r)
                .ToList();
            result = new FinalizeResultDto(true, issues,
                ShipmentDtoBuilder.Compose(HeaderOf(shipment, concurrency.VersionOf(shipment)), settings, bales, fresh, keyLabel));
        }, cancellationToken);

        return result ?? throw new InvalidOperationException("نتیجهٔ «قطعی» ساخته نشد.");
    }

    public async Task<ShipmentDto> ReopenAsync(
        Guid tenantId, Guid shipmentId, ReopenShipmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = ShipmentService.Clean(request.Reason);
        if (reason is not null && reason.Length > Shipment.NotesMaxLength)
            throw AppValidationException.ForField("reason", Messages.TooLong.Format(("label", "دلیل"), ("n", Shipment.NotesMaxLength)));

        ShipmentDto? result = null;
        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await ledgerLock.AcquireAsync(tenantId, ct);
            var shipment = await shipments.GetTrackedAsync(tenantId, shipmentId, ct)
                           ?? throw new KeyNotFoundException("محموله یافت نشد.");
            concurrency.Expect(shipment, request.Version);
            if (!shipment.IsFinalized)
                throw new DomainConflictException(Messages.ShipmentNotFinalized.Text);
            var settings = await settingsReader.GetAsync(tenantId, ct);

            var now = _time.GetUtcNow();
            shipment.Status = ShipmentStatus.Open;
            shipment.LastSnapshotJson = shipment.SnapshotJson;
            shipment.SnapshotJson = null;
            shipment.SnapshotHash = null;
            shipment.UpdatedAt = now;

            var rugs = await shipments.ListLineRugsTrackedAsync(tenantId, shipmentId, ct);
            foreach (var rug in rugs)
            {
                var status = RugStatusResolver.Resolve(rug.Sale is not null, exported: false, RugStatusResolver.FromWorkflow(rug));
                if (rug.Status == status) continue;
                rug.Status = status;
                rug.UpdatedAt = now;
            }

            audit.Record(AuditAction.ShipmentReopened, nameof(Shipment), shipment.Id,
                reason is null
                    ? $"محمولهٔ {Persian(shipment.Number)} دوباره باز شد."
                    : $"محمولهٔ {Persian(shipment.Number)} دوباره باز شد: {reason}",
                shipment.Title, new { reason, count = shipment.FinalizeCount });
            await unitOfWork.SaveChangesAsync(ct);

            // ارزش خط‌ها را مرحلهٔ ارزش خط در همین پاس از منبع‌های فعلی حساب کرد؛ پس نگله‌ها و ردیف‌ها تازه خوانده می‌شوند
            // (همان اتصال و تراکنش)، ولی سربرگ و ترجیحات همان‌اند که در دست است
            var bales = await shipments.ListBalesAsync(tenantId, shipmentId, ct);
            var rows = await shipments.ListRowsAsync(tenantId, shipmentId, settings.KeyField, cancellationToken: ct);
            var keyLabel = DeclaredValueMath.CustomKeyOf(settings.KeyField) is null
                ? DeclaredValueMath.KeyFieldLabel(settings.KeyField)
                : KeyLabel(settings, await shipments.CustomFieldsAsync(tenantId, ct));
            result = ShipmentDtoBuilder.Compose(HeaderOf(shipment, concurrency.VersionOf(shipment)), settings, bales, rows, keyLabel);
        }, cancellationToken);

        return result ?? throw new InvalidOperationException("نتیجهٔ «باز کردن دوباره» ساخته نشد.");
    }

    /// <summary>
    /// سربرگ از خودِ محمولهٔ ردیابی‌شده (همان مقدارهایی که ذخیره می‌شوند) با نسخه‌ای که EF بعد از ذخیره دارد — یک پرس‌وجو
    /// کمتر برای عکس و برای پاسخ.
    /// </summary>
    private static ShipmentHeaderData HeaderOf(Shipment s, uint version)
        => new(s.Id, s.Number, s.Title, s.DocumentDate, s.Destination, s.BuyerName, s.DeclarationNo, s.CustomsOffice,
            s.Status, s.FinalizeCount, s.FinalizedAt, s.SnapshotHash, s.TargetBaleAreaM2, s.UsdRate, s.Notes, version,
            s.CreatedAt, s.UpdatedAt);

    /// <summary>برچسب فارسی فیلد کلید (فیلد سفارشی: برچسب خودش) — همان قاعدهٔ <see cref="ReadinessService"/>.</summary>
    private static string KeyLabel(ShipmentSettings settings, IReadOnlyList<CustomFieldInfo> customFields)
        => DeclaredValueMath.CustomKeyOf(settings.KeyField) is { } custom
            ? customFields.FirstOrDefault(f => string.Equals(f.Key, custom, StringComparison.Ordinal))?.Label ?? custom
            : DeclaredValueMath.KeyFieldLabel(settings.KeyField);

    private static string Persian(int number) => PersianText.ToPersianDigits(number.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
