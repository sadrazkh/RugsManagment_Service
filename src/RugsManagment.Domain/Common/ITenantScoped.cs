namespace RugsManagment.Domain.Common;

/// <summary>
/// موجودیت‌هایی که به یک کارگاه (Tenant) تعلق دارند.
/// با این علامت، در کوئری‌ها فقط دادهٔ همان کارگاه برگردانده می‌شود (جداسازی مشتری‌ها).
/// </summary>
public interface ITenantScoped
{
    /// <summary>شناسه کارگاه مالک این رکورد</summary>
    Guid TenantId { get; set; }
}
