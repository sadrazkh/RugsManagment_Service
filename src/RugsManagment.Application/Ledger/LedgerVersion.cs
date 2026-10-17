namespace RugsManagment.Application.Ledger;

/// <summary>
/// نسخهٔ قاعده‌های محاسبهٔ حساب‌ها. هر ردیف <c>RugCostTotal</c> نسخه‌ای را که با آن ساخته شد نگه می‌دارد
/// (<c>LedgerVersion</c>)؛ وقتی این عدد بالا می‌رود (قاعدهٔ گرد کردن، منبع هزینهٔ تازه، فرمول تازه)، بررسی استارت
/// (<c>LedgerStartupCheck</c>) همهٔ کارگاه‌ها را از نو می‌سازد و هیچ‌کس لازم نیست migration دادهٔ جدا بنویسد.
/// </summary>
public static class LedgerVersion
{
    /// <summary>۱: فاز ۲ (سطرهای خرید با قاعدهٔ دسته، مراحل با کف صفر، بدون ثبت هزینه).</summary>
    public const long Current = 1;
}
