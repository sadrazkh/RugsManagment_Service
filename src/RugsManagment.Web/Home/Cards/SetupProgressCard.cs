using RugsManagment.Application.Features.Platform;
namespace RugsManagment.Web.Home.Cards;
public sealed class SetupProgressCard(SetupStatusService setup) : IHomeCard
{
    public string Key => "setup-progress";
    public int Order => 50;
    public async Task<HomeCardView?> BuildAsync(HomeCardContext context, CancellationToken cancellationToken)
    {
        if (!context.CanConfigure) return null;
        var status = await setup.GetAsync(context.TenantId,cancellationToken);
        return status.Ready ? null : new HomeCardView("~/Views/Shared/Components/HomeCards/SetupProgress.cshtml",status.Remaining,true);
    }
}
