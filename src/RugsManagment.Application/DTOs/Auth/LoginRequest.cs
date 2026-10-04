namespace RugsManagment.Application.DTOs.Auth;

/// <summary>فرم ورود (ایمیل + رمز)</summary>
public record LoginRequest(string Email, string Password);

/// <summary>
/// نتیجهٔ ورود یا تازه‌سازی نشست — آنچه لایهٔ وب برای ساختن کوکی لازم دارد.
///
/// مهر امنیتی عمداً بیرون از <see cref="UserDto"/> نگه داشته می‌شود: UserDto به JSON و به
/// viewها می‌رود، ولی مهر فقط باید داخل کوکی رمزنگاری‌شده بنشیند و هیچ‌جا نمایش داده نشود.
/// </summary>
public record AuthResult(UserDto User, string SecurityStamp);

/// <summary>کاربر بدون PasswordHash و بدون مهر امنیتی — امن برای JSON</summary>
public record UserDto(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    Guid? TenantId,
    string? TenantName,
    /// <summary>
    /// فعال بودن حساب. نگاشت عمومی آن را پر نمی‌کند (پیش‌فرض true)؛ مدیریت کاربران صریح
    /// مقدار واقعی را می‌گذارد تا فرم ویرایش حساب غیرفعال را بی‌صدا دوباره فعال نکند.
    /// </summary>
    bool IsActive = true);
