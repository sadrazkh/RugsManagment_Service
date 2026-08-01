using RugsManagment.Application.DTOs.Auth;

namespace RugsManagment.Application.Abstractions.Services;

/// <summary>
/// ورود و تازه‌سازی اطلاعات کاربر.
///
/// خروجی فقط خودِ کاربر است: نشست با کوکی نگه داشته می‌شود و لایهٔ وب
/// claimها را از همین <see cref="UserDto"/> می‌سازد.
/// </summary>
public interface IAuthService
{
    Task<UserDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>خواندن دوبارهٔ کاربر — بعد از ویرایش پروفایل، برای تازه کردن کوکی.</summary>
    Task<UserDto> RefreshAsync(Guid userId, CancellationToken cancellationToken = default);
}
