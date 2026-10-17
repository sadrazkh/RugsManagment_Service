using RugsManagment.Application.DTOs.Activity;

namespace RugsManagment.Application.Features.Platform.DTOs;

/// <summary>
/// پاسخ <c>GET /api/home/summary</c> (Floor) — کاشی‌ها و کارت‌های صفحهٔ خانه.
///
/// عضوهای پولی (<see cref="OpenShipmentCostPerM2"/>، <see cref="UnallocatedEntries"/>) برای کسی که Business
/// نیست null است. محموله‌ها و آمادگی از <see cref="IHomeShipmentSource"/> می‌آیند (تا فاز محموله خالی)، نه
/// از DTOهای فاز محموله، تا این قرارداد از همین فاز کامپایل شود (erratum E3).
/// </summary>
/// <param name="OpenShipments">همهٔ محموله‌های باز، تازه‌ترین فعالیتِ نگله اول</param>
/// <param name="Readiness">کمبودهای آمادگیِ اولین محمولهٔ باز</param>
/// <param name="StockPieces">تعداد تخته‌های انبار (فرش‌هایی که در هیچ محموله‌ای نیستند)</param>
/// <param name="StockAreaM2">متراژ انبار</param>
/// <param name="OpenShipmentCostPerM2">قیمت تمام‌شدهٔ هر متر اولین محمولهٔ باز (فقط Business؛ تا فاز هزینه null)</param>
/// <param name="UnallocatedEntries">تعداد هزینه‌های تقسیم‌نشده (فقط Business؛ تا فاز هزینه null)</param>
/// <param name="Recent">فعالیت‌های اخیر؛ برای غیر Business رویدادهای پولی (فروش، پرداخت، هزینه) حذف می‌شوند</param>
public record HomeSummaryDto(
    IReadOnlyList<HomeShipmentDto> OpenShipments,
    IReadOnlyList<HomeReadinessDto> Readiness,
    int StockPieces,
    decimal StockAreaM2,
    decimal? OpenShipmentCostPerM2,
    int? UnallocatedEntries,
    IReadOnlyList<ActivityEntryDto> Recent);

/// <summary>
/// یک محمولهٔ باز برای کاشی‌های خانه: «محمولهٔ ۸ · ۱۲ نگله · ۱۸۵ متر». دلار گمرکی پول داخلی نیست و برای
/// همه دیده می‌شود.
/// </summary>
/// <param name="Id">شناسهٔ محموله</param>
/// <param name="Number">شمارهٔ محموله</param>
/// <param name="Title">عنوان</param>
/// <param name="DocumentDate">تاریخ اظهار</param>
/// <param name="Bales">تعداد نگله‌ها</param>
/// <param name="Lines">تعداد ردیف‌ها</param>
/// <param name="Pieces">تعداد تخته‌ها (Σ تعداد)</param>
/// <param name="AreaM2">متراژ کل</param>
/// <param name="UsdTotal">ارزش گمرکی کل (دلار)</param>
/// <param name="ReadinessBlocking">تعداد کمبودهایی که جلوی قطعی شدن را می‌گیرند</param>
/// <param name="LastActivityAt">آخرین تغییر نگله‌ها (برای ترتیب)</param>
public record HomeShipmentDto(
    Guid Id,
    int Number,
    string Title,
    DateOnly DocumentDate,
    int Bales,
    int Lines,
    int Pieces,
    decimal AreaM2,
    decimal UsdTotal,
    int ReadinessBlocking,
    DateTimeOffset? LastActivityAt);

/// <summary>یک تراشهٔ آمادگی روی خانه (پیام فارسی از <c>Messages</c>).</summary>
/// <param name="Code">کد (مثل E_NO_FINAL_SIZE)</param>
/// <param name="Message">پیام فارسی پرشده</param>
/// <param name="Count">تعداد فرش‌های درگیر</param>
/// <param name="Blocking">جلوی قطعی شدن را می‌گیرد یا فقط هشدار است</param>
public record HomeReadinessDto(
    string Code,
    string Message,
    int Count,
    bool Blocking);
