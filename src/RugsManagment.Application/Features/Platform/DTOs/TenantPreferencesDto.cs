using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Platform.DTOs;

/// <summary>
/// ترجیحات کارگاه — پاسخ GET و بدنهٔ PUT در <c>/api/settings/preferences</c> (فقط Configure).
///
/// در PUT، <see cref="Version"/> همان xmin خوانده‌شده است و نسخهٔ کهنه ۴۰۹ می‌گیرد؛ <see cref="MoneyScaleLocked"/>
/// فقط خواندنی است و در نوشتن نادیده گرفته می‌شود. تغییر <see cref="MoneyScale"/> وقتی مبلغی ثبت شده ۴۰۹ است
/// (<c>Messages.MoneyLocked</c>). عضو آخر مقدار پیش‌فرض دارد چون دیرتر به قرارداد اضافه شد.
/// </summary>
/// <param name="SkuPrefix">پیشوند کد فرش (۱ تا ۱۲ حرف لاتین یا رقم)</param>
/// <param name="SerialMode">شمارهٔ فرش دستی، پیشنهادی یا خودکار</param>
/// <param name="SerialStart">کف شمارهٔ بعدی؛ null یعنی از ۱</param>
/// <param name="MoneyScale">تعداد رقم اعشار مبلغ‌ها (۰ تا ۲)</param>
/// <param name="DefaultUsdFx">نرخ پیش‌فرض دلار برای محمولهٔ تازه</param>
/// <param name="DefaultMethod">روش پیش‌فرض تقسیم هزینهٔ «کل»</param>
/// <param name="DefaultAreaBasis">پایهٔ متراژ پیش‌فرض هزینه‌ها</param>
/// <param name="DeclaredValueKeyField">کلید فیلدی که ارزش گمرکی بر اساسش خوانده می‌شود</param>
/// <param name="DeclaredValueFallbackUsd">ارزش گمرکی وقتی جدول نرخ ندارد</param>
/// <param name="DefaultBaleTargetAreaM2">متراژ هدف هر نگله</param>
/// <param name="BaleTargetTolerancePercent">تلورانس متراژ هدف (درصد)</param>
/// <param name="RequireFinalSizeToFinalize">بدون اندازهٔ نهایی همه، قطعی نشود</param>
/// <param name="BaleTerm">«نگله» یا «عدل»</param>
/// <param name="DefaultDeclarationTemplateId">قالب پیش‌فرض اظهارنامه</param>
/// <param name="ExporterName">نام صادرکننده</param>
/// <param name="ExporterCode">کد بازرگانی صادرکننده</param>
/// <param name="ExporterAddress">نشانی صادرکننده</param>
/// <param name="ExporterPhone">تلفن صادرکننده</param>
/// <param name="CustomsOffice">گمرک</param>
/// <param name="MoneyScaleLocked">آیا مبلغی ثبت شده و تعداد رقم اعشار دیگر عوض نمی‌شود (فقط خواندنی)</param>
/// <param name="Version">xmin ردیف (عدد JSON)</param>
/// <param name="MaxPlausibleSideM">ضلع بزرگ‌تر از این (متر) پرسش «متر یا سانت؟» می‌آورد</param>
public record TenantPreferencesDto(
    string SkuPrefix,
    SerialMode SerialMode,
    int? SerialStart,
    short MoneyScale,
    decimal? DefaultUsdFx,
    AllocationMethod DefaultMethod,
    AreaBasis DefaultAreaBasis,
    string DeclaredValueKeyField,
    decimal? DeclaredValueFallbackUsd,
    decimal? DefaultBaleTargetAreaM2,
    short BaleTargetTolerancePercent,
    bool RequireFinalSizeToFinalize,
    BaleTerm BaleTerm,
    Guid? DefaultDeclarationTemplateId,
    string? ExporterName,
    string? ExporterCode,
    string? ExporterAddress,
    string? ExporterPhone,
    string? CustomsOffice,
    bool MoneyScaleLocked,
    uint Version,
    decimal MaxPlausibleSideM = 12m);
