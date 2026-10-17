namespace RugsManagment.Domain.Enums;

/// <summary>
/// جای فیزیکی/تجاری فرش — محاسبه می‌شود و هرگز ذخیره نمی‌شود (spec C.10).
///
/// چرا ذخیره نمی‌شود: از چهار واقعیتِ جدا (حذف، فروش، عضویت در نگله، قطعی بودن محموله) به دست می‌آید؛
/// ستونِ جدا یعنی یک جای دیگر که باید هم‌زمان درست بماند و دیر یا زود نمی‌ماند. ترتیبِ اولویت در
/// <see cref="RugLocationRules.Resolve"/> است. فقط افزودنی با عدد صریح.
/// </summary>
public enum RugLocation
{
    /// <summary>«در انبار»</summary>
    InStock = 0,

    /// <summary>«در نگله» (خطی در محمولهٔ باز)</summary>
    InBale = 1,

    /// <summary>«صادر شده» (خطی در محمولهٔ قطعی)</summary>
    Exported = 2,

    /// <summary>«فروخته‌شده»</summary>
    Sold = 3,

    /// <summary>در سطل زباله</summary>
    Trashed = 4
}

/// <summary>
/// تنها پیاده‌سازی C# از ترتیب C.10؛ پروجکشن‌های SQL همین ترتیب را با CASE تکرار می‌کنند.
/// </summary>
public static class RugLocationRules
{
    /// <summary>
    /// اولین شرطی که برقرار است برنده است: حذف ← فروش ← محمولهٔ قطعی ← محمولهٔ باز ← انبار.
    /// فروش پیش از صادرات است چون فرشِ فروخته‌شده دیگر مال کارگاه نیست، حتی اگر در محموله‌ای مانده باشد.
    /// </summary>
    /// <param name="trashed">فرش در سطل زباله است (<c>DeletedAt</c> دارد).</param>
    /// <param name="hasSale">فروش ثبت‌شده دارد.</param>
    /// <param name="inShipment">خطی در یک محموله (هر نگله‌ای) دارد.</param>
    /// <param name="shipmentFinalized">محمولهٔ آن خط قطعی شده است (بدون <paramref name="inShipment"/> بی‌اثر).</param>
    public static RugLocation Resolve(bool trashed, bool hasSale, bool inShipment, bool shipmentFinalized)
    {
        if (trashed) return RugLocation.Trashed;
        if (hasSale) return RugLocation.Sold;
        if (inShipment) return shipmentFinalized ? RugLocation.Exported : RugLocation.InBale;
        return RugLocation.InStock;
    }
}
