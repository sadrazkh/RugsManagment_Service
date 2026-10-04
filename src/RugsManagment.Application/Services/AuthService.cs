using System.Security.Cryptography;
using System.Text;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.DTOs.Auth;
using RugsManagment.Application.Mapping;

namespace RugsManagment.Application.Services;

/// <summary>
/// ورود و احراز هویت — بررسی رمز و به‌روزرسانی آخرین ورود.
///
/// خودِ کوکی را لایهٔ وب می‌سازد؛ اینجا فقط کاربر و مهر امنیتی‌اش برگردانده می‌شود.
/// (صدور توکنِ قدیمی حذف شد: میزبان فعلی فقط کوکی دارد و توکنی که صادر می‌شد هیچ‌جا مصرف نمی‌شد.)
/// </summary>
public sealed class AuthService(IUserRepository users) : IAuthService
{
    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), cancellationToken)
            ?? throw new UnauthorizedAccessException("ایمیل یا رمز عبور اشتباه است.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("حساب کاربری غیرفعال است.");

        if (!VerifyPassword(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("ایمیل یا رمز عبور اشتباه است.");

        // بررسی وضعیت کارگاه بعد از تأیید رمز انجام می‌شود تا پیام‌های خطا
        // اطلاعاتی دربارهٔ وجود یا نبود حساب لو ندهند.
        if (user.Tenant is { } tenant)
        {
            if (!tenant.IsActive)
                throw new UnauthorizedAccessException("دسترسی این کارگاه غیرفعال شده است. با پشتیبانی تماس بگیرید.");

            if (tenant.SubscriptionExpiresAt is { } expiry && expiry <= DateTimeOffset.UtcNow)
                throw new UnauthorizedAccessException("اشتراک این کارگاه به پایان رسیده است. برای تمدید با پشتیبانی تماس بگیرید.");
        }

        // کاربر tracked است و ردیابی تغییرات EF فقط همین ستون را می‌نویسد. users.Update(user) عمداً صدا زده
        // نمی‌شود: همهٔ ستون‌ها را Modified می‌کرد و اگر مدیر در فاصلهٔ خواندن تا ذخیره (PBKDF2 بالا) حساب را
        // غیرفعال/تنزل داده یا رمزش را بازنشانی کرده بود، ذخیرهٔ ورود مقادیر کهنه (IsActive، Role، رمز و مهر قدیمی)
        // را بی‌صدا برمی‌گرداند — User توکن هم‌زمانی ندارد که این تداخل را بگیرد.
        user.LastLoginAt = DateTimeOffset.UtcNow;

        return new AuthResult(user.ToDto(), user.SecurityStamp);
    }

    /// <summary>ساخت هش رمز با PBKDF2 + salt تصادفی — برای ثبت کاربر جدید</summary>
    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            100_000,
            HashAlgorithmName.SHA256,
            32);

        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 2)
            return false;

        var salt = Convert.FromBase64String(parts[0]);
        var expected = Convert.FromBase64String(parts[1]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            100_000,
            HashAlgorithmName.SHA256,
            32);

        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
