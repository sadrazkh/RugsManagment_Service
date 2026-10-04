using RugsManagment.Domain.Enums;

namespace RugsManagment.Web.Support;

/// <summary>
/// کمک‌کننده‌های نمایش مشترک برای viewها — برچسب وضعیت، قالب مبلغ و عدد.
/// قالب‌بندی واقعی (ارقام فارسی/تاریخ شمسی) در <see cref="PersianFormat"/> است.
///
/// برچسب وضعیت فرش فقط همین‌جا (و آینه‌اش در <c>CA/lib/labels.ts</c>) نوشته می‌شود. کلاس‌های Tailwind این فایل
/// از C# برمی‌گردند و چون <c>Web/Support/**</c> منبع Tailwind است ساخته می‌شوند.
/// </summary>
public static class DisplayHelpers
{
    private const string NeutralChip = "bg-surface-container-high text-on-surface-variant";

    /// <summary>مبلغ با جداکنندهٔ هزارگان و ارقام فارسی؛ «—» برای null.</summary>
    public static string Money(decimal? value) => PersianFormat.Money(value);

    /// <summary>مبلغ همراه واحد «تومان» — برای جاهایی که واحد از context معلوم نیست.</summary>
    public static string MoneyWithUnit(decimal? value) => PersianFormat.MoneyWithUnit(value);

    /// <summary>عدد اعشاری کوتاه با ارقام فارسی (ابعاد، مساحت).</summary>
    public static string Number(decimal value) => PersianFormat.Number(value);

    /// <summary>شمارنده با ارقام فارسی.</summary>
    public static string Count(int value) => PersianFormat.Count(value);

    /// <summary>
    /// ترتیب نمایش وضعیت‌ها در فیلترها و آمار. «ثبت‌شده» اول است چون فرش تازه با همین وضعیت می‌آید.
    /// </summary>
    public static IReadOnlyList<RugStatus> RugStatusOrder { get; } =
    [
        Domain.Enums.RugStatus.Draft,
        Domain.Enums.RugStatus.InProgress,
        Domain.Enums.RugStatus.ReadyForSale,
        Domain.Enums.RugStatus.Sold,
        Domain.Enums.RugStatus.Exported,
        Domain.Enums.RugStatus.Archived
    ];

    /// <summary>
    /// وضعیت فرش در نمای کامل: برچسب فارسی + کلاس‌های رنگ چیپ (سازگار با حالت تاریک) + نماد.
    ///
    /// «پیش‌نویس» فقط مال محموله و خروجی است، پس فرش تازه «ثبت‌شده» است؛ «در کارگاه» فقط در نمای کامل معنا
    /// دارد (در نمای ساده <see cref="SimpleRugStatus"/>).
    /// </summary>
    public static (string Label, string Css, string Icon) RugStatus(RugStatus status) => status switch
    {
        Domain.Enums.RugStatus.Draft => ("ثبت‌شده", NeutralChip, "edit"),
        Domain.Enums.RugStatus.InProgress => ("در کارگاه", "bg-secondary-container text-on-secondary-container", "workflow"),
        Domain.Enums.RugStatus.ReadyForSale => ("آمادهٔ فروش", "bg-success/12 text-success", "success"),
        Domain.Enums.RugStatus.Sold => ("فروخته‌شده", "bg-primary/12 text-primary", "check"),
        Domain.Enums.RugStatus.Archived => ("بایگانی", NeutralChip, "package"),
        Domain.Enums.RugStatus.Exported => ("صادر شده", "bg-tertiary/12 text-tertiary", "truck"),
        _ => ("نامشخص", NeutralChip, "info")
    };

    /// <summary>وضعیت از نام انگلیسی enum (مثل «InProgress» در DTOهای داشبورد).</summary>
    public static (string Label, string Css, string Icon) RugStatus(string? statusName)
        => Enum.TryParse<RugStatus>(statusName, ignoreCase: false, out var status) && Enum.IsDefined(status)
            ? RugStatus(status)
            : ("نامشخص", NeutralChip, "info");

    /// <summary>
    /// وضعیت فرش برای نمای ساده: بازرگان مرحله‌های کارگاه را نمی‌بیند، فقط اینکه فرش کجاست — «در انبار»،
    /// «فروخته‌شده»، «صادر شده» یا «بایگانی». تا وقتی محل واقعی فرش (در نگله) در فاز محموله بیاید، هر فرش
    /// فروخته‌نشده و صادرنشده در انبار است.
    /// </summary>
    public static (string Label, string Css, string Icon) SimpleRugStatus(RugStatus status) => status switch
    {
        Domain.Enums.RugStatus.Sold or Domain.Enums.RugStatus.Exported or Domain.Enums.RugStatus.Archived
            => RugStatus(status),
        _ => ("در انبار", NeutralChip, "warehouse")
    };

    /// <summary>وضعیت برای حالت مؤثر پوسته: ساده یا کامل.</summary>
    public static (string Label, string Css, string Icon) RugStatus(RugStatus status, bool simple)
        => simple ? SimpleRugStatus(status) : RugStatus(status);

    /// <summary>همان <see cref="RugStatus(RugStatus, bool)"/> از نام انگلیسی enum (DTOهای داشبورد).</summary>
    public static (string Label, string Css, string Icon) RugStatus(string? statusName, bool simple)
        => Enum.TryParse<RugStatus>(statusName, ignoreCase: false, out var status) && Enum.IsDefined(status)
            ? RugStatus(status, simple)
            : ("نامشخص", NeutralChip, "info");

    /// <summary>نحوهٔ پرداخت در فروش.</summary>
    public static string PaymentMethod(SalePaymentMethod method) => method switch
    {
        SalePaymentMethod.Cash => "نقدی",
        SalePaymentMethod.Card => "کارت",
        SalePaymentMethod.Transfer => "حواله",
        SalePaymentMethod.Cheque => "چک",
        SalePaymentMethod.Installment => "اقساطی",
        _ => method.ToString()
    };

    /// <summary>وضعیت یک مرحله روی فرش.</summary>
    public static (string Label, string Css, string Icon) StepStatus(WorkflowStepStatus status) => status switch
    {
        WorkflowStepStatus.Pending => ("در صف", NeutralChip, "info"),
        WorkflowStepStatus.InProgress => ("در حال انجام", "bg-secondary-container text-on-secondary-container", "workflow"),
        WorkflowStepStatus.Completed => ("تکمیل‌شده", "bg-success/12 text-success", "success"),
        WorkflowStepStatus.Skipped => ("رد‌شده", "bg-surface-container-high text-on-surface-variant line-through", "close"),
        WorkflowStepStatus.Cancelled => ("لغو‌شده", "bg-error-container text-error", "error"),
        _ => (status.ToString(), NeutralChip, "info")
    };
}
