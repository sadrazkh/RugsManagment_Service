namespace RugsManagment.Domain.Common;

/// <summary>
/// پایهٔ همهٔ موجودیت‌های دیتابیس.
/// هر رکورد یک شناسه یکتا (Id) و زمان ایجاد/ویرایش دارد تا تاریخچه قابل ردیابی باشد.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>شناسه یکتای رکورد در دیتابیس</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>زمان ثبت اولیه (همیشه UTC)</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>آخرین زمان ویرایش؛ اگر null باشد یعنی هنوز ویرایش نشده</summary>
    public DateTimeOffset? UpdatedAt { get; set; }
}
