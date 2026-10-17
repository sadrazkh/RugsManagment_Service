using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.DTOs.Shipments;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Shipments;

/// <summary>ساختن DTOهای محموله از داده‌ای که repository خوانده (یک جا برای همهٔ سرویس‌ها).</summary>
public interface IShipmentDtoBuilder
{
    /// <summary>محمولهٔ کامل با نگله‌ها، جمع و آمادگی؛ محمولهٔ کارگاه دیگر → <see cref="KeyNotFoundException"/>.</summary>
    Task<ShipmentDto> BuildAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default);

    /// <summary>نگله‌های محموله با ردیف‌هایشان.</summary>
    Task<IReadOnlyList<BaleDto>> BalesAsync(Guid tenantId, Guid shipmentId, Guid? baleId = null, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class ShipmentDtoBuilder(
    IShipmentRepository shipments,
    IShipmentSettingsReader settingsReader,
    ReadinessService readiness) : IShipmentDtoBuilder
{
    public async Task<ShipmentDto> BuildAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var header = await shipments.GetHeaderAsync(tenantId, shipmentId, cancellationToken)
                     ?? throw new KeyNotFoundException("محموله یافت نشد.");
        var settings = await settingsReader.GetAsync(tenantId, cancellationToken);
        var bales = await shipments.ListBalesAsync(tenantId, shipmentId, cancellationToken);
        var rows = await shipments.ListRowsAsync(tenantId, shipmentId, settings.KeyField, cancellationToken: cancellationToken);
        var label = await readiness.KeyLabelAsync(tenantId, settings, cancellationToken);
        return Compose(header, settings, bales, rows, label);
    }

    /// <summary>
    /// همان DTO از داده‌ای که از پیش خوانده شده (خالص، بی دیتابیس) — «قطعی» و «باز کردن دوباره» با آن پاسخ را بی خواندن
    /// دوباره می‌سازند تا محمولهٔ ۳۰۰ خطی زیر ۲۰ دستور SQL بماند (D.4 «Performance»).
    /// </summary>
    public static ShipmentDto Compose(
        ShipmentHeaderData header, ShipmentSettings settings, IReadOnlyList<BaleData> bales, IReadOnlyList<ShipmentRowData> rows,
        string keyFieldLabel)
        => new(
            header.Id, header.Number, header.Title, header.DocumentDate, header.Destination, header.BuyerName,
            header.DeclarationNo, header.CustomsOffice, header.Status, header.FinalizeCount, header.FinalizedAt,
            header.SnapshotHash, header.TargetBaleAreaM2, header.UsdRate, header.Notes, header.Version,
            bales.Select(ShipmentMapping.ToSummary).ToList(),
            ShipmentMapping.Totals(bales),
            ReadinessService.Build(bales, rows, settings, header.TargetBaleAreaM2, keyFieldLabel),
            settings.TargetFor(header.TargetBaleAreaM2),
            settings.TolerancePercent);

    public async Task<IReadOnlyList<BaleDto>> BalesAsync(
        Guid tenantId, Guid shipmentId, Guid? baleId = null, CancellationToken cancellationToken = default)
    {
        var header = await shipments.GetHeaderAsync(tenantId, shipmentId, cancellationToken)
                     ?? throw new KeyNotFoundException("محموله یافت نشد.");
        var settings = await settingsReader.GetAsync(tenantId, cancellationToken);
        var bales = await shipments.ListBalesAsync(tenantId, shipmentId, cancellationToken);
        var rows = await shipments.ListRowsAsync(tenantId, shipmentId, settings.KeyField, baleId, cancellationToken: cancellationToken);
        var target = settings.TargetFor(header.TargetBaleAreaM2);
        var byBale = rows.ToLookup(r => r.BaleId);

        return bales
            .Where(b => baleId is null || b.Id == baleId)
            .Select(b => new BaleDto(b.Id, b.Number, b.Version, b.WeightKg, b.Notes, b.Pieces, b.AreaM2, b.UsdTotal, target,
                byBale[b.Id].Select(ShipmentMapping.ToRow).ToList()))
            .ToList();
    }
}

/// <summary>نگاشت دادهٔ repository به DTO.</summary>
public static class ShipmentMapping
{
    /// <summary>کلید فیلد سفارشیِ «رنگ متن» (رنگ زمینه) در متادیتای فرش.</summary>
    public const string FieldColorKey = "bg_color";

    /// <summary>کلید فیلد سفارشیِ «رنگ حاشیه».</summary>
    public const string BorderColorKey = "border_color";

    public static BaleSummaryDto ToSummary(BaleData b)
        => new(b.Id, b.Number, b.Version, b.Lines, b.Pieces, b.AreaM2, b.UsdTotal, b.WeightKg);

    public static ShipmentTotalsDto Totals(IReadOnlyList<BaleData> bales)
        => new(bales.Count, bales.Sum(b => b.Lines), bales.Sum(b => b.Pieces), bales.Sum(b => b.AreaM2), bales.Sum(b => b.UsdTotal));

    /// <summary>
    /// یک ردیف نگله. اگر ردیف ارزش هنوز نیست (نباید پیش بیاید؛ پاس همیشه می‌نویسد)، متراژ با قاعدهٔ دسته از خودِ فرش و نرخ
    /// «ندارد» گزارش می‌شود، نه صفر.
    /// </summary>
    public static BaleRowDto ToRow(ShipmentRowData r)
        => new(
            r.LineId, r.LineVersion, r.RugId, r.RugVersion, r.Seq, r.SerialNo, r.Quantity,
            DeclaredValueMath.MetadataValue(r.MetadataJson, FieldColorKey),
            DeclaredValueMath.MetadataValue(r.MetadataJson, BorderColorKey),
            r.Pattern, r.Origin, r.Material,
            r.IntakeLength, r.IntakeWidth, r.FinalLength, r.FinalWidth,
            DeclaredValueMath.SizeChangeCm(r.IntakeLength, r.IntakeWidth, r.FinalLength, r.FinalWidth),
            r.AreaM2,
            r.HasValue ? r.LineAreaM2 : r.AreaM2 * r.Quantity,
            r.HasValue ? r.UsdPerM2 : null,
            r.HasValue ? r.RateSource : DeclaredRateSource.Missing,
            r.HasValue ? r.PieceUsd : null,
            r.HasValue ? r.LineUsd : null,
            r.BaleId, r.BaleNumber, r.UsdPerM2Override, r.Sku, r.KeyValue);
}
