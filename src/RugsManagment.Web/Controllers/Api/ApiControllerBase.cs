using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>
/// پایهٔ کنترلرهای API (JSON) که جزیره‌های Vue مصرف می‌کنند.
/// خطاهای متداول سرویس‌ها را به کد وضعیت و پیام تمیز تبدیل می‌کند تا فرانت پیام دوستانه نشان دهد.
/// </summary>
[ApiController]
[Authorize]
[AutoValidateAntiforgeryToken] // POST/PUT/DELETE باید هدر X-CSRF-TOKEN داشته باشند
[ServiceFilter(typeof(ApiExceptionFilter))]
public abstract class ApiControllerBase : ControllerBase
{
}

/// <summary>نگاشت استثناهای دامنه به پاسخ JSON استاندارد.</summary>
public sealed class ApiExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var (status, message) = context.Exception switch
        {
            KeyNotFoundException e => (StatusCodes.Status404NotFound, e.Message),
            UnauthorizedAccessException e => (StatusCodes.Status403Forbidden, e.Message),
            InvalidOperationException e => (StatusCodes.Status400BadRequest, e.Message),
            ArgumentException e => (StatusCodes.Status400BadRequest, e.Message),
            _ => (0, string.Empty)
        };

        if (status == 0) return; // بقیه به‌صورت خطای مدیریت‌نشده باقی می‌مانند

        context.Result = new ObjectResult(new { message }) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
