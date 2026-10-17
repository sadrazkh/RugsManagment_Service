namespace RugsManagment.Web.Navigation;

/// <summary>
/// سهم فاز هزینه در منو (A.4). خودِ «هزینه‌ها» (<c>/Costs</c>) را <see cref="CoreNavContributor"/> از پیش سر جایش دارد (نوار
/// کناری و نوار پایین، فقط Business، وقتی <c>CostsController</c> هست)؛ این‌جا فقط فرمان Ctrl+K «ثبت هزینه» اضافه می‌شود که
/// صفحهٔ هزینه‌ها را با پنل ثبت باز می‌کند (<c>/Costs?new=1</c>)، چون پنل از هر صفحه‌ای فقط با کاشی خانه در دسترس بود.
/// انباردار (بی Business) هیچ‌کدام را نمی‌بیند؛ پول تومانی برای او نیست (A.5).
/// </summary>
public sealed class CostsNavContributor : INavContributor
{
    /// <summary>کلید فرمان «ثبت هزینه».</summary>
    public const string NewCostKey = "new-cost";

    /// <summary>صفحهٔ هزینه‌ها با پنل ثبت باز.</summary>
    public const string NewCostHref = "/Costs?new=1";

    public IEnumerable<NavItem> GetItems(NavContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.CanBusiness)
            yield break;

        yield return new NavItem(NewCostKey, "ثبت هزینه", NewCostHref, "coins", NavOrder.Costs + 5, NavSlot.Command,
            Hint: "رسید کرایه، شستشو و هزینه‌های دیگر");
    }
}
