namespace RugsManagment.Domain.Enums;

/// <summary>
/// وضعیت یک مرحلهٔ مشخص روی یک فرش (مثلاً قالیشویی برای فرش X).
/// </summary>
public enum WorkflowStepStatus
{
    /// <summary>هنوز نوبت این مرحله نرسیده</summary>
    Pending = 0,

    /// <summary>الان روی این مرحله کار می‌شود</summary>
    InProgress = 1,

    /// <summary>این مرحله با موفقیت تمام شد</summary>
    Completed = 2,

    /// <summary>مرحله اختیاری بود و عمداً رد شد (مثلاً بدون دارکشی)</summary>
    Skipped = 3,

    /// <summary>لغو شده (برای آینده)</summary>
    Cancelled = 4
}
