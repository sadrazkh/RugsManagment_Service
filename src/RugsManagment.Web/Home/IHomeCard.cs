using System.Security.Claims;
using RugsManagment.Application.Features.Platform.DTOs;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Web.Home;

/// <summary>
/// یک کارت صفحهٔ خانه (کاشی‌های بزرگ، انبار، محموله‌های باز، هزینه‌ها، فعالیت اخیر).
///
/// چرا: خانه هر فاز چیزی تازه نشان می‌دهد؛ اگر view خانه هر بار ویرایش می‌شد، فازهای موازی روی یک فایل
/// به هم می‌خوردند. حالا هر فاز یک کلاس کارت و یک partial اضافه می‌کند و view خانه فقط کارت‌ها را به
/// ترتیب <see cref="Order"/> می‌چیند.
///
/// قرارداد: <see cref="BuildAsync"/> برای بیننده‌ای که نباید کارت را ببیند null برمی‌گرداند (مثلاً کارت پول
/// برای انباردار)؛ خلاصهٔ خانه یک بار ساخته و در <see cref="HomeCardContext.Summary"/> به همه داده می‌شود،
/// پس کارت‌ها برای همان داده دوباره کوئری نمی‌زنند. ثبت در DI به‌صورت scoped. کلاس‌های Tailwind فقط در
/// partial یا در فایل‌های <c>Web/Home/**</c> (که منبع Tailwind است).
/// </summary>
public interface IHomeCard
{
    /// <summary>شناسهٔ پایدار (مثل «tiles»، «stock»).</summary>
    string Key { get; }

    /// <summary>ترتیب نمایش؛ از <see cref="HomeCardOrder"/>.</summary>
    int Order { get; }

    /// <summary>partial و مدل کارت، یا null یعنی برای این بیننده نشان داده نشود.</summary>
    Task<HomeCardView?> BuildAsync(HomeCardContext context, CancellationToken cancellationToken);
}

/// <summary>آنچه view خانه برای یک کارت رندر می‌کند.</summary>
/// <param name="ViewName">مسیر partial، مثل «~/Views/Home/Cards/_Tiles.cshtml»</param>
/// <param name="Model">مدل partial</param>
/// <param name="Wide">تمام‌عرض (کاشی‌ها) یا نیم‌عرض روی دسکتاپ</param>
public sealed record HomeCardView(string ViewName, object? Model, bool Wide = false);

/// <summary>بینندهٔ خانه و دادهٔ مشترک کارت‌ها.</summary>
/// <param name="User">کاربر جاری</param>
/// <param name="TenantId">کارگاه</param>
/// <param name="Role">نقش</param>
/// <param name="CanBusiness">سیاست Business (مبلغ‌ها، محمولهٔ تازه، ثبت هزینه)</param>
/// <param name="CanConfigure">سیاست Configure</param>
/// <param name="IsSimple">حالت مؤثر پوسته (ساده یا پیش‌نمایش «نمای ساده»)</param>
/// <param name="Summary">خلاصهٔ خانه — عضوهای پولی‌اش برای غیر Business از پیش null است</param>
public sealed record HomeCardContext(
    ClaimsPrincipal User,
    Guid TenantId,
    UserRole Role,
    bool CanBusiness,
    bool CanConfigure,
    bool IsSimple,
    HomeSummaryDto Summary);

/// <summary>ترتیب کارت‌های خانه (A.4). فاصله‌ها برای کارت‌های فازهای بعد است.</summary>
public static class HomeCardOrder
{
    /// <summary>چهار کاشی بزرگ</summary>
    public const int Tiles = 100;

    /// <summary>محموله‌های باز (فاز محموله)</summary>
    public const int OpenShipments = 200;

    /// <summary>تراشه‌های آمادگی</summary>
    public const int Readiness = 300;

    /// <summary>انبار: تخته و متراژ</summary>
    public const int Stock = 400;

    /// <summary>پول (فقط Business؛ فاز هزینه)</summary>
    public const int Money = 500;

    /// <summary>فعالیت اخیر</summary>
    public const int Recent = 900;
}
