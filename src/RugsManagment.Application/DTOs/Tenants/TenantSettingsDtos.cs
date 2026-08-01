using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.DTOs.Tenants;

/// <summary>تنظیمات قابل‌ویرایش توسط خودِ مدیر کارگاه.</summary>
public record TenantSettingsDto(
    Guid Id,
    string Name,
    string Slug,
    string? ContactPhone,
    string? ContactEmail,
    CurrencyUnit Currency,
    string? LogoUrl,
    Guid? DefaultWorkflowTemplateId,
    DateTimeOffset? SubscriptionExpiresAt)
{
    /// <summary>چند روز تا پایان اشتراک؛ null یعنی اشتراک بی‌انتها.</summary>
    public int? DaysUntilExpiry => SubscriptionExpiresAt is null
        ? null
        : (int)Math.Ceiling((SubscriptionExpiresAt.Value - DateTimeOffset.UtcNow).TotalDays);

    public bool IsExpired => SubscriptionExpiresAt is not null && SubscriptionExpiresAt <= DateTimeOffset.UtcNow;
}

public record UpdateTenantSettingsRequest(
    string Name,
    string? ContactPhone,
    string? ContactEmail,
    CurrencyUnit Currency,
    Guid? DefaultWorkflowTemplateId);
