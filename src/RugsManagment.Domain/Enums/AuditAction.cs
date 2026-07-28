namespace RugsManagment.Domain.Enums;

/// <summary>
/// نوع رویداد در تاریخچهٔ فعالیت.
///
/// عمداً محدود و معنایی است (نه «Insert/Update/Delete» خام) تا فهرست فعالیت
/// برای کاربر کارگاه خوانا باشد و بشود رویش فیلتر گذاشت.
/// </summary>
public enum AuditAction
{
    Created = 0,
    Updated = 1,
    Deleted = 2,

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
    SettingsChanged = 50
}
