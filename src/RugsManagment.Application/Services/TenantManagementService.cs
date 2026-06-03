using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.DTOs.Tenants;
using RugsManagment.Application.Mapping;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Services;

/// <summary>مدیریت کارگاه‌ها — فقط ادمین سیستم از API TenantsController استفاده می‌کند</summary>
public interface ITenantManagementService
{
    Task<IReadOnlyList<TenantDto>> ListAsync(CancellationToken ct = default);
    Task<TenantDto> CreateAsync(CreateTenantRequest request, CancellationToken ct = default);
    Task<TenantDto> UpdateAsync(Guid id, UpdateTenantRequest request, CancellationToken ct = default);
}

public sealed class TenantManagementService(
    ITenantRepository tenants,
    IUserRepository users,
    IUnitOfWork unitOfWork) : ITenantManagementService
{
    public async Task<IReadOnlyList<TenantDto>> ListAsync(CancellationToken ct = default)
    {
        var list = await tenants.ListAsync(cancellationToken: ct);
        return list.Select(t => t.ToDto()).ToList();
    }

    /// <summary>
    /// کارگاه جدید + یک کاربر TenantAdmin همزمان — برای فروش اشتراک به فرش‌باف.
    /// </summary>
    public async Task<TenantDto> CreateAsync(CreateTenantRequest request, CancellationToken ct = default)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();
        if (await tenants.GetBySlugAsync(slug, ct) is not null)
            throw new InvalidOperationException("این شناسه (slug) قبلاً ثبت شده است.");

        var tenant = new Tenant
        {
            Name = request.Name,
            Slug = slug,
            ContactPhone = request.ContactPhone,
            ContactEmail = request.ContactEmail,
            IsActive = true
        };

        await tenants.AddAsync(tenant, ct);

        // Id کارگاه از قبل Guid است؛ نیازی به Save میانی نیست
        var admin = new User
        {
            TenantId = tenant.Id,
            Email = request.AdminEmail.Trim().ToLowerInvariant(),
            FullName = request.AdminFullName,
            Role = UserRole.TenantAdmin,
            PasswordHash = AuthService.HashPassword(request.AdminPassword),
            IsActive = true
        };

        await users.AddAsync(admin, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return tenant.ToDto();
    }

    public async Task<TenantDto> UpdateAsync(Guid id, UpdateTenantRequest request, CancellationToken ct = default)
    {
        var tenant = await tenants.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("کارگاه یافت نشد.");

        tenant.Name = request.Name;
        tenant.IsActive = request.IsActive;
        tenant.ContactPhone = request.ContactPhone;
        tenant.ContactEmail = request.ContactEmail;
        tenant.SubscriptionExpiresAt = request.SubscriptionExpiresAt;
        tenant.UpdatedAt = DateTimeOffset.UtcNow;

        tenants.Update(tenant);
        await unitOfWork.SaveChangesAsync(ct);
        return tenant.ToDto();
    }
}
