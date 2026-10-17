using RugsManagment.Application.DTOs.Activity;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Web.Home.Cards;

/// <summary>مدل کارت «کارهای اخیر».</summary>
/// <param name="Entries">رویدادها، تازه‌ترین اول</param>
/// <param name="ActivityHref">«همه» (فقط برای کسی که تاریخچهٔ کامل را می‌بیند)</param>
public sealed record HomeRecentModel(IReadOnlyList<ActivityEntryDto> Entries, string? ActivityHref)
{
    /// <summary>نماد هر نوع رویداد.</summary>
    public static string IconFor(AuditAction action) => (int)action switch
    {
        0 => "plus",
        1 => "edit",
        2 => "trash",
        3 => "undo",
        >= 10 and < 20 => "workflow",
        >= 20 and < 30 => "receipt",
        >= 30 and < 40 => "handshake",
        >= 40 and < 50 => "users",
        >= 50 and < 70 => "settings",
        >= 70 and < 80 => "ruler",
        >= 80 and < 90 => "bale",
        >= 90 and < 100 => "coins",
        >= 100 and < 110 => "spreadsheet",
        >= 110 and < 120 => "upload",
        _ => "info"
    };
}

/// <summary>
/// کارت «کارهای اخیر» (A.4) از <see cref="Application.Features.Platform.DTOs.HomeSummaryDto.Recent"/>.
/// برای غیر Business، سرویس خلاصه رویدادهای پولی را از پیش حذف کرده است. نبودِ رویداد = کارت دیده نمی‌شود
/// (برای کارگاه تازه، کارت خالی فقط فضا می‌گیرد).
/// </summary>
public sealed class RecentCard : IHomeCard
{
    /// <summary>بیش از این، کارت از صفحه بلندتر می‌شود؛ بقیه در «تاریخچهٔ فعالیت».</summary>
    public const int MaxEntries = 6;

    public string Key => "recent";

    public int Order => HomeCardOrder.Recent;

    public Task<HomeCardView?> BuildAsync(HomeCardContext context, CancellationToken cancellationToken)
    {
        var entries = context.Summary.Recent.Take(MaxEntries).ToList();
        if (entries.Count == 0) return Task.FromResult<HomeCardView?>(null);

        var model = new HomeRecentModel(entries, context.CanConfigure ? "/Settings/Activity" : null);
        return Task.FromResult<HomeCardView?>(
            new HomeCardView("~/Views/Shared/Components/HomeCards/Recent.cshtml", model));
    }
}
