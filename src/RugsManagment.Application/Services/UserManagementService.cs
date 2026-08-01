using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.DTOs.Auth;
using RugsManagment.Application.DTOs.Users;
using RugsManagment.Application.Mapping;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Services;

/// <summary>
/// مدیریت کاربران یک کارگاه — همیشه به tenantId محدود می‌شود تا نشت داده بین کارگاه‌ها رخ ندهد.
/// نقش SystemAdmin از این مسیر قابل ساخت نیست.
/// </summary>
public interface IUserManagementService
{
    Task<IReadOnlyList<UserDto>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<UserDto> CreateAsync(Guid tenantId, CreateUserRequest request, CancellationToken ct = default);
    Task<UserDto> UpdateAsync(Guid tenantId, Guid userId, UpdateUserRequest request, CancellationToken ct = default);
}

public sealed class UserManagementService(
    IUserRepository users,
    IAuditLog audit,
    IUnitOfWork unitOfWork) : IUserManagementService
{
    public async Task<IReadOnlyList<UserDto>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = await users.ListAsync(u => u.TenantId == tenantId, ct);
        return list.OrderBy(u => u.FullName).Select(u => u.ToDto()).ToList();
    }

    public async Task<UserDto> CreateAsync(Guid tenantId, CreateUserRequest request, CancellationToken ct = default)
    {
        EnsureTenantRole(request.Role);

        var email = request.Email.Trim().ToLowerInvariant();
        if (await users.GetByEmailAsync(email, ct) is not null)
            throw new InvalidOperationException("این ایمیل قبلاً ثبت شده است.");

        var user = new User
        {
            TenantId = tenantId,
            Email = email,
            FullName = request.FullName.Trim(),
            Role = request.Role,
            PasswordHash = AuthService.HashPassword(request.Password),
            IsActive = true
        };

        await users.AddAsync(user, ct);

        audit.Record(AuditAction.UserInvited, nameof(User), user.Id,
            $"کاربر «{user.FullName}» با نقش {RoleLabel(user.Role)} اضافه شد.", user.Email);

        await unitOfWork.SaveChangesAsync(ct);
        return user.ToDto();
    }

    public async Task<UserDto> UpdateAsync(Guid tenantId, Guid userId, UpdateUserRequest request, CancellationToken ct = default)
    {
        EnsureTenantRole(request.Role);

        var user = await users.GetByIdAsync(userId, ct)
            ?? throw new KeyNotFoundException("کاربر یافت نشد.");

        // جداسازی مستأجر: کاربر باید متعلق به همین کارگاه باشد
        if (user.TenantId != tenantId)
            throw new UnauthorizedAccessException("این کاربر متعلق به کارگاه شما نیست.");

        var wasActive = user.IsActive;
        var passwordReset = !string.IsNullOrWhiteSpace(request.NewPassword);

        user.FullName = request.FullName.Trim();
        user.Role = request.Role;
        user.IsActive = request.IsActive;
        if (passwordReset)
            user.PasswordHash = AuthService.HashPassword(request.NewPassword!);
        user.UpdatedAt = DateTimeOffset.UtcNow;

        users.Update(user);

        // غیرفعال شدن حساب رویداد امنیتی مهمی است و جدا ثبت می‌شود
        if (wasActive && !user.IsActive)
        {
            audit.Record(AuditAction.UserDisabled, nameof(User), user.Id,
                $"حساب «{user.FullName}» غیرفعال شد.", user.Email);
        }
        else
        {
            var note = passwordReset ? " (رمز عبور بازنشانی شد)" : "";
            audit.Record(AuditAction.Updated, nameof(User), user.Id,
                $"حساب «{user.FullName}» ویرایش شد{note}.", user.Email);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return user.ToDto();
    }

    private static void EnsureTenantRole(UserRole role)
    {
        if (role is not (UserRole.TenantAdmin or UserRole.Operator))
            throw new InvalidOperationException("نقش نامعتبر برای کاربر کارگاه.");
    }

    private static string RoleLabel(UserRole role)
        => role == UserRole.TenantAdmin ? "مدیر کارگاه" : "اپراتور";
}
