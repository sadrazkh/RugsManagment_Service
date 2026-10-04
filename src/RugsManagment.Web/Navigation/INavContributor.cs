namespace RugsManagment.Web.Navigation;

/// <summary>
/// سهم یک ماژول در منو. <c>NavBuilder</c> همهٔ contributorهای ثبت‌شده را جمع می‌کند، بر اساس
/// <see cref="NavItem.Key"/> یکی و بر اساس <see cref="NavItem.Order"/> مرتب می‌کند و به نوار کناری، نوار
/// پایین، «بیشتر» و Ctrl+K می‌دهد.
///
/// چرا: منوی قدیمی یک آرایهٔ ثابت در layout بود و هر صفحهٔ تازه layout را ویرایش می‌کرد؛ حالا فاز
/// محموله، هزینه و ورود از اکسل هر کدام یک فایل contributor اضافه می‌کنند و layout دست نمی‌خورد.
///
/// قرارداد: فقط آیتم‌هایی که بیننده اجازهٔ باز کردنشان را دارد (از <see cref="NavContext"/>)؛ بدون
/// دسترسی به دیتابیس (منو در هر صفحه ساخته می‌شود)؛ برچسب‌ها با واژه‌های فرهنگ بازرگان. ثبت در DI به‌صورت
/// scoped؛ پیدا کردن خودکار همهٔ پیاده‌سازی‌های اسمبلی وب کار Program.cs است.
/// </summary>
public interface INavContributor
{
    IEnumerable<NavItem> GetItems(NavContext context);
}
