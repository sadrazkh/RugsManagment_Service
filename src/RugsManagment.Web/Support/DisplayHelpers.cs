using RugsManagment.Domain.Enums;

namespace RugsManagment.Web.Support;

/// <summary>
/// کمک‌کننده‌های نمایش مشترک برای viewها — برچسب وضعیت، قالب مبلغ و عدد.
/// قالب‌بندی واقعی (ارقام فارسی/تاریخ شمسی) در <see cref="PersianFormat"/> است.
/// </summary>
public static class DisplayHelpers
{
    /// <summary>مبلغ با جداکنندهٔ هزارگان و ارقام فارسی؛ «—» برای null.</summary>
    public static string Money(decimal? value) => PersianFormat.Money(value);

    /// <summary>مبلغ همراه واحد «تومان» — برای جاهایی که واحد از context معلوم نیست.</summary>
    public static string MoneyWithUnit(decimal? value) => PersianFormat.MoneyWithUnit(value);

    /// <summary>عدد اعشاری کوتاه با ارقام فارسی (ابعاد، مساحت).</summary>
    public static string Number(decimal value) => PersianFormat.Number(value);

    /// <summary>شمارنده با ارقام فارسی.</summary>
    public static string Count(int value) => PersianFormat.Count(value);

    /// <summary>وضعیت فرش: برچسب فارسی + کلاس‌های رنگ چیپ (سازگار با حالت تاریک).</summary>
    public static (string Label, string Css, string Icon) RugStatus(RugStatus status) => status switch
    {
        Domain.Enums.RugStatus.Draft => ("پیش‌نویس", "bg-surface-container-high text-on-surface-variant", "edit"),
        Domain.Enums.RugStatus.InProgress => ("در جریان", "bg-secondary-container text-on-secondary-container", "workflow"),
        Domain.Enums.RugStatus.ReadyForSale => ("آمادهٔ فروش", "bg-success/12 text-success", "success"),
        Domain.Enums.RugStatus.Sold => ("فروخته‌شده", "bg-primary/12 text-primary", "check"),
        Domain.Enums.RugStatus.Archived => ("بایگانی", "bg-surface-container-high text-on-surface-variant", "package"),
        _ => (status.ToString(), "bg-surface-container-high text-on-surface-variant", "info")
    };

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
        WorkflowStepStatus.Pending => ("در صف", "bg-surface-container-high text-on-surface-variant", "info"),
        WorkflowStepStatus.InProgress => ("در حال انجام", "bg-secondary-container text-on-secondary-container", "workflow"),
        WorkflowStepStatus.Completed => ("تکمیل‌شده", "bg-success/12 text-success", "success"),
        WorkflowStepStatus.Skipped => ("رد‌شده", "bg-surface-container-high text-on-surface-variant line-through", "close"),
        WorkflowStepStatus.Cancelled => ("لغو‌شده", "bg-error-container text-error", "error"),
        _ => (status.ToString(), "bg-surface-container-high text-on-surface-variant", "info")
    };
}
