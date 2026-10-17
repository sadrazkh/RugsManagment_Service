namespace RugsManagment.Application.Common;

/// <summary>
/// آیا کاربر جاری مبلغ تومانی ببیند و بنویسد — تصمیم «انباردار هیچ مبلغی نمی‌بیند».
///
/// پیاده‌سازی در لایهٔ وب است و از سیاست Business (بازرگان و تنظیم‌کننده) می‌خواند؛ Application فقط
/// همین پرسش را می‌پرسد و چیزی از HttpContext نمی‌داند. سرویس‌هایی که DTO می‌سازند با
/// <see cref="MoneyRedaction"/> عضوهای پولی را null می‌کنند و درخواستِ پولی از کسی که این پاسخش
/// false است را رد می‌کنند. دلار گمرکی پول داخلی نیست و به این پرسش ربطی ندارد.
/// </summary>
public interface IMoneyVisibility
{
    /// <summary>true یعنی مبلغ‌ها دیده و نوشته می‌شوند؛ false یعنی همه null و هر نوشتن پولی ۴۰۳.</summary>
    bool CanSeeMoney { get; }
}

/// <summary>
/// پاسخ همیشه «بله» — برای اجراهای بدون کاربر (seed، کار استارت، آزمون‌ها) که باید عدد واقعی را
/// ببینند. هرگز در مسیر درخواست کاربر ثبت نشود.
/// </summary>
public sealed class FullMoneyVisibility : IMoneyVisibility
{
    public static readonly FullMoneyVisibility Instance = new();

    public bool CanSeeMoney => true;
}
