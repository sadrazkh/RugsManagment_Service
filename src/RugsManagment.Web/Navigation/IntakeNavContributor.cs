namespace RugsManagment.Web.Navigation;

/// <summary>
/// «ورود فرش» (<c>/Intake</c>) در منو. <see cref="CoreNavContributor"/> جای این آیتم را در جدول A.4 از پیش دارد (نوار کناری
/// حالت کامل و Ctrl+K)؛ این‌جا همان کلید با Order کمتر برمی‌گردد (NavBuilder کمترین Order را نگه می‌دارد) تا صفحهٔ
/// «بیشتر» هم آن را داشته باشد — «بیشتر» هر صفحه‌ای را که بیننده اجازه دارد و در نوار اصلی‌اش نیست فهرست می‌کند، و در
/// حالت ساده ورود فرش جز کاشی «ثبت فرش تازه» در خانه راه دیگری نداشت. برای هر کاربر کارگاه (Floor)، همان سیاست صفحه.
/// </summary>
public sealed class IntakeNavContributor : INavContributor
{
    public IEnumerable<NavItem> GetItems(NavContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.CanFloor)
            yield break;

        yield return new NavItem("intake", "ورود فرش", "/Intake", "plus", NavOrder.Intake - 1,
            (context.IsSimple ? NavSlot.None : NavSlot.Sidebar) | NavSlot.More | NavSlot.Command,
            Hint: "فرش‌های رسیده از فروشنده، در جدول", ActivePrefix: "/Intake");
    }
}
