using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
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

        user.FullName = request.FullName.Trim();
        user.Role = request.Role;
        user.IsActive = request.IsActive;
        if (!string.IsNullOrWhiteSpace(request.NewPassword))
            user.PasswordHash = AuthService.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTimeOffset.UtcNow;

        users.Update(user);
        await unitOfWork.SaveChangesAsync(ct);
        return user.ToDto();
    }

    private static void EnsureTenantRole(UserRole role)
    {
        if (role is not (UserRole.TenantAdmin or UserRole.Operator))
            throw new InvalidOperationException("نقش نامعتبر برای کاربر کارگاه.");
    }
}
