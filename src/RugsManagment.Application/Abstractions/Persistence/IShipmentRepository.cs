using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Abstractions.Persistence;

/// <summary>
/// خواندن و بارگذاری محموله، نگله، خط و جدول ارزش گمرکی (پیاده‌سازی: <c>Infrastructure/Persistence/Repositories/ShipmentRepository</c>).
///
/// قرارداد: هر متد کارگاه را می‌گیرد و بیرون از آن چیزی نمی‌بیند (شناسهٔ کارگاه دیگر = «پیدا نشد»). بارگذاری‌های
/// <c>…TrackedAsync</c> موجودیتِ ردیابی‌شده برمی‌گردانند تا تغییرشان از پاس سازگاری بگذرد؛ بقیه خواندن بی‌ردیابی‌اند
/// (projection یا SQL) و هرگز چیزی نمی‌نویسند. ردیف‌های مشتق‌شده (<see cref="ShipmentLineValue"/>) فقط خوانده می‌شوند.
/// </summary>
public interface IShipmentRepository
{
    // ── بارگذاری ردیابی‌شده برای نوشتن ──────────────────────────────────

    Task<Shipment?> GetTrackedAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default);

    Task<Bale?> GetBaleTrackedAsync(Guid tenantId, Guid baleId, CancellationToken cancellationToken = default);

    /// <summary>نگلهٔ محموله با این شماره (ردیابی‌شده)؛ null یعنی نیست.</summary>
    Task<Bale?> FindBaleByNumberTrackedAsync(Guid tenantId, Guid shipmentId, int number, CancellationToken cancellationToken = default);

    /// <summary>همهٔ نگله‌های محموله (ردیابی‌شده)، به ترتیب شماره.</summary>
    Task<IReadOnlyList<Bale>> ListBalesTrackedAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default);

    Task<ShipmentLine?> GetLineTrackedAsync(Guid tenantId, Guid lineId, CancellationToken cancellationToken = default);

    /// <summary>خط یک فرش (ردیابی‌شده، با نگله و محموله)؛ null یعنی فرش در هیچ نگله‌ای نیست.</summary>
    Task<ShipmentLine?> GetLineOfRugTrackedAsync(Guid tenantId, Guid rugId, CancellationToken cancellationToken = default);

    /// <summary>خط‌های یک نگله (ردیابی‌شده)، به ترتیب Seq و بعد شناسه.</summary>
    Task<IReadOnlyList<ShipmentLine>> ListLinesOfBaleTrackedAsync(Guid tenantId, Guid baleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// همهٔ فرش‌های خط‌دارِ محموله با مراحل و فروششان (ردیابی‌شده، یک پرس‌وجوی تقسیم‌شده با <c>WHERE "Id" = ANY</c>)، برای
    /// «قطعی» و «باز کردن دوباره» که وضعیت را در حافظه حل می‌کنند (D.4 «Performance»).
    /// </summary>
    Task<IReadOnlyList<Rug>> ListLineRugsTrackedAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default);

    /// <summary>همهٔ ردیف‌های جدول ارزش گمرکی (ردیابی‌شده).</summary>
    Task<IReadOnlyList<DeclaredValueRate>> ListRatesTrackedAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task AddShipmentAsync(Shipment shipment, CancellationToken cancellationToken = default);

    Task AddBaleAsync(Bale bale, CancellationToken cancellationToken = default);

    Task AddLineAsync(ShipmentLine line, CancellationToken cancellationToken = default);

    Task AddRateAsync(DeclaredValueRate rate, CancellationToken cancellationToken = default);

    void RemoveShipment(Shipment shipment);

    void RemoveBale(Bale bale);

    void RemoveLine(ShipmentLine line);

    void RemoveRate(DeclaredValueRate rate);

    // ── خواندن ───────────────────────────────────────────────────────────

    /// <summary>بزرگ‌ترین شمارهٔ نگلهٔ محموله؛ ۰ بی نگله.</summary>
    Task<int> MaxBaleNumberAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default);

    /// <summary>بزرگ‌ترین Seq نگله؛ ۰ بی خط.</summary>
    Task<int> MaxSeqAsync(Guid tenantId, Guid baleId, CancellationToken cancellationToken = default);

    /// <summary>سربرگ و نسخهٔ محموله (بی‌ردیابی)؛ null یعنی نیست.</summary>
    Task<ShipmentHeaderData?> GetHeaderAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// محموله‌های کارگاه (اختیاری فقط یک وضعیت) با جمع‌ها و ورودی‌های آمادگی، تازه‌ترین شماره اول — یک SQL گروهی (C.6).
    /// </summary>
    Task<IReadOnlyList<ShipmentListData>> ListAsync(
        Guid tenantId, ShipmentStatus? status, string keyField, CancellationToken cancellationToken = default);

    /// <summary>خلاصهٔ نگله‌های محموله به ترتیب شماره (جمع‌ها با قاعدهٔ دسته از ارزش خط‌ها).</summary>
    Task<IReadOnlyList<BaleData>> ListBalesAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// ردیف‌های محموله (یا فقط یک نگله، یا فقط چند خط): خط + فرش + ارزش، به ترتیب شمارهٔ نگله، Seq و شناسه.
    /// <paramref name="keyField"/> کلید فیلد ارزش گمرکی تا مقدارش در <see cref="ShipmentRowData.KeyValue"/> بیاید.
    /// </summary>
    Task<IReadOnlyList<ShipmentRowData>> ListRowsAsync(
        Guid tenantId, Guid shipmentId, string keyField, Guid? baleId = null, IReadOnlyCollection<Guid>? lineIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>وضع یک فرش برای افزودن به نگله (سطل زباله هم دیده می‌شود)؛ null یعنی در این کارگاه نیست.</summary>
    Task<RugLineState?> RugStateAsync(Guid tenantId, Guid rugId, CancellationToken cancellationToken = default);

    /// <summary>جای فرش‌ها در نگله‌ها (برای enricher و جست‌وجوی شماره)؛ فرش بی‌خط در نتیجه نیست.</summary>
    Task<IReadOnlyList<RugPlacementData>> PlacementsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// کارگاه(های) این فرش‌ها — تنها خواندن بی کارگاه، برای enricher بیرون از درخواست کاربر (seed و کار استارت) که DTO
    /// فرشش کارگاه ندارد؛ چیزی جز شناسهٔ کارگاه برنمی‌گرداند.
    /// </summary>
    Task<IReadOnlyList<Guid>> TenantsOfRugsAsync(IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken = default);

    /// <summary>جدول ارزش گمرکی (بی‌ردیابی)، به ترتیب کلید.</summary>
    Task<IReadOnlyList<DeclaredValueRate>> ListRatesAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>مقدار فیلد کلید خط‌های محموله‌های باز که نرخ دستی ندارند (برای «چند ردیف دوباره حساب شد»).</summary>
    Task<IReadOnlyList<string?>> OpenLineKeyValuesAsync(Guid tenantId, string keyField, CancellationToken cancellationToken = default);

    /// <summary>فیلدهای سفارشی فعال فرش (کلید، برچسب، «خروجی‌پذیر») — برای عکس قطعی، قفل و برچسب فیلد کلید.</summary>
    Task<IReadOnlyList<CustomFieldInfo>> CustomFieldsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>محموله‌های باز با جمع‌ها و زمان آخرین تغییر نگله‌ها (برای خانه)، تازه‌ترین فعالیت اول.</summary>
    Task<IReadOnlyList<ShipmentListData>> ListOpenForHomeAsync(
        Guid tenantId, string keyField, CancellationToken cancellationToken = default);

    /// <summary>«انبار»: فرش‌های فعالِ نه فروخته و نه در هیچ نگله — Σ تعداد و Σ متراژ کل.</summary>
    Task<(int Pieces, decimal AreaM2)> StockAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>سربرگ یک محموله با نسخه.</summary>
public sealed record ShipmentHeaderData(
    Guid Id, int Number, string Title, DateOnly DocumentDate, string? Destination, string? BuyerName,
    string? DeclarationNo, string? CustomsOffice, ShipmentStatus Status, int FinalizeCount, DateTimeOffset? FinalizedAt,
    string? SnapshotHash, decimal? TargetBaleAreaM2, decimal? UsdRate, string? Notes, uint Version,
    DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);

/// <summary>
/// یک محموله در فهرست با جمع‌ها و ورودی‌های آمادگی (C.6): نگله‌های خالی، فرش‌های بی‌اندازهٔ نهایی، و مقدار فیلد کلیدِ
/// خط‌های «ندارد» (یکسان‌سازی و شمارش کلیدها در C# انجام می‌شود، چون KeyNormalize در SQL نیست).
/// </summary>
public sealed record ShipmentListData(
    Guid Id, int Number, string Title, DateOnly DocumentDate, ShipmentStatus Status,
    int Bales, int Lines, int Pieces, decimal AreaM2, decimal UsdTotal,
    int EmptyBales, int NoFinalSize, IReadOnlyList<string?> MissingKeyValues, DateTimeOffset? LastActivityAt);

/// <summary>یک نگله با جمع‌ها.</summary>
public sealed record BaleData(
    Guid Id, int Number, uint Version, decimal? WeightKg, string? Notes, int Lines, int Pieces, decimal AreaM2, decimal UsdTotal);

/// <summary>یک ردیف نگله: خط، فرش و ارزش دلاری ذخیره‌شده.</summary>
public sealed record ShipmentRowData(
    Guid LineId, uint LineVersion, int Seq, decimal? UsdPerM2Override,
    Guid BaleId, int BaleNumber,
    Guid RugId, uint RugVersion, string Sku, string? SerialNo, int Quantity,
    string? Title, string? Origin, string? Pattern, string? Material, int? KnotDensity, string? MetadataJson,
    decimal IntakeLength, decimal IntakeWidth, decimal? FinalLength, decimal? FinalWidth, decimal AreaM2,
    bool HasValue, decimal? UsdPerM2, DeclaredRateSource RateSource, decimal? PieceUsd, decimal LineAreaM2, decimal? LineUsd,
    string? KeyValue);

/// <summary>وضع یک فرش برای افزودن به نگله.</summary>
public sealed record RugLineState(Guid Id, string Sku, string? SerialNo, int? SerialFrom, int? SerialTo, int Quantity, bool Deleted, bool HasSale);

/// <summary>یک فیلد سفارشی فعال فرش.</summary>
public sealed record CustomFieldInfo(string Key, string Label, bool Exportable);

/// <summary>جای یک فرش: خطش، نگله و محموله و ارزش دلاری.</summary>
public sealed record RugPlacementData(
    Guid RugId, Guid LineId, Guid ShipmentId, int ShipmentNumber, ShipmentStatus ShipmentStatus,
    Guid BaleId, int BaleNumber, decimal? UsdPerM2, decimal? LineUsd);
