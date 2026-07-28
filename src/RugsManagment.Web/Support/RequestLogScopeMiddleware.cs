using System.Security.Claims;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Support;

/// <summary>
/// یک scope لاگ به ازای هر درخواست با شناسهٔ درخواست، کارگاه و کاربر.
///
/// چرا: وقتی چند کارگاه روی یک نمونه کار می‌کنند، پیام خام «ثبت فروش ناموفق» بی‌فایده است؛
/// باید بشود همهٔ خطوط یک درخواست را از هم جدا کرد. با scope، همهٔ لاگ‌های درون آن درخواست
/// (از جمله لاگ‌های EF و ASP.NET) این کلیدها را همراه خود می‌برند.
///
/// باید بعد از UseAuthentication ثبت شود وگرنه هویت کاربر هنوز مشخص نیست.
/// خروجی در تولید JSON است (AddJsonConsole در Program) تا مستقیم قابل ingest باشد.
/// </summary>
public sealed class RequestLogScopeMiddleware(RequestDelegate next, ILogger<RequestLogScopeMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var state = new Dictionary<string, object?>
        {
            ["RequestId"] = context.TraceIdentifier,
            ["RequestPath"] = context.Request.Path.Value,
            ["TenantId"] = context.User.FindFirstValue(ClaimsPrincipalExtensions.TenantIdClaim),
            ["UserId"] = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        };

        using (logger.BeginScope(state))
        {
            await next(context);
        }
    }
}

public static class RequestLogScopeExtensions
{
    public static IApplicationBuilder UseRequestLogScope(this IApplicationBuilder app)
        => app.UseMiddleware<RequestLogScopeMiddleware>();
}
