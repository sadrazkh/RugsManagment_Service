using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace RugsManagment.Web.Home.Cards;

/// <summary>مدل کارت انبار.</summary>
/// <param name="Pieces">تخته‌های انبار (Σ تعداد، نه تعداد ردیف)</param>
/// <param name="AreaM2">متراژ انبار</param>
/// <param name="NewRugHref">«ثبت فرش تازه» در حالت خالی</param>
/// <param name="ImportHref">«ورود از اکسل» در حالت خالی (فقط Business و وقتی صفحه‌اش هست)</param>
public sealed record HomeStockModel(int Pieces, decimal AreaM2, string NewRugHref, string? ImportHref);

/// <summary>
/// کارت «انبار»: چند تخته و چند متر فرش در هیچ محموله‌ای نیست (A.4). بدون پول، پس برای همه.
/// انبار خالی همان پیام H.3 را می‌دهد: «انبار خالی است.» با «ثبت فرش تازه» (و برای Business «ورود از اکسل»
/// وقتی آن صفحه آمد).
/// </summary>
public sealed class StockCard(IActionDescriptorCollectionProvider actions) : IHomeCard
{
    public string Key => "stock";

    public int Order => HomeCardOrder.Stock;

    public Task<HomeCardView?> BuildAsync(HomeCardContext context, CancellationToken cancellationToken)
    {
        var features = HomeRoutes.Detect(actions);
        var model = new HomeStockModel(
            context.Summary.StockPieces,
            context.Summary.StockAreaM2,
            features.Intake ? HomeRoutes.Intake : HomeRoutes.LegacyRugCreate,
            context.CanBusiness && features.Import ? HomeRoutes.Import : null);

        return Task.FromResult<HomeCardView?>(
            new HomeCardView("~/Views/Shared/Components/HomeCards/Stock.cshtml", model));
    }
}
