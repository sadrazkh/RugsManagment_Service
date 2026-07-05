using System.Globalization;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Web.Support;

/// <summary>کمک‌کننده‌های نمایش مشترک برای viewها — برچسب وضعیت، قالب مبلغ.</summary>
public static class DisplayHelpers
{
    /// <summary>مبلغ با جداکنندهٔ هزارگان (بدون اعشار)؛ خالی برای null.</summary>
    public static string Money(decimal? value)
        => value.HasValue ? value.Value.ToString("#,0", CultureInfo.InvariantCulture) : "—";

    public static string Number(decimal value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);

    public static (string Label, string Css) RugStatus(RugStatus status) => status switch
    {
        Domain.Enums.RugStatus.Draft => ("پیش‌نویس", "bg-surface-container text-on-surface-variant"),
        Domain.Enums.RugStatus.InProgress => ("در جریان", "bg-secondary-container text-on-secondary-container"),
        Domain.Enums.RugStatus.ReadyForSale => ("آمادهٔ فروش", "bg-success/10 text-success"),
        Domain.Enums.RugStatus.Sold => ("فروخته‌شده", "bg-primary/10 text-primary"),
        Domain.Enums.RugStatus.Archived => ("بایگانی", "bg-surface-container text-on-surface-variant"),
        _ => (status.ToString(), "bg-surface-container")
    };

    public static (string Label, string Css) StepStatus(WorkflowStepStatus status) => status switch
    {
        WorkflowStepStatus.Pending => ("در صف", "bg-surface-container text-on-surface-variant"),
        WorkflowStepStatus.InProgress => ("در حال انجام", "bg-secondary-container text-on-secondary-container"),
        WorkflowStepStatus.Completed => ("تکمیل‌شده", "bg-success/10 text-success"),
        WorkflowStepStatus.Skipped => ("رد‌شده", "bg-surface-container text-on-surface-variant line-through"),
        WorkflowStepStatus.Cancelled => ("لغو‌شده", "bg-error-container text-error"),
        _ => (status.ToString(), "bg-surface-container")
    };
}
