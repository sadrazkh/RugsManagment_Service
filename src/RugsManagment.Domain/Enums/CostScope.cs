namespace RugsManagment.Domain.Enums;

/// <summary>
/// یک هزینه برای کدام فرش‌هاست (spec D.2).
///
/// خودِ ثبت هزینه در فاز هزینه‌ها می‌آید، ولی نوع هزینه (<c>CostCategory.DefaultScope</c>) از همین فاز
/// پیش‌فرضش را نگه می‌دارد؛ پس enum از حالا هست تا آن ستون نوع درست داشته باشد. فقط افزودنی با عدد صریح.
/// </summary>
public enum CostScope
{
    /// <summary>فهرست صریح فرش‌ها («این فرش»، شماره‌ها، فیلتر یا «همهٔ انبار» هنگام ذخیره)</summary>
    Selection = 0,

    /// <summary>خط‌های فعلی چند نگله</summary>
    Bale = 1,

    /// <summary>همهٔ خط‌های یک محموله</summary>
    Shipment = 2,

    /// <summary>فرش‌های یک «ورودی» (خرید: عکس لحظهٔ ذخیره؛ هزینه: پویا)</summary>
    IntakeBatch = 3
}
