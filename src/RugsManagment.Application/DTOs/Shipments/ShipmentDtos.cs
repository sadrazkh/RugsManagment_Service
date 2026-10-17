using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.DTOs.Shipments;

// قراردادهای محموله (spec I.3) — آینهٔ TS در CA/lib/types/shipments.ts. رکوردهای مکانی‌اند؛ عضو تازه فقط به انتها و با
// پیش‌فرض اضافه می‌شود (I.1). هیچ‌کدام مبلغ تومانی ندارد: دلار گمرکی هزینهٔ داخلی نیست و برای همهٔ نقش‌ها دیده می‌شود (A.5).

/// <summary>
/// ساخت محموله (Business). «محمولهٔ تازه» با یک ضربه و بی پنجره: همهٔ عضوها اختیاری‌اند — عنوان خالی «اظهار {شماره}»،
/// تاریخ خالی (<c>0001-01-01</c>) امروزِ ایران، و متراژ هدف و نرخ دلار خالی از ترجیحات کارگاه (C.1).
/// </summary>
public record CreateShipmentRequest(
    string? Title,
    DateOnly DocumentDate,
    string? Destination,
    string? BuyerName,
    string? DeclarationNo,
    string? CustomsOffice,
    decimal? TargetBaleAreaM2,
    decimal? UsdRate,
    string? Notes);

/// <summary>
/// ویرایش سربرگ (Business): همهٔ فیلدها با هم جایگزین می‌شوند (خالی یعنی پاک)، جز عنوان خالی که «اظهار {شماره}» می‌شود و
/// تاریخ خالی که همان قبلی می‌ماند. <see cref="Version"/> همان <c>ShipmentDto.Version</c>؛ کهنه → ۴۰۹. روی محمولهٔ قطعی هر
/// تغییری جز یادداشت و متراژ هدف ۴۰۹ است (C.8).
/// </summary>
public record UpdateShipmentRequest(
    string? Title,
    DateOnly DocumentDate,
    string? Destination,
    string? BuyerName,
    string? DeclarationNo,
    string? CustomsOffice,
    decimal? TargetBaleAreaM2,
    decimal? UsdRate,
    string? Notes,
    uint Version);

/// <summary>یک ردیف فهرست محموله‌ها؛ <see cref="ReadinessBlocking"/> از یک SQL گروهی برای همهٔ فهرست (C.6).</summary>
public record ShipmentListItemDto(
    Guid Id,
    int Number,
    string Title,
    DateOnly DocumentDate,
    ShipmentStatus Status,
    int Bales,
    int Lines,
    int Pieces,
    decimal AreaM2,
    decimal UsdTotal,
    int ReadinessBlocking);

/// <summary>
/// محمولهٔ کامل برای سربرگ و فضای کار. <see cref="Version"/> = xmin محموله (برای PUT، قطعی، باز کردن دوباره و
/// شماره‌گذاری دوبارهٔ نگله‌ها). <see cref="Readiness"/> همان <c>GET …/readiness</c> بدون قالب.
/// </summary>
public record ShipmentDto(
    Guid Id,
    int Number,
    string Title,
    DateOnly DocumentDate,
    string? Destination,
    string? BuyerName,
    string? DeclarationNo,
    string? CustomsOffice,
    ShipmentStatus Status,
    int FinalizeCount,
    DateTimeOffset? FinalizedAt,
    string? SnapshotHash,
    decimal? TargetBaleAreaM2,
    decimal? UsdRate,
    string? Notes,
    uint Version,
    IReadOnlyList<BaleSummaryDto> Bales,
    ShipmentTotalsDto Totals,
    IReadOnlyList<ReadinessIssueDto> Readiness,
    // ── افزوده بر I.3 (دنباله‌دار) ─────────────────────────────────────────
    /// <summary>متراژ هدف مؤثر هر نگله: خودِ محموله ?? ترجیح کارگاه؛ null یعنی بی‌هدف (نوار هدف خنثی).</summary>
    decimal? EffectiveTargetAreaM2 = null,
    /// <summary>تلورانس هدف (درصد، ترجیح کارگاه) — بالاتر از هدف × (۱ + t) نوار نارنجی می‌شود.</summary>
    short BaleTargetTolerancePercent = 10);

/// <summary>خلاصهٔ یک نگله (فهرست نگله‌ها، پانویس‌ها). جمع‌ها با قاعدهٔ دسته از <c>ShipmentLineValue</c>.</summary>
public record BaleSummaryDto(
    Guid Id,
    int Number,
    uint Version,
    int Lines,
    int Pieces,
    decimal AreaM2,
    decimal UsdTotal,
    decimal? WeightKg);

/// <summary>یک نگله با ردیف‌هایش (<c>GET /api/shipments/{id}/bales</c>).</summary>
public record BaleDto(
    Guid Id,
    int Number,
    uint Version,
    decimal? WeightKg,
    string? Notes,
    int Pieces,
    decimal AreaM2,
    decimal UsdTotal,
    decimal? TargetAreaM2,
    IReadOnlyList<BaleRowDto> Rows);

/// <summary>
/// یک ردیف نگله: خط + فرش + ارزش دلاری. <see cref="AreaM2"/> مساحت یک تخته در اندازهٔ مؤثر، <see cref="LineAreaM2"/> همان ×
/// تعداد؛ <see cref="LineUsd"/> = <see cref="PieceUsd"/> × تعداد. <see cref="SizeChangeCm"/> تغییر علامت‌دارِ بزرگ‌ترِ طول یا عرض
/// به سانت (منفی = کوچک‌تر شده)؛ null وقتی اندازهٔ نهایی نیست. <see cref="FieldColor"/> و <see cref="BorderColor"/> از
/// فیلدهای سفارشی <c>bg_color</c> و <c>border_color</c>.
/// </summary>
public record BaleRowDto(
    Guid LineId,
    uint LineVersion,
    Guid RugId,
    uint RugVersion,
    int Seq,
    string? Serial,
    int Qty,
    string? FieldColor,
    string? BorderColor,
    string? Pattern,
    string? Origin,
    string? Material,
    decimal IntakeLength,
    decimal IntakeWidth,
    decimal? FinalLength,
    decimal? FinalWidth,
    int? SizeChangeCm,
    decimal AreaM2,
    decimal LineAreaM2,
    decimal? UsdPerM2,
    DeclaredRateSource RateSource,
    decimal? PieceUsd,
    decimal? LineUsd,
    // ── افزوده بر I.3 (دنباله‌دار) ─────────────────────────────────────────
    /// <summary>نگلهٔ این ردیف (برای جدول یکپارچهٔ همهٔ نگله‌ها).</summary>
    Guid BaleId = default,
    /// <summary>شمارهٔ همان نگله.</summary>
    int BaleNumber = 0,
    /// <summary>نرخ دستیِ خط (بازرگان)؛ null یعنی از جدول یا پیش‌فرض.</summary>
    decimal? UsdPerM2Override = null,
    /// <summary>کد انبار (در حالت ساده کوچک نشان داده می‌شود).</summary>
    string? Sku = null,
    /// <summary>مقدار فیلد کلید ارزش گمرکی این فرش (برای فرم «نرخ بگذارید»).</summary>
    string? DeclaredKey = null);

/// <summary>جمع محموله.</summary>
public record ShipmentTotalsDto(int Bales, int Lines, int Pieces, decimal AreaM2, decimal UsdTotal)
{
    public static readonly ShipmentTotalsDto Empty = new(0, 0, 0, 0m, 0m);
}

/// <summary>
/// یک تراشهٔ آمادگی (C.6) با پیام فارسی از <c>Messages</c>. <see cref="RugIds"/> برای «[نشانم بده]» و
/// «[بقیه همان اندازهٔ اول]». تراشهٔ یک نگله (<c>E_EMPTY_BALE</c>، <c>W_BALE_OVER_TARGET</c>) نگله‌اش را دارد و تراشهٔ
/// ارزش گمرکی (<c>E_NO_DECLARED_VALUE</c>، یکی برای هر مقدار فیلد کلید) همان مقدار را برای فرم «نرخ بگذارید».
/// </summary>
public record ReadinessIssueDto(
    string Code,
    string Message,
    int Count,
    IReadOnlyList<Guid> RugIds,
    bool Blocking,
    // ── افزوده بر I.3 (دنباله‌دار) ─────────────────────────────────────────
    string? Key = null,
    Guid? BaleId = null,
    int? BaleNumber = null);

/// <summary>«قطعی کن» (Business).</summary>
public record FinalizeShipmentRequest(uint Version);

/// <summary>
/// نتیجهٔ «قطعی کن»: با کمبود بازدارنده <see cref="Ok"/> = false و فهرست کامل کمبودها (هیچ چیز عوض نشده)؛ وگرنه محمولهٔ
/// قطعی‌شده.
/// </summary>
public record FinalizeResultDto(bool Ok, IReadOnlyList<ReadinessIssueDto> Issues, ShipmentDto Shipment);

/// <summary>«باز کردن دوباره» (Business)؛ دلیل در تاریخچه ثبت می‌شود.</summary>
public record ReopenShipmentRequest(uint Version, string? Reason);

// ── نگله‌ها ──────────────────────────────────────────────────────────────

/// <summary>نگلهٔ تازه (Floor): بی شماره = بزرگ‌ترین + ۱؛ شمارهٔ تکراری ۴۰۹.</summary>
public record CreateBaleRequest(int? Number);

/// <summary>
/// ویرایش نگله (Floor): <see cref="Number"/> null یعنی همان شماره؛ وزن و یادداشت جایگزین می‌شوند (null یعنی پاک).
/// </summary>
public record UpdateBaleRequest(int? Number, decimal? WeightKg, string? Notes, uint Version);

/// <summary>شماره‌گذاری دوباره به ۱..n به ترتیب فعلی؛ <see cref="Version"/> نسخهٔ محموله.</summary>
public record RenumberBalesRequest(uint Version);
