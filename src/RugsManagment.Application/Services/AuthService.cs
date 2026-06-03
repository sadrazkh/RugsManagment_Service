using System.Security.Cryptography;
using System.Text;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.DTOs.Auth;
using RugsManagment.Application.Mapping;

namespace RugsManagment.Application.Services;

/// <summary>
/// ورود و احراز هویت — بررسی رمز، به‌روزرسانی آخرین ورود، صدور JWT از طریق IJwtTokenGenerator.
/// </summary>
public sealed class AuthService(
    IUserRepository users,
    IJwtTokenGenerator tokenGenerator) : IAuthService
{
    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), cancellationToken)
            ?? throw new UnauthorizedAccessException("ایمیل یا رمز عبور اشتباه است.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("حساب کاربری غیرفعال است.");

        if (!VerifyPassword(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("ایمیل یا رمز عبور اشتباه است.");

        user.LastLoginAt = DateTimeOffset.UtcNow;
        users.Update(user);

        var (token, expires) = tokenGenerator.Generate(user);
        return new AuthResponse(token, expires, user.ToDto());
    }

    public async Task<AuthResponse> RefreshAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("کاربر یافت نشد.");

        var (token, expires) = tokenGenerator.Generate(user);
        return new AuthResponse(token, expires, user.ToDto());
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

/// <summary>ساخت توکن JWT — پیاده‌سازی در Infrastructure با کلید appsettings</summary>
public interface IJwtTokenGenerator
{
    (string Token, DateTimeOffset ExpiresAt) Generate(Domain.Entities.User user);
}
