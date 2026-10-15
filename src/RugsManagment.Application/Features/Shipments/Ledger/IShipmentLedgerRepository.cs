using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Shipments.Ledger;

/// <summary>
/// SQL پاس سازگاری برای محموله (پیاده‌سازی: <c>Infrastructure/Persistence/Repositories/ShipmentLedgerRepository</c>، در فهرست
/// سفید «نوشتن خام» آزمون معماری). خواندن‌ها را contributorها و قفل‌ها بعد از گرفتن قفل کارگاه و پیش از flush صدا می‌زنند
/// (پس وضعیتِ دیتابیسِ «پیش از این ذخیره» را می‌بینند)؛ نوشتن فقط <see cref="WriteValuesAsync"/> است، بعد از flush و فقط
/// در جدول مشتق‌شدهٔ <c>ShipmentLineValues</c> — هرگز در <c>Rugs</c> یا <c>ShipmentLines</c> (و xmin آن‌ها). همهٔ متدها
/// کارگاه را می‌گیرند و بیرون از آن چیزی نمی‌بینند.
/// </summary>
public interface IShipmentLedgerRepository
{
    /// <summary>
    /// خط‌هایی که فرششان، نگله‌شان یا محموله‌شان در این مجموعه‌هاست (هر مجموعهٔ null نادیده)، با وضعیت و شمارهٔ محموله و
    /// نگله. وضعیت دیتابیس «پیش از flush» است.
    /// </summary>
    Task<IReadOnlyList<LinePlacement>> LinesAsync(
        Guid tenantId, IReadOnlyCollection<Guid>? rugIds, IReadOnlyCollection<Guid>? baleIds, IReadOnlyCollection<Guid>? shipmentIds,
        CancellationToken cancellationToken);

    /// <summary>خط‌های محموله‌های باز که نرخ دستی ندارند، با مقدار فیلد کلید فرششان.</summary>
    Task<IReadOnlyList<OpenLineKey>> OpenLinesWithoutOverrideAsync(Guid tenantId, string keyField, CancellationToken cancellationToken);

    /// <summary>وضعیت و شمارهٔ محموله‌ها.</summary>
    Task<IReadOnlyList<ShipmentState>> ShipmentStatesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> shipmentIds, CancellationToken cancellationToken);

    /// <summary>محموله و شمارهٔ نگله‌ها.</summary>
    Task<IReadOnlyList<BaleState>> BaleStatesAsync(Guid tenantId, IReadOnlyCollection<Guid> baleIds, CancellationToken cancellationToken);

    /// <summary>
    /// مقدارهای ذخیره‌شدهٔ ستون‌های قفل‌دار فرش‌ها (سطل زباله هم) — برای فرشی که بی بارگذاری وصل شده و مقدار اصلی‌اش معلوم
    /// نیست (<c>ChangeKind.Unknown</c>، D.4 گام ۱).
    /// </summary>
    Task<IReadOnlyList<RugLockedValues>> RugValuesAsync(Guid tenantId, IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken);

    /// <summary>کلید فیلدهای سفارشی فعال «خروجی‌پذیر» (قفل: فقط همین‌ها در محمولهٔ قطعی قفل‌اند).</summary>
    Task<IReadOnlyList<string>> ExportableCustomKeysAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>ترجیحات ارزش گمرکی و واژهٔ نگله (بی ردیف: پیش‌فرض‌ها).</summary>
    Task<ShipmentLedgerSettings> SettingsAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>جدول ارزش گمرکی با کلید یکسان‌شده.</summary>
    Task<IReadOnlyDictionary<string, decimal>> RatesAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// ترجیحات و جدول ارزش گمرکی با هم، در یک رفت‌وبرگشت — مرحلهٔ ارزش خط با هر عمل خط و هر «باز کردن دوباره» اجرا
    /// می‌شود و هر دستور کمتر به بودجهٔ «قطعی/باز کردن ۳۰۰ خط زیر ۲۰ دستور» و «اندازه زیر ۲۰۰ میلی‌ثانیه» کمک می‌کند (D.4).
    /// </summary>
    Task<(ShipmentLedgerSettings Settings, IReadOnlyDictionary<string, decimal> Rates)> ValuationSourcesAsync(
        Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// ورودی محاسبهٔ ارزش خط‌ها (بعد از flush): مساحت مؤثر یک تخته (ستون تولیدشده)، تعداد، نرخ دستی، مقدار فیلد کلید و
    /// ارزش ذخیره‌شدهٔ فعلی برای diff. <paramref name="lineIds"/> null یعنی همهٔ خط‌های کارگاه (بازسازی).
    /// </summary>
    Task<IReadOnlyList<LineValueInput>> LineInputsAsync(
        Guid tenantId, IReadOnlyCollection<Guid>? lineIds, string keyField, CancellationToken cancellationToken);

    /// <summary>نوشتن (upsert) ردیف‌های ارزش؛ فقط ردیف‌هایی که داده می‌شوند (diff در C#). شمار نوشته‌ها.</summary>
    Task<int> WriteValuesAsync(Guid tenantId, IReadOnlyList<LineValueRow> rows, DateTimeOffset now, CancellationToken cancellationToken);
}

/// <summary>یک خط با جایش.</summary>
public sealed record LinePlacement(
    Guid LineId, Guid RugId, Guid ShipmentId, int ShipmentNumber, ShipmentStatus Status, Guid BaleId, int BaleNumber, bool HasOverride);

/// <summary>یک خط باز بی نرخ دستی با مقدار فیلد کلید.</summary>
public sealed record OpenLineKey(Guid LineId, Guid ShipmentId, Guid RugId, string? KeyValue);

/// <summary>وضعیت یک محموله.</summary>
public sealed record ShipmentState(Guid Id, int Number, ShipmentStatus Status);

/// <summary>یک نگله: محموله و شماره.</summary>
public sealed record BaleState(Guid Id, Guid ShipmentId, int Number);

/// <summary>ترجیحات پاس محموله.</summary>
public sealed record ShipmentLedgerSettings(string KeyField, decimal? FallbackUsd, BaleTerm BaleTerm);

/// <summary>مقدارهای ذخیره‌شدهٔ ستون‌های قفل‌دار یک فرش.</summary>
public sealed record RugLockedValues(
    Guid Id, bool HasSale, IReadOnlyDictionary<string, object?> Values);

/// <summary>ورودی محاسبهٔ ارزش یک خط و ارزش ذخیره‌شدهٔ فعلی.</summary>
public sealed record LineValueInput(
    Guid LineId, Guid ShipmentId, Guid RugId, ShipmentStatus ShipmentStatus, decimal PieceAreaM2, int Quantity,
    decimal? UsdPerM2Override, string? KeyValue,
    bool HasValue, Guid? StoredShipmentId, decimal? StoredUsdPerM2, DeclaredRateSource StoredSource,
    decimal? StoredPieceUsd, decimal StoredLineAreaM2, decimal? StoredLineUsd);

/// <summary>ردیفی که مرحلهٔ ارزش خط می‌نویسد.</summary>
public sealed record LineValueRow(
    Guid LineId, Guid ShipmentId, decimal? UsdPerM2, DeclaredRateSource RateSource, decimal? PieceUsd, decimal LineAreaM2, decimal? LineUsd);
