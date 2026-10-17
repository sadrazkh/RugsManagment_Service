using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.DTOs.Costs;

// قرارداد دفتر «هزینه‌ها» (spec I.3 «Costs») — آینهٔ TS در CA/lib/types/costs.ts. رکوردهای موضعی؛ عضو تازه فقط به انتها و
// با مقدار پیش‌فرض اضافه می‌شود (I.1). همهٔ مبلغ‌ها به ارز کارگاه‌اند مگر نامشان «Original» باشد. همهٔ این‌ها فقط برای
// Business است (I.2: /api/costs*)، پس پنهان‌سازی پول لازم ندارند.

/// <summary>
/// <c>POST /api/costs</c>، <c>PUT /api/costs/{id}</c>، <c>POST /api/costs/preview</c> (I.3). پنل ساده همین را با پیش‌فرض‌های نوع
/// هزینه پر می‌فرستد (H.2): <see cref="AreaBasis"/> را کاربر ساده هرگز نمی‌بیند.
/// </summary>
/// <param name="CategoryId">نوع هزینه.</param>
/// <param name="Date">تاریخ (روز ایران؛ پیش‌فرض امروز).</param>
/// <param name="Description">«توضیح».</param>
/// <param name="ReceiptNo">«شمارهٔ رسید»؛ ثبت متری/دانه‌ای را منجمد می‌کند (D.3).</param>
/// <param name="ServiceProviderId">«کارگاه / طرف حساب»؛ ثبت متری/دانه‌ای را منجمد می‌کند و در صورت‌حساب او یک ردیف رسید است.</param>
/// <param name="Basis">«هر متر | هر تخته | کل» (Percent ممنوع است).</param>
/// <param name="Rate">نرخ برای «هر متر»/«هر تخته» در <paramref name="Currency"/>.</param>
/// <param name="Amount">مبلغ «کل» در <paramref name="Currency"/>.</param>
/// <param name="Currency">ارز مبلغ (تومان، ریال یا دلار).</param>
/// <param name="FxRate">نرخ دلار؛ برای مبلغ دلاری الزامی (<c>E_FX_REQUIRED</c>).</param>
/// <param name="AreaBasis">پایهٔ متراژ (اندازهٔ اول یا مؤثر).</param>
/// <param name="Method">روش تقسیم «کل».</param>
/// <param name="Scope">برای کدام فرش‌ها.</param>
/// <param name="ShipmentId">«این محموله».</param>
/// <param name="BatchId">«این ورودی».</param>
/// <param name="RugIds">فرش‌های «انتخاب» (از resolve-targets یا انتخاب در فهرست).</param>
/// <param name="BaleIds">«این نگله‌ها».</param>
/// <param name="ScopeNote">این فهرست چطور ساخته شد («۲۴۲ فرش انبار در ۱۴۰۵/۰۶/۱۵»).</param>
/// <param name="Pins">«مبلغ ثابت برای یک فرش» به ارز کارگاه.</param>
/// <param name="Version">نسخهٔ ثبت برای PUT (xmin)؛ در POST نادیده.</param>
/// <param name="ClientKey">کلید یکتای کلاینت؛ POST با همان کلید همان ثبت را برمی‌گرداند (تلاش دوبارهٔ پنل).</param>
public record SaveCostEntryRequest(
    Guid CategoryId,
    DateOnly Date,
    string? Description,
    string? ReceiptNo,
    Guid? ServiceProviderId,
    CostBasis Basis,
    decimal? Rate,
    decimal? Amount,
    CurrencyUnit Currency,
    decimal? FxRate,
    AreaBasis AreaBasis,
    AllocationMethod Method,
    CostScope Scope,
    Guid? ShipmentId,
    Guid? BatchId,
    IReadOnlyList<Guid>? RugIds,
    IReadOnlyList<Guid>? BaleIds,
    string? ScopeNote,
    IReadOnlyList<PinDto>? Pins,
    uint? Version,
    Guid? ClientKey);

/// <summary>مبلغ ثابت یک فرش، به ارز کارگاه (D.3 «Pins»).</summary>
public record PinDto(Guid RugId, decimal Amount);

/// <summary>یک هشدار تقسیم (D.8) با پیام فارسی ساخته‌شده و شمار.</summary>
/// <param name="Code">کد ثابت (<c>W_PIN_DROPPED</c>، …).</param>
/// <param name="Message">متن فارسی با نشانه‌های پرشده.</param>
/// <param name="Count">چند فرش (یا ۱ برای هشدارهای کل ثبت).</param>
public record WarningDto(string Code, string Message, int Count);

/// <summary>سهم یک فرش در پیش‌نمایش یا نتیجهٔ تقسیم.</summary>
/// <param name="RugId">فرش.</param>
/// <param name="Serial">شمارهٔ بازرگان (یا کد انبار وقتی شماره ندارد).</param>
/// <param name="WeightUnits">وزن (صدم متر، تخته، سنت، ۱ برای مساوی؛ برای مبلغ ثابت ۰).</param>
/// <param name="WeightUnit">واحد وزن: <see cref="ShareWeightUnits"/>.</param>
/// <param name="Amount">سهم به ارز کارگاه.</param>
/// <param name="IsPinned">مبلغ ثابت است.</param>
/// <param name="PerPiece">سهم یک تخته برای دسته (ثبت زندهٔ متری/دانه‌ای: مبلغ تخته پیش از × تعداد؛ بقیه: سهم ÷ تعداد)؛ برای تک‌تخته null.</param>
public record ShareDto(Guid RugId, string? Serial, long WeightUnits, string WeightUnit, decimal Amount, bool IsPinned, decimal? PerPiece);

/// <summary>واحدهای وزن <see cref="ShareDto.WeightUnit"/> (رشته، تا TS همان را نشان دهد).</summary>
public static class ShareWeightUnits
{
    /// <summary>صدم متر مربع (به نسبت متراژ، رسید متری).</summary>
    public const string AreaHundredths = "m2x100";

    /// <summary>تخته (به تعداد، رسید دانه‌ای).</summary>
    public const string Pieces = "pieces";

    /// <summary>یک برای هر ردیف (مساوی، یا وقتی همهٔ وزن‌ها صفر بود).</summary>
    public const string Equal = "equal";

    /// <summary>سنت دلار اظهاری.</summary>
    public const string UsdCents = "usdCents";

    /// <summary>صدم واحد قیمت خرید.</summary>
    public const string PurchaseCents = "purchaseCents";

    /// <summary>مبلغ مستقیم (ثبت زندهٔ متری/دانه‌ای، یا مبلغ ثابت).</summary>
    public const string Amount = "amount";
}

/// <summary>
/// نتیجه یا پیش‌نمایش تقسیم (I.3). <see cref="Sentence"/> همان جملهٔ پنل ساده است (H.2)، مثل
/// «۸۵٬۰۰۰ تومان برای هر متر × ۲۰٫۸۱ متر = ۱٬۷۶۸٬۸۵۰ تومان».
/// </summary>
/// <param name="OriginalAmount">کل در ارز ثبت (برای «کل» همان مبلغ؛ برای متری/دانه‌ای Σ در ارز ثبت).</param>
/// <param name="BaseAmount">کل به ارز کارگاه که تقسیم شد.</param>
/// <param name="Lines">شمار ردیف‌های عضو.</param>
/// <param name="Pieces">جمع تخته‌ها.</param>
/// <param name="AreaM2">جمع متراژ در پایهٔ متراژ ثبت.</param>
/// <param name="PerM2">کل ÷ متراژ (گرد به MoneyScale)؛ null وقتی متراژ صفر است.</param>
/// <param name="State">وضع تقسیم.</param>
/// <param name="Unallocated">باقی‌ماندهٔ بی‌صاحب.</param>
/// <param name="Warnings">هشدارها (D.8).</param>
/// <param name="Shares">سهم هر فرش، به ترتیب شماره.</param>
/// <param name="Sentence">جملهٔ فارسی پنل.</param>
public record AllocationPreviewDto(
    decimal OriginalAmount,
    decimal BaseAmount,
    int Lines,
    int Pieces,
    decimal AreaM2,
    decimal? PerM2,
    AllocationState State,
    decimal Unallocated,
    IReadOnlyList<WarningDto> Warnings,
    IReadOnlyList<ShareDto> Shares,
    string Sentence);

/// <summary>
/// یک ثبت کامل (I.3): درخواستِ ذخیره‌شده (برای پر کردن دوبارهٔ پنل) و نتیجهٔ آخرین تقسیم. عضوهای افزوده بعد از
/// <paramref name="CreatedAt"/> برای نمایش‌اند.
/// </summary>
/// <param name="Id">شناسه.</param>
/// <param name="Number">شمارهٔ ثبت.</param>
/// <param name="Request">مقدارهای ذخیره‌شده به شکل درخواست (نسخه همان <paramref name="Version"/>).</param>
/// <param name="Version">نسخه (xmin) برای PUT و DELETE.</param>
/// <param name="Result">نتیجهٔ تقسیم از ردیف‌های مشتق‌شده.</param>
/// <param name="CreatedAt">زمان ثبت.</param>
/// <param name="CategoryName">نام نوع هزینه.</param>
/// <param name="Group">گروه نوع (خرید / هزینه).</param>
/// <param name="FrozenAt">زمان انجماد رسید متری/دانه‌ای؛ null یعنی زنده یا «کل».</param>
/// <param name="Deleted">حذف نرم شده.</param>
/// <param name="ServiceProviderName">نام طرف خدمات.</param>
/// <param name="CategoryIcon">آیکون نوع هزینه.</param>
public record CostEntryDto(
    Guid Id,
    int Number,
    SaveCostEntryRequest Request,
    uint Version,
    AllocationPreviewDto Result,
    DateTimeOffset CreatedAt,
    string? CategoryName = null,
    CostGroup Group = CostGroup.Expense,
    DateTimeOffset? FrozenAt = null,
    bool Deleted = false,
    string? ServiceProviderName = null,
    string? CategoryIcon = null);

/// <summary>یک ردیف فهرست «هزینه‌ها» (I.3).</summary>
/// <param name="Id">شناسه.</param>
/// <param name="Number">شمارهٔ ثبت.</param>
/// <param name="Date">تاریخ.</param>
/// <param name="Category">نام نوع هزینه.</param>
/// <param name="Group">گروه.</param>
/// <param name="Description">توضیح.</param>
/// <param name="TargetsText">«برای»: «محمولهٔ ۸ (۲۴۲ تخته)»، «نگلهٔ ۳»، «۱۲ فرش ورودی ۳»، …</param>
/// <param name="BaseAmount">کل به ارز کارگاه (برای ثبت زنده کلِ امروز).</param>
/// <param name="OriginalAmount">کل در ارز ثبت وقتی ارز ثبت با کارگاه فرق دارد؛ وگرنه null.</param>
/// <param name="Currency">ارز ثبت.</param>
/// <param name="State">وضع تقسیم.</param>
/// <param name="Unallocated">باقی‌ماندهٔ بی‌صاحب.</param>
/// <param name="Deleted">حذف نرم شده.</param>
/// <param name="Version">نسخه برای حذف از فهرست.</param>
/// <param name="CategoryIcon">آیکون نوع.</param>
/// <param name="Basis">مبنای ثبت.</param>
/// <param name="Frozen">رسید منجمد است.</param>
/// <param name="WarningCodes">کدهای هشدار آخرین تقسیم (برای تراشه‌ها).</param>
public record CostEntryListItemDto(
    Guid Id,
    int Number,
    DateOnly Date,
    string Category,
    CostGroup Group,
    string? Description,
    string TargetsText,
    decimal BaseAmount,
    decimal? OriginalAmount,
    CurrencyUnit Currency,
    AllocationState State,
    decimal Unallocated,
    bool Deleted,
    uint Version = 0,
    string? CategoryIcon = null,
    CostBasis Basis = CostBasis.Total,
    bool Frozen = false,
    IReadOnlyList<string>? WarningCodes = null);

/// <summary>
/// فیلتر فهرست «هزینه‌ها» (<c>GET /api/costs?shipmentId&amp;batchId&amp;categoryId&amp;state&amp;from&amp;to&amp;page</c>).
/// <see cref="ShipmentId"/> ثبت‌هایی را می‌دهد که به آن محموله وصل‌اند یا فرشی از آن را دارند.
/// </summary>
public record CostEntryQuery
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    public Guid? ShipmentId { get; init; }
    public Guid? BatchId { get; init; }
    public Guid? CategoryId { get; init; }

    /// <summary>فقط این وضع (تراشهٔ «تقسیم‌نشده» = <see cref="AllocationState.Unallocated"/> و <see cref="AllocationState.Partial"/> با <see cref="NotFullyAllocated"/>).</summary>
    public AllocationState? State { get; init; }

    /// <summary>«تقسیم‌نشده»: هر ثبتی که همهٔ مبلغش تقسیم نشده (Partial یا Unallocated).</summary>
    public bool NotFullyAllocated { get; init; }

    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }

    /// <summary>ثبت‌های حذف‌شده هم بیایند (برای «برگردان»).</summary>
    public bool IncludeDeleted { get; init; }

    /// <summary>فقط ثبت‌هایی که این فرش را دارند (صفحهٔ فرش).</summary>
    public Guid? RugId { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary>صفحه و اندازهٔ صفحه در بازهٔ مجاز.</summary>
    public CostEntryQuery Sanitized() => this with
    {
        Page = Math.Max(1, Page),
        PageSize = PageSize <= 0 ? DefaultPageSize : Math.Min(PageSize, MaxPageSize)
    };
}

/// <summary>
/// <c>POST /api/costs/resolve-targets</c> (I.2): متن شماره‌ها («3778، 3780-3790»)، یک فیلتر فهرست فرش‌ها یا «همهٔ انبار»
/// <b>هنگام ذخیره</b> به فهرست صریح فرش تبدیل می‌شود؛ هیچ‌وقت دامنهٔ «فیلتر زنده» نیست (D.2).
/// </summary>
/// <param name="SerialsText">شماره‌ها با «،» یا «,» یا فاصله؛ بازه با «-» یا «تا».</param>
/// <param name="Filter">فیلتر فهرست فرش‌ها.</param>
/// <param name="InStock">«همهٔ انبار»: فرش‌های فعالِ نه فروخته و نه در نگله.</param>
public record ResolveTargetsRequest(string? SerialsText, RugQuery? Filter, bool? InStock);

/// <summary>نتیجهٔ <see cref="ResolveTargetsRequest"/>.</summary>
/// <param name="RugIds">فرش‌های پیداشده (یکتا، به ترتیب شماره).</param>
/// <param name="Lines">شمار ردیف‌ها.</param>
/// <param name="Pieces">جمع تخته‌ها.</param>
/// <param name="AreaM2">جمع متراژ مؤثر.</param>
/// <param name="Unresolved">تکه‌هایی از متن که فرشی برایشان پیدا نشد یا درست نبودند.</param>
/// <param name="ScopeNote">«۲۴۲ فرش انبار در ۱۴۰۵/۰۶/۱۵».</param>
public record ResolveTargetsResultDto(
    IReadOnlyList<Guid> RugIds,
    int Lines,
    int Pieces,
    decimal AreaM2,
    IReadOnlyList<string> Unresolved,
    string ScopeNote);
