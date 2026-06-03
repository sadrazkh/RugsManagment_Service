namespace RugsManagment.Application.DTOs.Tenants;

public record TenantDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    string? ContactPhone,
    string? ContactEmail,
    DateTimeOffset? SubscriptionExpiresAt);

/// <summary>ایجاد کارگاه + اولین مدیر — فقط ادمین سیستم</summary>
public record CreateTenantRequest(
    string Name,
    string Slug,
    string AdminEmail,
    string AdminPassword,
    string AdminFullName,
    string? ContactPhone,
    string? ContactEmail);

public record UpdateTenantRequest(
    string Name,
    bool IsActive,
    string? ContactPhone,
    string? ContactEmail,
    DateTimeOffset? SubscriptionExpiresAt);
