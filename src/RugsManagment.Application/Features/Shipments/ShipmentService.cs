using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Shipments;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Shipments;

/// <summary>فهرست، ساخت، ویرایش سربرگ و حذف محموله (spec C.1، I.2).</summary>
public interface IShipmentService
{
    /// <summary>محموله‌های کارگاه، تازه‌ترین شماره اول؛ با <paramref name="status"/> فقط همان وضعیت.</summary>
    Task<IReadOnlyList<ShipmentListItemDto>> ListAsync(Guid tenantId, ShipmentStatus? status = null, CancellationToken cancellationToken = default);

    /// <summary>محمولهٔ کامل؛ کارگاه دیگر → <see cref="KeyNotFoundException"/>.</summary>
    Task<ShipmentDto> GetAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default);

    /// <summary>«محمولهٔ تازه» با یک ضربه (Business).</summary>
    Task<ShipmentDto> CreateAsync(Guid tenantId, CreateShipmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>ویرایش سربرگ (Business)؛ نسخهٔ کهنه ۴۰۹؛ روی محمولهٔ قطعی فقط یادداشت و متراژ هدف (C.8).</summary>
    Task<ShipmentDto> UpdateAsync(Guid tenantId, Guid shipmentId, UpdateShipmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// حذف (Business): فقط محمولهٔ باز و بی‌خط؛ نگله‌های خالی با آن می‌روند (C.1). هزینه‌هایی که به آن وصل بودند «تقسیم‌نشده»
    /// می‌شوند (کلید خارجی SET NULL فاز هزینه‌ها). <paramref name="version"/> اختیاری؛ اگر هست باید تازه باشد.
    /// </summary>
    Task DeleteAsync(Guid tenantId, Guid shipmentId, uint? version = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// سرویس محموله. همهٔ نوشتن‌ها از <see cref="IUnitOfWork"/> (پاس سازگاری) می‌گذرند؛ قفل «قطعی» را
/// <c>ShipmentLockGuard</c> در خودِ پاس اجرا می‌کند، پس این سرویس فقط پیام‌های پیش‌دستانه (حذف محمولهٔ پُر) را می‌دهد.
/// </summary>
public sealed class ShipmentService(
    IShipmentRepository shipments,
    IShipmentDtoBuilder dtos,
    IShipmentSettingsReader settingsReader,
    ITenantCounters counters,
    IConcurrencyGuard concurrency,
    IUnitOfWork unitOfWork,
    IAuditLog audit,
    TimeProvider? time = null) : IShipmentService
{
    /// <summary>بزرگ‌ترین متراژ هدف نگله (numeric(6,2)).</summary>
    public const decimal MaxTargetAreaM2 = 9999.99m;

    /// <summary>بزرگ‌ترین نرخ دلار (numeric(18,6)).</summary>
    public const decimal MaxUsdRate = 999_999_999_999.999999m;

    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public async Task<IReadOnlyList<ShipmentListItemDto>> ListAsync(
        Guid tenantId, ShipmentStatus? status = null, CancellationToken cancellationToken = default)
    {
        var settings = await settingsReader.GetAsync(tenantId, cancellationToken);
        var rows = await shipments.ListAsync(tenantId, status, settings.KeyField, cancellationToken);
        return rows.Select(r => ToListItem(r, settings)).ToList();
    }

    /// <summary>یک ردیف فهرست با شمار تراشه‌های بازدارنده (همان قاعده‌های <see cref="ReadinessRules"/>).</summary>
    public static ShipmentListItemDto ToListItem(ShipmentListData r, ShipmentSettings settings)
        => new(r.Id, r.Number, r.Title, r.DocumentDate, r.Status, r.Bales, r.Lines, r.Pieces, r.AreaM2, r.UsdTotal,
            r.Status == ShipmentStatus.Finalized
                ? 0
                : ReadinessRules.BlockingCount(r.Lines, r.EmptyBales, r.NoFinalSize, settings.RequireFinalSize, r.MissingKeyValues));

    public Task<ShipmentDto> GetAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default)
        => dtos.BuildAsync(tenantId, shipmentId, cancellationToken);

    public async Task<ShipmentDto> CreateAsync(Guid tenantId, CreateShipmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var settings = await settingsReader.GetAsync(tenantId, cancellationToken);
        var header = Validate(request.Title, request.Destination, request.BuyerName, request.DeclarationNo,
            request.CustomsOffice, request.TargetBaleAreaM2, request.UsdRate, request.Notes);

        var number = checked((int)await counters.NextBlockAsync(tenantId, Shipment.CounterKey, 1, cancellationToken));
        var shipment = new Shipment
        {
            TenantId = tenantId,
            Number = number,
            Title = header.Title ?? DefaultTitle(number),
            DocumentDate = request.DocumentDate == default ? Intake.IranDay.Today(_time.GetUtcNow()) : request.DocumentDate,
            Destination = header.Destination,
            BuyerName = header.BuyerName,
            DeclarationNo = header.DeclarationNo,
            CustomsOffice = header.CustomsOffice ?? Clean(settings.Preferences.CustomsOffice),
            TargetBaleAreaM2 = header.TargetBaleAreaM2 ?? settings.Preferences.DefaultBaleTargetAreaM2,
            UsdRate = header.UsdRate ?? settings.Preferences.DefaultUsdFx,
            Notes = header.Notes,
            Status = ShipmentStatus.Open,
            CreatedAt = _time.GetUtcNow()
        };

        await shipments.AddShipmentAsync(shipment, cancellationToken);
        audit.Record(AuditAction.ShipmentCreated, nameof(Shipment), shipment.Id,
            $"محمولهٔ {Persian(number)} ساخته شد ({shipment.Title}).", shipment.Title, new { number });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await dtos.BuildAsync(tenantId, shipment.Id, cancellationToken);
    }

    public async Task<ShipmentDto> UpdateAsync(
        Guid tenantId, Guid shipmentId, UpdateShipmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var shipment = await shipments.GetTrackedAsync(tenantId, shipmentId, cancellationToken)
                       ?? throw new KeyNotFoundException("محموله یافت نشد.");
        var header = Validate(request.Title, request.Destination, request.BuyerName, request.DeclarationNo,
            request.CustomsOffice, request.TargetBaleAreaM2, request.UsdRate, request.Notes);

        concurrency.Expect(shipment, request.Version);
        shipment.Title = header.Title ?? DefaultTitle(shipment.Number);
        if (request.DocumentDate != default)
            shipment.DocumentDate = request.DocumentDate;
        shipment.Destination = header.Destination;
        shipment.BuyerName = header.BuyerName;
        shipment.DeclarationNo = header.DeclarationNo;
        shipment.CustomsOffice = header.CustomsOffice;
        shipment.TargetBaleAreaM2 = header.TargetBaleAreaM2;
        shipment.UsdRate = header.UsdRate;
        shipment.Notes = header.Notes;
        // نسخه همیشه سنجیده شود، حتی وقتی هیچ ستونی واقعاً عوض نشده (UPDATE با WHERE xmin)
        shipment.UpdatedAt = _time.GetUtcNow();

        audit.Record(AuditAction.ShipmentUpdated, nameof(Shipment), shipment.Id,
            $"سربرگ محمولهٔ {Persian(shipment.Number)} ویرایش شد.", shipment.Title);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await dtos.BuildAsync(tenantId, shipment.Id, cancellationToken);
    }

    public async Task DeleteAsync(Guid tenantId, Guid shipmentId, uint? version = null, CancellationToken cancellationToken = default)
    {
        var shipment = await shipments.GetTrackedAsync(tenantId, shipmentId, cancellationToken)
                       ?? throw new KeyNotFoundException("محموله یافت نشد.");
        if (version is { } v)
            concurrency.Expect(shipment, v);

        // پیام‌های پیش‌دستانه؛ همین دو قاعده در BaleMembershipGuard هم هست تا هیچ مسیری دورش نزند
        if (shipment.IsFinalized)
            throw new Domain.Common.DomainConflictException(Messages.ShipmentDeleteFinalized.Text);

        var bales = await shipments.ListBalesAsync(tenantId, shipmentId, cancellationToken);
        if (bales.Any(b => b.Lines > 0))
            throw new Domain.Common.DomainConflictException(Messages.ShipmentNotEmpty.Text);

        shipments.RemoveShipment(shipment);
        audit.Record(AuditAction.ShipmentDeleted, nameof(Shipment), shipment.Id,
            $"محمولهٔ {Persian(shipment.Number)} حذف شد.", shipment.Title, new { number = shipment.Number, bales = bales.Count });
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // ═════════════════════════════════════════════════════════════════════

    /// <summary>عنوان پیش‌فرض «اظهار ۸».</summary>
    public static string DefaultTitle(int number) => Messages.ShipmentDefaultTitle.Format(("number", number));

    private sealed record Header(
        string? Title, string? Destination, string? BuyerName, string? DeclarationNo, string? CustomsOffice,
        decimal? TargetBaleAreaM2, decimal? UsdRate, string? Notes);

    /// <summary>متن‌ها یکسان‌سازی و طولشان سنجیده می‌شود؛ عددها نامنفی و در دقت ستون. همهٔ خطاها با هم (۴۰۰).</summary>
    private static Header Validate(
        string? title, string? destination, string? buyerName, string? declarationNo, string? customsOffice,
        decimal? target, decimal? usdRate, string? notes)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        string? Text(string field, string label, string? value, int max)
        {
            var cleaned = Clean(value);
            if (cleaned is not null && cleaned.Length > max)
                errors[field] = [Messages.TooLong.Format(("label", label), ("n", max))];
            return cleaned;
        }

        var header = new Header(
            Text("title", "عنوان", title, Shipment.TitleMaxLength),
            Text("destination", "مقصد", destination, Shipment.DestinationMaxLength),
            Text("buyerName", "خریدار", buyerName, Shipment.BuyerNameMaxLength),
            Text("declarationNo", "شمارهٔ اظهار", declarationNo, Shipment.DeclarationNoMaxLength),
            Text("customsOffice", "گمرک", customsOffice, Shipment.CustomsOfficeMaxLength),
            target, usdRate,
            Text("notes", "یادداشت", notes, Shipment.NotesMaxLength));

        if (target is { } t && (t < 0 || t > MaxTargetAreaM2 || !Domain.Common.Num.HasAtMostDecimals(t, 2)))
            errors["targetBaleAreaM2"] = [Messages.OutOfRange.Format(("label", "متراژ هدف هر نگله"), ("min", 0), ("max", MaxTargetAreaM2))];
        if (usdRate is { } u && (u < 0 || u > MaxUsdRate || !Domain.Common.Num.HasAtMostDecimals(u, 6)))
            errors["usdRate"] = [Messages.NegativeNotAllowed.Text];

        if (errors.Count > 0)
            throw new AppValidationException(Messages.ValidationFailed.Text, errors);
        return header;
    }

    /// <summary>متن یکسان‌شده (فاصله‌ها، ی و ک)؛ خالی → null. ارقام همان‌طور که نوشته شده‌اند می‌مانند.</summary>
    internal static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim()
            .Replace(PersianText.ArabicYeh, PersianText.PersianYeh, StringComparison.Ordinal)
            .Replace(PersianText.ArabicKaf, PersianText.PersianKaf, StringComparison.Ordinal)
            .Normalize(System.Text.NormalizationForm.FormC);
        return text.Length == 0 ? null : text;
    }

    private static string Persian(int number) => PersianText.ToPersianDigits(number.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
