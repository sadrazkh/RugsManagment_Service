namespace RugsManagment.Web.Navigation;
public sealed class ImportNavContributor : INavContributor
{
    public IEnumerable<NavItem> GetItems(NavContext context)
    {
        if (context.CanBusiness) yield return new NavItem("import", "ورود از اکسل", "/Import", "upload", NavOrder.Costs + 20, NavSlot.Command);
    }
}
