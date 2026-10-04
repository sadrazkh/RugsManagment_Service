using RugsManagment.Application.DTOs.Auth;

namespace RugsManagment.Application.Abstractions.Services;

/// <summary>
/// ورود با ایمیل و رمز.
///
/// عمداً متدی برای «خواندن کاربر و مهرِ فعلیِ دیتابیس برای بازنویسی کوکی» ندارد: کوکیِ خودِ کاربر فقط با
/// مهری بازنویسی می‌شود که همان درخواست نوشته (تغییر رمز/نقش) یا مهری که همین حالا در کوکی است. مهرِ
/// دیتابیس ممکن است جای دیگری چرخیده باشد و کپی‌اش کوکیِ باطل‌شده را دوباره معتبر می‌کرد.
/// </summary>
public interface IAuthService
{
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
