namespace RugsManagment.Web.Navigation;

/// <summary>
/// «محموله‌ها» در منو (فاز ۴b، شکل نهایی؛ جدول A.4): برای هر کاربر کارگاه (Floor، همان سیاست صفحه‌ها) در نوار کناری، نوار
/// پایین گوشی و Ctrl+K — بازرگان «خانه · فرش‌ها · محموله‌ها · هزینه‌ها · بیشتر» و انباردار «خانه · فرش‌ها · محموله‌ها ·
/// بیشتر» را می‌بیند.
///
/// <see cref="CoreNavContributor"/> جای این آیتم را از پیش دارد؛ این‌جا همان کلید با Order کمتر برمی‌گردد (NavBuilder کمترین
/// Order را نگه می‌دارد) تا آیتم مال خودِ فاز محموله باشد: «فعال» بودنش همهٔ صفحه‌های زیر <c>/Shipments</c> را می‌گیرد —
/// کارگاه محموله (<c>/Shipments/{id}</c>) و «حالت اسکن (گوشی)» (<c>/Shipments/{id}/Pack</c>) — و توضیحش در «بیشتر» واژهٔ نگله
/// را ثابت نمی‌نویسد (منو بی دیتابیس ساخته می‌شود و واژهٔ کارگاه را نمی‌داند؛ کارگاهی که «عدل» می‌گوید نباید «نگله» ببیند).
/// </summary>
public sealed class ShipmentsNavContributor : INavContributor
{
    public const string Key = "shipments";

    public IEnumerable<NavItem> GetItems(NavContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.CanFloor)
            yield break;

        yield return new NavItem(Key, "محموله‌ها", "/Shipments", "truck", NavOrder.Shipments - 1,
            NavSlot.Sidebar | NavSlot.Bottom | NavSlot.Command,
            Hint: "اظهارهای صادراتی، بسته‌بندی فرش‌ها و حالت اسکن گوشی",
            ActivePrefix: "/Shipments");
    }
}
