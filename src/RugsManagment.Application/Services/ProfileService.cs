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

    /// <summary>
    /// تغییر رمز با تأیید رمز فعلی. مهر امنیتی هم عوض می‌شود تا نشست‌های دیگر بسته شوند.
    /// مهرِ تازه‌ای را برمی‌گرداند که همین درخواست نوشت — فقط همین مقدار حق دارد در کوکیِ خودِ کاربر بنشیند.
    /// </summary>
    Task<string> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default);
}

public sealed class ProfileService(
    IUserRepository users,
    IAuditLog audit,
    IUnitOfWork unitOfWork) : IProfileService
{
    public async Task<UserDto> GetAsync(Guid userId, CancellationToken ct = default)
        => (await LoadAsync(userId, ct)).ToDto();

    public async Task<UserDto> UpdateNameAsync(Guid userId, string fullName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new InvalidOperationException("نام نمی‌تواند خالی باشد.");

        var user = await LoadAsync(userId, ct);
        user.FullName = PersianText.Normalize(fullName.Trim());
        user.UpdatedAt = DateTimeOffset.UtcNow;
        // بدون users.Update: کاربر tracked است و EF فقط همین دو ستون را می‌نویسد (چرا: توضیح LoadAsync)
        await unitOfWork.SaveChangesAsync(ct);
        return user.ToDto();
    }

    public async Task<string> ChangePasswordAsync(
        Guid userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await LoadAsync(userId, ct);

        // رمز فعلی حتماً بررسی می‌شود: اگر کسی پشت سیستمِ باز نشسته باشد،
        // نباید بتواند بدون دانستن رمز، حساب را از دست صاحبش خارج کند.
        if (!AuthService.VerifyPassword(currentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("رمز فعلی درست نیست.");

        // همان قاعدهٔ رمزی که تنظیم‌کننده برای کاربر تازه یا بازنشانی می‌گذارد (PasswordPolicy)
        if (!PasswordPolicy.IsAcceptable(newPassword))
            throw new InvalidOperationException(
                $"رمز جدید باید حداقل {PersianText.ToPersianDigits(PasswordPolicy.MinLength.ToString())} کاراکتر باشد.");

        if (AuthService.VerifyPassword(newPassword, user.PasswordHash))
            throw new InvalidOperationException("رمز جدید نباید با رمز فعلی یکسان باشد.");

        user.PasswordHash = AuthService.HashPassword(newPassword);
        // مهر تازه یعنی همهٔ نشست‌های دیگرِ این کاربر (دستگاه گم‌شده، مرورگر مشترک) باطل می‌شوند؛
        // کنترلر بلافاصله کوکی همین دستگاه را با مهر جدید بازنویسی می‌کند تا خودِ کاربر بیرون نیفتد.
        user.RotateSecurityStamp();
        user.UpdatedAt = DateTimeOffset.UtcNow;
        // بدون users.Update: فقط رمز، مهر و UpdatedAt نوشته می‌شوند (چرا: توضیح LoadAsync)

        // خودِ رمز هرگز در لاگ نمی‌آید — فقط این واقعیت که عوض شده
        audit.Record(AuditAction.PasswordChanged, nameof(User), user.Id, "رمز عبور تغییر کرد.", user.FullName);

        await unitOfWork.SaveChangesAsync(ct);
        return user.SecurityStamp;
    }

    /// <summary>
    /// کاربر را tracked می‌خواند؛ نوشتن‌ها فقط با ردیابی تغییرات EF ذخیره می‌شوند.
    ///
    /// چرا هیچ‌جا users.Update(user) نیست: روی موجودیتِ tracked همهٔ ستون‌ها را Modified می‌کرد. User توکن
    /// هم‌زمانی ندارد، پس اگر مدیر بین خواندن و ذخیرهٔ این سرویس حساب را غیرفعال کرده، نقشش را عوض کرده یا رمزش
    /// را بازنشانی کرده بود، این ذخیره مقادیر کهنه (IsActive، Role، رمز و مهر قدیمی) را بی‌صدا برمی‌گرداند و
    /// ابطالِ نشست بی‌اثر می‌شد. حالا فقط ستون‌هایی که همین متد عوض کرده نوشته می‌شوند.
    /// </summary>
    private async Task<User> LoadAsync(Guid userId, CancellationToken ct)
        => await users.GetByIdAsync(userId, ct)
           ?? throw new KeyNotFoundException("کاربر یافت نشد.");
}
