namespace RugsManagment.Domain.Enums;

/// <summary>
/// نوع رویداد در تاریخچهٔ فعالیت.
///
/// عمداً محدود و معنایی است (نه «Insert/Update/Delete» خام) تا فهرست فعالیت
/// برای کاربر کارگاه خوانا باشد و بشود رویش فیلتر گذاشت.
///
/// همهٔ مقدارهای فازهای بعد (محموله، نگله، هزینه، قالب خروجی، ورود از اکسل) همین‌جا و یک‌جا
/// اضافه شده‌اند تا برچسب‌های فهرست فعالیت و فیلتر صفحهٔ تاریخچه فقط یک بار ویرایش شوند.
/// فقط افزودنی با عدد صریح؛ عدد هر رویداد در جدول AuditEntries نشسته و هرگز جابه‌جا نمی‌شود.
/// </summary>
public enum AuditAction
{
    Created = 0,
    Updated = 1,
    Deleted = 2,

    /// <summary>بازگردانی از سطل زباله (حذف نرم برگشت خورد)</summary>
    Restored = 3,

    /// <summary>مرحله تکمیل و فرش به مرحلهٔ بعد رفت</summary>
    StepAdvanced = 10,

    /// <summary>بازگشت به مرحلهٔ قبل</summary>
    StepReverted = 11,

    /// <summary>مرحلهٔ اختیاری رد شد</summary>
    StepSkipped = 12,

    /// <summary>مسیر گردش کار فرش تغییر کرد</summary>
    WorkflowChanged = 13,

    SaleRecorded = 20,
    SaleCancelled = 21,

    /// <summary>پرداخت به طرف خدمات</summary>
    ProviderPaid = 30,

    UserInvited = 40,
    UserDisabled = 41,
    PasswordChanged = 42,
    SettingsChanged = 50,

    // ── سکو: ترجیحات، بستهٔ آماده، چیدمان جدول‌ها ─────────────────────────

    /// <summary>ترجیحات کارگاه (شماره‌گذاری، نگله، صادرکننده، …) عوض شد</summary>
    PreferencesChanged = 60,

    /// <summary>«بستهٔ صادرات فرش» روی کارگاه اعمال شد</summary>
    PresetApplied = 61,

    /// <summary>ستون‌های یکی از جدول‌ها (ورود، نگله، انبار) عوض شد</summary>
    GridLayoutChanged = 62,

    // ── فرش ─────────────────────────────────────────────────────────────

    /// <summary>یک دسته به دو دسته جدا شد</summary>
    RugSplit = 70,

    /// <summary>اندازهٔ نهایی ثبت یا پاک شد</summary>
    FinalSizeRecorded = 71,

    // ── محموله و نگله ───────────────────────────────────────────────────

    ShipmentCreated = 80,
    ShipmentUpdated = 81,
    ShipmentDeleted = 82,

    /// <summary>محموله قطعی شد (فرش‌ها «صادر شده» شدند)</summary>
    ShipmentFinalized = 83,

    /// <summary>محمولهٔ قطعی دوباره باز شد</summary>
    ShipmentReopened = 84,

    BaleCreated = 85,
    BaleDeleted = 86,
    BalesRenumbered = 87,

    /// <summary>فرش به نگله اضافه، جابه‌جا یا از آن خارج شد</summary>
    LinesChanged = 88,

    /// <summary>جدول ارزش گمرکی (دلار برای هر متر) عوض شد</summary>
    DeclaredValuesChanged = 89,

    // ── هزینه ───────────────────────────────────────────────────────────

    CostEntryCreated = 90,
    CostEntryUpdated = 91,
    CostEntryDeleted = 92,
    CostEntryRestored = 93,
    CostCategoryChanged = 94,

    /// <summary>فروش (درآمد) یک محموله ثبت یا عوض شد</summary>
    RevenueChanged = 95,

    // ── قالب خروجی و خروجی گرفتن ─────────────────────────────────────────

    TemplateCreated = 100,
    TemplateUpdated = 101,
    TemplateDeleted = 102,
    TemplateRestored = 103,

    /// <summary>اکسل یا چاپ گرفته شد</summary>
    Exported = 104,

    // ── ورود از اکسل ────────────────────────────────────────────────────

    ImportCommitted = 110,
    ImportUndone = 111,

    // ── نگهداری ─────────────────────────────────────────────────────────

    /// <summary>حساب‌های هزینه از نو ساخته شد (ابزار سلامت حساب‌ها)</summary>
    LedgerRebuilt = 120
}
