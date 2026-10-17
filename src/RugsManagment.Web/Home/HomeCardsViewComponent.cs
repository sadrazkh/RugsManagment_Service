using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.Features.Platform;
using RugsManagment.Application.Features.Platform.DTOs;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;
using RugsManagment.Web.Support;

namespace RugsManagment.Web.Home;

/// <summary>کارت‌های آمادهٔ خانه، به ترتیب.</summary>
public sealed record HomeCardsModel(IReadOnlyList<HomeCardView> Cards);

/// <summary>
/// صفحهٔ خانه را از همهٔ <see cref="IHomeCard"/>های ثبت‌شده می‌سازد: خلاصهٔ خانه را یک بار می‌گیرد (برای
/// انباردار بی‌پول)، به هر کارت می‌دهد و partialهای برگشتی را به ترتیب <see cref="IHomeCard.Order"/> می‌چیند.
///
/// view خانه فقط <c>@await Component.InvokeAsync("HomeCards")</c> است؛ پس فاز محموله (کارت محموله‌های باز و
/// آمادگی) و فاز هزینه (کارت پول) یک کلاس کارت و یک partial اضافه می‌کنند و هیچ فایل مشترکی را ویرایش
/// نمی‌کنند. کارتی که خطا بدهد کنار گذاشته و ثبت می‌شود؛ بقیهٔ خانه باز می‌شود.
/// </summary>
public sealed class HomeCardsViewComponent(
    IEnumerable<IHomeCard> cards,
    IHomeSummaryService summaries,
    IUserPreferencesAccessor preferences,
    ILogger<HomeCardsViewComponent> logger) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var user = HttpContext.User;
        if (user.GetTenantId() is not Guid tenantId)
            return View(new HomeCardsModel([]));

        var ct = HttpContext.RequestAborted;
        var summary = await LoadSummaryAsync(tenantId, ct);
        var role = Enum.TryParse<UserRole>(user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value, out var parsed)
            ? parsed
            : UserRole.Operator;

        var context = new HomeCardContext(
            User: user,
            TenantId: tenantId,
            Role: role,
            CanBusiness: Policies.IsBusiness(user),
            CanConfigure: Policies.IsConfigure(user),
            IsSimple: preferences.IsSimple,
            Summary: summary);

        var views = new List<HomeCardView>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var card in cards.OrderBy(c => c.Order).ThenBy(c => c.Key, StringComparer.Ordinal))
        {
            if (!seen.Add(card.Key)) continue; // یک کلید دو بار ثبت شده (مثلاً اسکن دوباره) → یک بار

            try
            {
                if (await card.BuildAsync(context, ct) is { } view)
                    views.Add(view);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "کارت خانهٔ {Card} ساخته نشد؛ بقیهٔ خانه نشان داده می‌شود.", card.Key);
            }
        }

        return View(new HomeCardsModel(views));
    }

    private async Task<HomeSummaryDto> LoadSummaryAsync(Guid tenantId, CancellationToken ct)
    {
        try
        {
            return await summaries.GetAsync(tenantId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // خانه باید باز شود حتی اگر شمارش انبار خطا داد؛ کارت‌ها حالت خالی را نشان می‌دهند
            logger.LogError(ex, "خلاصهٔ خانهٔ کارگاه {TenantId} ساخته نشد.", tenantId);
            return new HomeSummaryDto([], [], 0, 0m, null, null, []);
        }
    }
}
