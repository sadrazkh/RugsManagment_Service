using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Common;
using RugsManagment.Domain.Common;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>
/// پایهٔ کنترلرهای API (JSON) که جزیره‌های Vue مصرف می‌کنند.
/// خطاهای متداول سرویس‌ها را به کد وضعیت و پیام تمیز تبدیل می‌کند تا فرانت پیام دوستانه نشان دهد.
///
/// پیش‌فرض دسترسی <see cref="Policies.Floor"/> است (هر کاربر کارگاه، نه مدیر سامانه)؛ اکشن‌هایی که پول یا
/// تنظیمات را لمس می‌کنند <see cref="Policies.Business"/> یا <see cref="Policies.Configure"/> را اضافه می‌کنند.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.Floor)]
[AutoValidateAntiforgeryToken] // POST/PUT/DELETE باید هدر X-CSRF-TOKEN داشته باشند
[ServiceFilter(typeof(ApiExceptionFilter))]
public abstract class ApiControllerBase : ControllerBase
{
}

/// <summary>
/// نگاشت استثناهای دامنه به پاسخ JSON استاندارد.
///
/// شکل بدنه‌ها:
/// <list type="bullet">
/// <item>۴۰۰ اعتبارسنجی: <c>{message, errors: {field: [msg]}}</c> — جزیره خطا را زیر همان خانه نشان می‌دهد.</item>
/// <item>۴۰۹ تعارض دامنه: <c>{message, field, …}</c> — کلیدهای اضافه از <see cref="DomainConflictException.Extensions"/>
/// (مثل <c>nextFree</c>)، پس زیرکلاس‌های فازهای بعد این فیلتر را ویرایش نمی‌کنند.</item>
/// <item>بقیه: <c>{message}</c>.</item>
/// </list>
/// ترتیب شاخه‌ها مهم است: استثناهای اعتبارسنجی و تعارض پیش از <see cref="InvalidOperationException"/> می‌آیند.
/// </summary>
public sealed class ApiExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var result = context.Exception switch
        {
            KeyNotFoundException e => Body(StatusCodes.Status404NotFound, e.Message),
            UnauthorizedAccessException e => Body(StatusCodes.Status403Forbidden, e.Message),
            AppValidationException e => Validation(e.Message, e.Errors, e.Code),
            DomainValidationException e => Validation(e.Message, e.Errors),
            DomainConflictException e => Conflict(e),
            ConcurrencyConflictException e => Body(StatusCodes.Status409Conflict, e.Message),
            DuplicateKeyException e => Body(StatusCodes.Status409Conflict, e.Message),
            InvalidOperationException e => Body(StatusCodes.Status400BadRequest, e.Message),
            ArgumentException e => Body(StatusCodes.Status400BadRequest, e.Message),
            _ => null
        };

        if (result is null) return; // بقیه به‌صورت خطای مدیریت‌نشده باقی می‌مانند

        context.Result = result;
        context.ExceptionHandled = true;
    }

    private static ObjectResult Body(int status, string message)
        => new(new { message }) { StatusCode = status };

    private static ObjectResult Validation(string message, IReadOnlyDictionary<string, string[]> errors, string? code = null)
        => new(new { message, errors, code }) { StatusCode = StatusCodes.Status400BadRequest };

    private static ObjectResult Conflict(DomainConflictException e)
    {
        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["message"] = e.Message,
            ["field"] = e.Field
        };

        // کلیدهای اضافهٔ زیرکلاس؛ «message» و «field» را بازنویسی نمی‌کنند
        if (e.Extensions is { } extensions)
        {
            foreach (var (key, value) in extensions)
                body.TryAdd(key, value);
        }

        return new ObjectResult(body) { StatusCode = StatusCodes.Status409Conflict };
    }
}
