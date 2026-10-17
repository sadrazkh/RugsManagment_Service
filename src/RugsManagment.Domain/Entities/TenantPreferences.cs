using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// ترجیحات کارگاه — یک ردیف برای هر کارگاه (کلید اصلی همان <see cref="TenantId"/>).
///
/// چرا جدولی جدا و نه ستون‌های بیشتر روی Tenant: Tenant را ادمین سامانه هم می‌نویسد و نسخهٔ هم‌زمانی
/// ندارد، ولی این‌ها را تنظیم‌کنندهٔ کارگاه از صفحهٔ ترجیحات ذخیره می‌کند و دو ذخیرهٔ هم‌زمان نباید
/// همدیگر را بی‌صدا پاک کنند؛ پس این جدول xmin دارد (پیکربندی در Infrastructure، نه این‌جا).
///
/// ردیف هنگام ساخت کارگاه و در هر استارت (برای کارگاه‌های قدیمی) با مقدارهای پیش‌فرض همین کلاس
/// ساخته می‌شود؛ پس خواننده‌ها می‌توانند فرض کنند همیشه هست.
/// </summary>
public class TenantPreferences : ITenantScoped
{
    /// <summary>بیشترین طول پیشوند کد فرش (varchar(12)).</summary>
    public const int SkuPrefixMaxLength = 12;

    /// <summary>پیش‌فرض پیشوند کد فرش، همان که پیش از این ثابت در کد بود.</summary>
    public const string DefaultSkuPrefix = "RUG";

    /// <summary>کمترین و بیشترین تعداد رقم اعشار مبلغ‌ها.</summary>
    public const short MinMoneyScale = 0;
    public const short MaxMoneyScale = 2;

    /// <summary>بیشترین طول کلید فیلد ارزش گمرکی (varchar(60)).</summary>
    public const int DeclaredValueKeyFieldMaxLength = 60;

    /// <summary>کلید فیلدی که جدول ارزش گمرکی بر اساسش نرخ می‌دهد — پیش‌فرض «محل بافت».</summary>
    public const string DefaultDeclaredValueKeyField = "rug.origin";

    /// <summary>اضلاع بزرگ‌تر از این (متر) پرسش «متر یا سانت؟» می‌آورند.</summary>
    public const decimal DefaultMaxPlausibleSideM = 12m;

    /// <summary>تلورانس پیش‌فرض متراژ هدف هر نگله (درصد).</summary>
    public const short DefaultBaleTargetTolerancePercent = 10;

    public Guid TenantId { get; set; }

    /// <summary>پیشوند کد فرش، مثل «RUG» در RUG-202609-0001.</summary>
    public string SkuPrefix { get; set; } = DefaultSkuPrefix;

    /// <summary>شمارهٔ فرش دستی، پیشنهادی یا خودکار باشد.</summary>
    public SerialMode SerialMode { get; set; } = SerialMode.SuggestNext;

    /// <summary>کف «شمارهٔ بعدی»: شمارهٔ پیشنهادی هرگز از این کمتر نمی‌شود (null یعنی از ۱).</summary>
    public int? SerialStart { get; set; }

    /// <summary>
    /// تعداد رقم اعشار مبلغ‌های تومان/ریال (۰ تا ۲؛ پیش‌فرض ۰). بعد از ثبت اولین مبلغ قفل می‌شود،
    /// چون همهٔ گردکردن‌های ذخیره‌شده به همین عدد بسته‌اند.
    /// </summary>
    public short MoneyScale { get; set; }

    /// <summary>ضلعِ بزرگ‌تر از این (متر) پرسش «متر یا سانت؟» می‌آورد (numeric(4,2)، پیش‌فرض ۱۲).</summary>
    public decimal MaxPlausibleSideM { get; set; } = DefaultMaxPlausibleSideM;

    /// <summary>نرخ پیش‌فرض دلار برای محمولهٔ تازه (numeric(18,6))؛ null یعنی خالی.</summary>
    public decimal? DefaultUsdFx { get; set; }

    /// <summary>روش پیش‌فرض تقسیم هزینهٔ «کل» بین فرش‌ها.</summary>
    public AllocationMethod DefaultMethod { get; set; } = AllocationMethod.ByArea;

    /// <summary>پایهٔ متراژ پیش‌فرض هزینه‌ها.</summary>
    public AreaBasis DefaultAreaBasis { get; set; } = AreaBasis.Effective;

    /// <summary>کلید فیلدی که ارزش گمرکی بر اساسش از جدول خوانده می‌شود (مثل «rug.origin»).</summary>
    public string DeclaredValueKeyField { get; set; } = DefaultDeclaredValueKeyField;

    /// <summary>ارزش گمرکی (دلار برای هر متر) وقتی جدول برای فرشی نرخ ندارد؛ null یعنی «ندارد».</summary>
    public decimal? DeclaredValueFallbackUsd { get; set; }

    /// <summary>
    /// مهر هم‌زمانیِ جدول ارزش گمرکی: هر جایگزینیِ کامل جدول یکی بالا می‌برد تا ذخیرهٔ کهنه ۴۰۹ بگیرد.
    /// </summary>
    public int DeclaredValuesStamp { get; set; }

    /// <summary>متراژ هدف هر نگله (numeric(6,2))؛ null یعنی بدون هدف.</summary>
    public decimal? DefaultBaleTargetAreaM2 { get; set; }

    /// <summary>چند درصد بالاتر از هدف هنوز «در محدوده» حساب شود.</summary>
    public short BaleTargetTolerancePercent { get; set; } = DefaultBaleTargetTolerancePercent;

    /// <summary>قطعی کردن محموله بدون اندازهٔ نهایی همهٔ فرش‌ها ممنوع باشد یا نه.</summary>
    public bool RequireFinalSizeToFinalize { get; set; } = true;

    /// <summary>«نگله» یا «عدل» — همهٔ متن‌ها و خروجی‌ها از همین می‌خوانند.</summary>
    public BaleTerm BaleTerm { get; set; } = BaleTerm.Negleh;

    /// <summary>قالب پیش‌فرض اظهارنامه (کلید خارجی در migration اسناد اضافه می‌شود).</summary>
    public Guid? DefaultDeclarationTemplateId { get; set; }

    // ── سربرگ صادرکننده (روی اظهارنامه) ─────────────────────────────────

    public string? ExporterName { get; set; }
    public string? ExporterCode { get; set; }
    public string? ExporterAddress { get; set; }
    public string? ExporterPhone { get; set; }
    public string? CustomsOffice { get; set; }

    /// <summary>ردیف پیش‌فرض یک کارگاه — همان چیزی که seed برای کارگاهِ بی‌ردیف می‌سازد.</summary>
    public static TenantPreferences CreateDefault(Guid tenantId) => new() { TenantId = tenantId };
}
