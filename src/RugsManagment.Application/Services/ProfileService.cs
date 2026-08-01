using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Auth;
using RugsManagment.Application.Mapping;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Services;

/// <summary>حساب کاربری خودِ کاربر — نام نمایشی و رمز عبور.</summary>
public interface IProfileService
{
    Task<UserDto> GetAsync(Guid userId, CancellationToken ct = default);
    Task<UserDto> UpdateNameAsync(Guid userId, string fullName, CancellationToken ct = default);

    /// <summary>تغییر رمز با تأیید رمز فعلی.</summary>
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default);
}

public sealed class ProfileService(
    IUserRepository users,
    IAuditLog audit,
    IUnitOfWork unitOfWork) : IProfileService
{
    /// <summary>حداقل طول رمز. کوتاه‌تر از این در برابر حدس‌زدن بی‌دفاع است.</summary>
    private const int MinPasswordLength = 8;

    public async Task<UserDto> GetAsync(Guid userId, CancellationToken ct = default)
        => (await LoadAsync(userId, ct)).ToDto();

    public async Task<UserDto> UpdateNameAsync(Guid userId, string fullName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new InvalidOperationException("نام نمی‌تواند خالی باشد.");

        var user = await LoadAsync(userId, ct);
        user.FullName = PersianText.Normalize(fullName.Trim());
        user.UpdatedAt = DateTimeOffset.UtcNow;
        users.Update(user);

        await unitOfWork.SaveChangesAsync(ct);
        return user.ToDto();
    }

    public async Task ChangePasswordAsync(
        Guid userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await LoadAsync(userId, ct);

        // رمز فعلی حتماً بررسی می‌شود: اگر کسی پشت سیستمِ باز نشسته باشد،
        // نباید بتواند بدون دانستن رمز، حساب را از دست صاحبش خارج کند.
        if (!AuthService.VerifyPassword(currentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("رمز فعلی درست نیست.");

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < MinPasswordLength)
            throw new InvalidOperationException(
                $"رمز جدید باید حداقل {PersianText.ToPersianDigits(MinPasswordLength.ToString())} کاراکتر باشد.");

        if (AuthService.VerifyPassword(newPassword, user.PasswordHash))
            throw new InvalidOperationException("رمز جدید نباید با رمز فعلی یکسان باشد.");

        user.PasswordHash = AuthService.HashPassword(newPassword);
        user.UpdatedAt = DateTimeOffset.UtcNow;
        users.Update(user);

        // خودِ رمز هرگز در لاگ نمی‌آید — فقط این واقعیت که عوض شده
        audit.Record(AuditAction.PasswordChanged, nameof(User), user.Id, "رمز عبور تغییر کرد.", user.FullName);

        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<User> LoadAsync(Guid userId, CancellationToken ct)
        => await users.GetByIdAsync(userId, ct)
           ?? throw new KeyNotFoundException("کاربر یافت نشد.");
}
