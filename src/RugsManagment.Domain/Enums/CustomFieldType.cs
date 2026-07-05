namespace RugsManagment.Domain.Enums;

/// <summary>نوع دادهٔ یک فیلد سفارشی که کارگاه تعریف می‌کند.</summary>
public enum CustomFieldType
{
    /// <summary>متن آزاد</summary>
    Text = 0,

    /// <summary>عدد</summary>
    Number = 1,

    /// <summary>تاریخ</summary>
    Date = 2,

    /// <summary>انتخاب از فهرست (گزینه‌ها در OptionsJson)</summary>
    Select = 3,

    /// <summary>بله/خیر</summary>
    Boolean = 4
}
