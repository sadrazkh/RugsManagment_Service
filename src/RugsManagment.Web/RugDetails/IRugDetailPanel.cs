using System.Security.Claims;
using RugsManagment.Application.DTOs.Rugs;

namespace RugsManagment.Web.RugDetails;

/// <summary>
/// یک بخش اضافه در صفحهٔ جزئیات فرش (<c>/Rugs/Details/{id}</c>) — درز فاز ۲ (spec I.4، K.1).
///
/// چرا: فازهای بعد چیزی به صفحهٔ فرش اضافه می‌کنند (جای فرش در نگله و محموله، سهم ثبت‌های هزینه، خروجی‌ها)؛ اگر هر کدام
/// <c>Details.cshtml</c> را ویرایش می‌کرد، فازهای موازی روی یک فایل به هم می‌خوردند. حالا هر فاز یک کلاس در
/// <c>Web/RugDetails/</c> می‌گذارد و <see cref="RugDetailPanelsViewComponent"/> خودکار پیدا و رندرش می‌کند (بی ثبت در
/// DI و بی ویرایش Program.cs). کلاس‌های Tailwind این پوشه را هم Tailwind می‌بیند (<c>@source</c> در app.css).
/// </summary>
public interface IRugDetailPanel
{
    /// <summary>شناسهٔ پایدار و یکتا (مثل «split-origin»).</summary>
    string Key { get; }

    /// <summary>کدام ستون صفحه.</summary>
    RugDetailSlot Slot { get; }

    /// <summary>ترتیب در همان ستون؛ کوچک‌تر بالاتر.</summary>
    int Order { get; }

    /// <summary>
    /// محتوای بخش برای این فرش و این بیننده، یا null یعنی «این‌جا چیزی نشان نده» (مثلاً بخش پولی برای انباردار، یا
    /// فرشی که دسته نیست). پنل خودش پول را برای غیر Business نشان نمی‌دهد (<see cref="RugDetailPanelContext.CanSeeMoney"/>).
    /// </summary>
    Task<RugDetailPanelView?> RenderAsync(RugDetailPanelContext context, CancellationToken cancellationToken);
}

/// <summary>ستون‌های صفحهٔ جزئیات فرش.</summary>
public enum RugDetailSlot
{
    /// <summary>ستون اصلی (مشخصات، اندازه‌ها، عکس‌ها)</summary>
    Main = 0,

    /// <summary>ستون کناری (مالی، کارهای کارگاه)</summary>
    Side = 1,

    /// <summary>زیر سربرگ، کنار نام فرش (چیپ‌های کوچک مثل «جداشده از …» یا «در نگلهٔ ۳»)</summary>
    Header = 2
}

/// <summary>آنچه یک پنل برای رندر لازم دارد.</summary>
/// <param name="Rug">فرش، همان DTOِ صفحه (پول برای غیر Business پنهان).</param>
/// <param name="User">بیننده.</param>
/// <param name="Simple">نمای ساده است (فقط پوسته؛ دسترسی همان می‌ماند).</param>
/// <param name="CanSeeMoney">بیننده مبلغ تومانی می‌بیند (Business).</param>
/// <param name="TenantId">کارگاه بیننده.</param>
public sealed record RugDetailPanelContext(RugDto Rug, ClaimsPrincipal User, bool Simple, bool CanSeeMoney, Guid TenantId);

/// <summary>
/// چیزی که پنل رندر می‌کند: یک partial (با مدلش) یا یک جزیرهٔ Vue (با props). دقیقاً یکی.
/// </summary>
/// <param name="Partial">مسیر partial، مثل «~/Views/Rugs/_Details.SplitOrigin.cshtml».</param>
/// <param name="Model">مدل partial.</param>
/// <param name="Island">نام جزیره (kebab-case)، مثل «rug-split».</param>
/// <param name="Props">props جزیره (به JSON سریال می‌شود).</param>
/// <param name="Skeleton">ارتفاع اسکلت بارگذاری جزیره، یا «off».</param>
public sealed record RugDetailPanelView(
    string? Partial = null,
    object? Model = null,
    string? Island = null,
    object? Props = null,
    string Skeleton = "10rem")
{
    /// <summary>یک partial.</summary>
    public static RugDetailPanelView ForPartial(string partial, object? model) => new(Partial: partial, Model: model);

    /// <summary>یک جزیره.</summary>
    public static RugDetailPanelView ForIsland(string island, object props, string skeleton = "10rem")
        => new(Island: island, Props: props, Skeleton: skeleton);
}
