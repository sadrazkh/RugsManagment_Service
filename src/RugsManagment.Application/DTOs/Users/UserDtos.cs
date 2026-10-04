using RugsManagment.Application.DTOs.Auth;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.DTOs.Users;

/// <summary>ساخت کاربر جدید در یک کارگاه (توسط مدیر همان کارگاه).</summary>
public record CreateUserRequest(string FullName, string Email, string Password, UserRole Role);

/// <summary>ویرایش کاربر کارگاه — رمز اختیاری (خالی یعنی بدون تغییر).</summary>
public record UpdateUserRequest(string FullName, UserRole Role, bool IsActive, string? NewPassword);

/// <summary>
/// نتیجهٔ ویرایش کاربر. <see cref="RotatedSecurityStamp"/> فقط وقتی پر است که «همین» ویرایش مهر را چرخانده
/// (نقش/وضعیت/رمز) و برابر مهری است که همین درخواست نوشت.
///
/// چرا جدا از <see cref="UserDto"/>: مهر هرگز نباید به JSON یا view برود؛ این نتیجه فقط به کنترلر می‌رسد تا
/// اگر مدیر حساب خودش را طوری ویرایش کرد که مهر چرخید، کوکی خودش را با همین مهر بازنویسی کند. خواندن مهر
/// از دیتابیس به‌جای این مقدار، چرخشی را که دیگری هم‌زمان انجام داده بود هم به کوکی می‌آورد و نشستِ باطل‌شده را زنده می‌کرد.
/// </summary>
public record UserUpdateResult(UserDto User, string? RotatedSecurityStamp);
