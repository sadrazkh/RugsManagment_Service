using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using RugsManagment.Application.Common;
using RugsManagment.Domain.Common;

namespace RugsManagment.Web.Filters;

/// <summary>
/// نگاشت خطاهای دامنه در صفحه‌های MVC (همتای <c>ApiExceptionFilter</c> برای APIها).
///
/// چرا: «این فرش در نگلهٔ ۱۲ است» یا «مبلغ ریال اعشار ندارد» خطای کاربر است نه خرابی سرور؛ بدون این فیلتر
/// هر فرم Razor (حذف فرش، بازگردانی، فرم‌های ورودی و تنظیمات) با چنین خطایی صفحهٔ ۵۰۰ می‌داد. حالا پیام
/// فارسی همان استثنا با سطح «خطا» در toast می‌نشیند و کاربر به صفحه‌ای که از آن آمده بود برمی‌گردد (۳۰۲).
///
/// فقط <see cref="DomainConflictException"/> (و زیرکلاس‌های فازهای بعد)، <see cref="AppValidationException"/> و
/// <see cref="DomainValidationException"/> را می‌گیرد؛ بقیه دست‌نخورده می‌مانند (۴۰۴ همان ۴۰۴ است، خطای واقعی
/// همان صفحهٔ خطا). کنترلرهای API را کاری ندارد، چون آن‌ها JSON برمی‌گردانند.
///
/// ثبت سراسری در Program.cs: <c>AddControllersWithViews(o =&gt; o.Filters.Add&lt;DomainExceptionPageFilter&gt;())</c>؛
/// این فایل دیگر ویرایش نمی‌شود.
/// </summary>
public sealed class DomainExceptionPageFilter(
    ITempDataDictionaryFactory tempDataFactory,
    ILogger<DomainExceptionPageFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.ExceptionHandled || !IsPageAction(context))
            return;

        var message = context.Exception switch
        {
            DomainConflictException e => e.Message,
            AppValidationException e => e.Message,
            DomainValidationException e => e.Message,
            _ => null
        };

        if (message is null)
            return;

        logger.LogInformation("خطای دامنه در صفحهٔ {Path}: {Message}", context.HttpContext.Request.Path, message);

        var tempData = tempDataFactory.GetTempData(context.HttpContext);
        PageToast.Set(tempData, message, PageToast.Error);

        context.Result = new RedirectResult(BackUrl(context.HttpContext.Request));
        context.ExceptionHandled = true;
    }

    /// <summary>
    /// اکشن کنترلر صفحه (فرزند <see cref="Controller"/>) است و نه API. کنترلرهای API از <c>ControllerBase</c>
    /// ارث می‌برند و <c>[ApiController]</c> دارند.
    /// </summary>
    private static bool IsPageAction(ExceptionContext context)
        => context.ActionDescriptor is ControllerActionDescriptor action
           && typeof(Controller).IsAssignableFrom(action.ControllerTypeInfo)
           && !action.EndpointMetadata.OfType<IApiBehaviorMetadata>().Any();

    /// <summary>
    /// صفحه‌ای که کاربر از آن آمد (Referer)، فقط اگر روی همین سایت باشد؛ وگرنه خانه. آدرس بیرونی هرگز
    /// مقصد ریدایرکت نمی‌شود.
    /// </summary>
    private static string BackUrl(HttpRequest request)
    {
        var referer = request.Headers.Referer.ToString();
        if (Uri.TryCreate(referer, UriKind.Absolute, out var uri)
            && string.Equals(uri.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase)
            && (!request.Host.Port.HasValue || uri.Port == request.Host.Port.Value)
            && uri.Scheme is "http" or "https")
        {
            var local = uri.PathAndQuery;
            if (local.StartsWith('/') && !local.StartsWith("//", StringComparison.Ordinal) && !local.StartsWith("/\\", StringComparison.Ordinal))
                return local;
        }

        return "/";
    }
}

/// <summary>
/// قرارداد toast سمت سرور: متن در <c>TempData["Toast"]</c> و سطح در <c>TempData["ToastLevel"]</c>
/// («success»، «error»، «warning»؛ نبودِ سطح یعنی success). layout هر دو را می‌خواند و به toast جزیره‌ها
/// می‌دهد، تا پیام خطا دیگر با رنگ «موفق» نیاید.
/// </summary>
public static class PageToast
{
    public const string MessageKey = "Toast";
    public const string LevelKey = "ToastLevel";

    public const string Success = "success";
    public const string Error = "error";
    public const string Warning = "warning";

    /// <summary>پیام و سطح را می‌نشاند (سطح ناشناخته = success).</summary>
    public static void Set(ITempDataDictionary tempData, string message, string level = Success)
    {
        tempData[MessageKey] = message;
        tempData[LevelKey] = level is Error or Warning ? level : Success;
    }

    /// <summary>پیام خطا.</summary>
    public static void SetError(ITempDataDictionary tempData, string message) => Set(tempData, message, Error);

    /// <summary>پیام هشدار.</summary>
    public static void SetWarning(ITempDataDictionary tempData, string message) => Set(tempData, message, Warning);
}
