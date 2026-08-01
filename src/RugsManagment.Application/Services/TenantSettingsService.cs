using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Tenants;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Services;

/// <summary>تنظیمات کارگاه که خودِ مدیر کارگاه (نه ادمین سیستم) مدیریت می‌کند.</summary>
public interface ITenantSettingsService
{
    Task<TenantSettingsDto> GetAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantSettingsDto> UpdateAsync(Guid tenantId, UpdateTenantSettingsRequest request, CancellationToken ct = default);

    /// <summary>ذخیرهٔ نام فایل لوگو بعد از آپلود؛ null یعنی حذف لوگو.</summary>
    Task<TenantSettingsDto> SetLogoAsync(Guid tenantId, string? fileName, CancellationToken ct = default);
}

public sealed class TenantSettingsService(
    ITenantRepository tenants,
    IAuditLog audit,
    IUnitOfWork unitOfWork) : ITenantSettingsService
{
    public async Task<TenantSettingsDto> GetAsync(Guid tenantId, CancellationToken ct = default)
        => ToDto(await LoadAsync(tenantId, ct));

    public async Task<TenantSettingsDto> UpdateAsync(
        Guid tenantId, UpdateTenantSettingsRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("نام کارگاه الزامی است.");

        var tenant = await LoadAsync(tenantId, ct);

        // تغییر واحد پول هیچ عددی را تبدیل نمی‌کند؛ در UI هم صریح هشدار داده می‌شود
        var currencyChanged = tenant.Currency != request.Currency;

        tenant.Name = PersianText.Normalize(request.Name.Trim());
        tenant.ContactPhone = Clean(request.ContactPhone);
        tenant.ContactEmail = Clean(request.ContactEmail);
        tenant.Currency = request.Currency;
        tenant.DefaultWorkflowTemplateId = request.DefaultWorkflowTemplateId;
        tenant.UpdatedAt = DateTimeOffset.UtcNow;

        tenants.Update(tenant);

        audit.Record(
            AuditAction.SettingsChanged,
            nameof(Tenant),
            tenant.Id,
            currencyChanged
                ? $"تنظیمات کارگاه به‌روزرسانی شد (واحد پول به {CurrencyLabel(request.Currency)} تغییر کرد)."
                : "تنظیمات کارگاه به‌روزرسانی شد.",
            tenant.Name);

        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(tenant);
    }

    public async Task<TenantSettingsDto> SetLogoAsync(
        Guid tenantId, string? fileName, CancellationToken ct = default)
    {
        var tenant = await LoadAsync(tenantId, ct);

        tenant.LogoFileName = fileName;
        tenant.UpdatedAt = DateTimeOffset.UtcNow;
        tenants.Update(tenant);

        audit.Record(
            AuditAction.SettingsChanged,
            nameof(Tenant),
            tenant.Id,
            fileName is null ? "لوگوی کارگاه حذف شد." : "لوگوی کارگاه تغییر کرد.",
            tenant.Name);

        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(tenant);
    }

    // ─────────────────────────────────────────────────────────
    private async Task<Tenant> LoadAsync(Guid tenantId, CancellationToken ct)
        => await tenants.GetByIdAsync(tenantId, ct)
           ?? throw new KeyNotFoundException("کارگاه یافت نشد.");

    private static TenantSettingsDto ToDto(Tenant t) => new(
        t.Id,
        t.Name,
        t.Slug,
        t.ContactPhone,
        t.ContactEmail,
        t.Currency,
        // لوگو مثل عکس فرش با بررسی مالکیت کارگاه سرو می‌شود، نه از wwwroot
        t.LogoFileName is null ? null : $"/media/tenant/{t.LogoFileName}",
        t.DefaultWorkflowTemplateId,
        t.SubscriptionExpiresAt);

    private static string CurrencyLabel(CurrencyUnit unit) => unit == CurrencyUnit.Rial ? "ریال" : "تومان";

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : PersianText.Normalize(value.Trim());
}
