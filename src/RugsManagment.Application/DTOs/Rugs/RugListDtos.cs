using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.DTOs.Rugs;

/// <summary>
/// ردیف سبک فهرست فرش‌ها.
///
/// عمداً از <see cref="RugDto"/> جداست: فهرست نباید همهٔ مراحل هر فرش را لود کند.
/// همهٔ مقادیر (از جمله هزینه و مرحلهٔ جاری) مستقیم در SQL محاسبه می‌شوند.
///
/// <para><b>هشدار ساختن در EF:</b> این record داخل projection (<c>RugRepository.Project</c>) ساخته می‌شود و expression tree
/// آرگومان اختیاری حذف‌شده را نمی‌پذیرد (CS0854)؛ پس هر عضوی که به انتها اضافه می‌شود باید در همان تغییر صریحاً در
/// projection هم پاس شود (I.1). پول (<see cref="TotalInvestment"/>، <see cref="CostPerM2"/>) برای غیر Business null است.</para>
/// </summary>
public record RugListItemDto(
    Guid Id,
    string Sku,
    string? Title,
    string? Origin,
    string? Pattern,
    decimal WidthMeters,
    decimal LengthMeters,
    decimal AreaSquareMeters,
    RugStatus Status,
    /// <summary>بندانگشتیِ عکس شاخص؛ null یعنی فرش هنوز عکسی ندارد</summary>
    string? ThumbnailUrl,
    Guid? BatchId,
    string? BatchName,
    string? CurrentStepNameFa,
    /// <summary>شناسهٔ مرحلهٔ در حال انجام — برای دکمهٔ «مرحلهٔ بعد» در لیست؛ null یعنی مرحلهٔ فعالی نیست</summary>
    Guid? ActiveStepId,
    /// <summary>سرمایهٔ کل (خرید + مراحل)؛ برای غیر Business null</summary>
    decimal? TotalInvestment,
    /// <summary>مراحل تمام‌شده (تکمیل یا رد‌شده) — برای نوار پیشرفت بدون لود کردن خود مراحل</summary>
    int CompletedStepCount,
    int TotalStepCount,
    DateTimeOffset CreatedAt,
    // ── فاز ۲ (I.3): به همین ترتیب؛ فقط به انتها اضافه کنید و در projection صریح پاس دهید ──
    /// <summary>شمارهٔ بازرگان به شکل نمایشی؛ ستون اول فهرست. null یعنی بی‌شماره.</summary>
    string? SerialNo = null,
    /// <summary>تعداد تخته‌ها (دسته)؛ دست‌کم ۱.</summary>
    int Quantity = 1,
    /// <summary>مساحت کل ردیف = مساحت یک تخته × تعداد (ستون ذخیره‌شدهٔ <c>TotalAreaM2</c>).</summary>
    decimal TotalAreaM2 = 0,
    /// <summary>ردیف <c>RugCostTotal</c> دارد؛ false یعنی بها هنوز حساب نشده و <see cref="TotalInvestment"/> عدد واقعی نیست.</summary>
    bool CostComputed = false,
    /// <summary>بها برای هر متر مربع (<c>RugCostTotal.CostPerM2</c>)؛ برای غیر Business null.</summary>
    decimal? CostPerM2 = null,
    /// <summary>جای فرش (C.10)، در SQL حساب می‌شود.</summary>
    RugLocation Location = RugLocation.InStock,
    /// <summary>شمارهٔ نگله وقتی در محموله است (فاز محموله)؛ وگرنه null.</summary>
    int? BaleNumber = null,
    /// <summary>شمارهٔ محموله وقتی در نگله است؛ وگرنه null.</summary>
    int? ShipmentNumber = null,
    /// <summary>نسخهٔ ردیف (xmin) برای ویرایش از فهرست/جدول.</summary>
    uint Version = 0);

/// <summary>
/// ردیف سطل زباله — فرشی که حذف نرم شده و هنوز قابل بازگردانی است.
/// این هم در projection ساخته می‌شود؛ عضو تازه را همان‌جا صریح پاس دهید (CS0854).
/// </summary>
public record DeletedRugDto(
    Guid Id,
    string Sku,
    string? Title,
    string? Origin,
    decimal WidthMeters,
    decimal LengthMeters,
    RugStatus Status,
    decimal TotalInvestment,
    DateTimeOffset DeletedAt,
    /// <summary>نام حذف‌کننده؛ اگر کاربر بعداً حذف شده باشد null است</summary>
    string? DeletedByName,
    // ── فاز ۲ ──────────────────────────────────────────────────────────────
    /// <summary>
    /// شمارهٔ بازرگان؛ کاربر فرش را با همین می‌شناسد، و بازگرداندن وقتی شماره حالا مال فرش دیگری است رد می‌شود.
    /// </summary>
    string? SerialNo = null,
    /// <summary>تعداد تخته‌ها.</summary>
    int Quantity = 1);

/// <summary>ستون‌های قابل مرتب‌سازی در فهرست فرش‌ها.</summary>
public enum RugSortBy
{
    CreatedAt = 0,
    Sku = 1,
    Title = 2,
    Area = 3,

    /// <summary>برای غیر Business به <see cref="CreatedAt"/> برمی‌گردد تا ترتیب، هزینه را لو ندهد.</summary>
    TotalCost = 4,
    Status = 5,
    CurrentStep = 6,

    /// <summary>
    /// شمارهٔ بازرگان: عددی بر اساس <c>SerialSort</c> (اولِ بازه)، شماره‌های متنی («A-12») و بی‌شماره‌ها همیشه آخر
    /// (در هر دو جهت)، بعد خودِ <c>SerialNo</c> و بعد Id. «3911 - 3960» پیش از «3961» می‌آید، نه بر اساس متن.
    /// </summary>
    Serial = 7
}

/// <summary>
/// فیلتر/مرتب‌سازی/صفحه‌بندی فهرست فرش‌ها.
/// مقادیر مستقیماً از query string صفحه bind می‌شوند تا آدرس قابل اشتراک و bookmark باشد.
/// </summary>
public record RugQuery
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    /// <summary>
    /// جستجو در شمارهٔ بازرگان (<c>SerialNo</c>)، کد، عنوان، اصالت، طرح، جنس و یادداشت («شامل»). برای «دقیقاً این شماره،
    /// یا عددی داخل یک دسته» از <see cref="Serial"/> استفاده کنید.
    /// </summary>
    public string? Search { get; init; }

    public RugStatus? Status { get; init; }
    public Guid? BatchId { get; init; }

    /// <summary>فقط فرش‌هایی که به هیچ گروهی تعلق ندارند (برای انتخابگر «افزودن به گروه»)</summary>
    public bool WithoutBatch { get; init; }

    /// <summary>فقط فرش‌هایی که همین حالا روی این نوع مرحله هستند</summary>
    public Guid? StepTypeId { get; init; }

    public DateTimeOffset? CreatedFrom { get; init; }
    public DateTimeOffset? CreatedTo { get; init; }

    // ── فاز ۲: همه این‌جا اعلام شده‌اند؛ فیلترهای محموله را فاز محموله اجرا می‌کند ──

    /// <summary>
    /// فقط فرش‌هایی که خطی در این محموله دارند. پیش از فاز محموله (جدول خط نیست) هیچ فرشی در محموله نیست، پس نتیجه خالی است.
    /// </summary>
    public Guid? ShipmentId { get; init; }

    /// <summary>فقط فرش‌های این نگله. پیش از فاز محموله نتیجه خالی است.</summary>
    public Guid? BaleId { get; init; }

    /// <summary>فقط فرش‌هایی که در هیچ نگله‌ای نیستند (انتخابگر «از انبار»). پیش از فاز محموله همهٔ فرش‌ها.</summary>
    public bool NotBaled { get; init; }

    /// <summary>
    /// فقط فرش‌های این جا (C.10، چیپ‌های «در انبار»، «در نگله»، «صادر شده»، «فروخته‌شده»). <see cref="RugLocation.Trashed"/>
    /// همیشه نتیجهٔ خالی می‌دهد، چون فهرست فرش‌های حذف‌شده را نمی‌بیند؛ سطل زباله صفحهٔ خودش را دارد.
    /// </summary>
    public RugLocation? Location { get; init; }

    /// <summary>
    /// شماره، تطبیق دقیق و نه «شامل» (همان قاعدهٔ <c>/api/rugs/by-serial</c>): عدد تک → فرشی که شماره‌اش همین است یا
    /// دسته‌ای که این عدد داخلش است؛ دسته («3911 - 3960») → فرش‌هایی که بازه‌شان با آن همپوشانی دارد؛ شمارهٔ متنی →
    /// همان متن. متنی که شمارهٔ معتبر نیست هیچ نتیجه‌ای نمی‌دهد (خطا نمی‌دهد، چون از آدرس صفحه می‌آید).
    /// </summary>
    public string? Serial { get; init; }

    public RugSortBy SortBy { get; init; } = RugSortBy.CreatedAt;
    public bool Descending { get; init; } = true;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary>
    /// ورودی را امن و قابل‌استفاده می‌کند: صفحه و اندازهٔ صفحه در بازهٔ معتبر،
    /// جستجوی خالی به null، و تاریخ‌ها به UTC.
    ///
    /// تبدیل به UTC ضروری است چون PostgreSQL برای ستون timestamptz فقط offset صفر
    /// می‌پذیرد؛ model binder اما تاریخِ بدون ساعت را با offset محلیِ سرور می‌سازد.
    /// </summary>
    public RugQuery Sanitized() => this with
    {
        Page = Page < 1 ? 1 : Page,
        PageSize = PageSize < 5 ? DefaultPageSize : Math.Min(PageSize, MaxPageSize),
        Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim(),
        Serial = string.IsNullOrWhiteSpace(Serial) ? null : Serial.Trim(),
        CreatedFrom = CreatedFrom?.ToUniversalTime(),
        CreatedTo = CreatedTo?.ToUniversalTime()
    };

    /// <summary>آیا فیلتری غیر از مرتب‌سازی/صفحه فعال است؟ (برای نمایش دکمهٔ «حذف فیلترها»)</summary>
    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(Search)
        || Status.HasValue
        || BatchId.HasValue
        || WithoutBatch
        || StepTypeId.HasValue
        || CreatedFrom.HasValue
        || CreatedTo.HasValue
        || ShipmentId.HasValue
        || BaleId.HasValue
        || NotBaled
        || Location.HasValue
        || !string.IsNullOrWhiteSpace(Serial);
}
