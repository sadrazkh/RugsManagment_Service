using RugsManagment.Domain.Common;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// ارائه‌دهنده خدمات بیرونی (قالیشوی، رفوگر، …) مخصوص یک کارگاه.
/// هنگام پیش بردن مرحله می‌توان این را به مرحلهٔ فرش نسبت داد.
/// </summary>
public class ServiceProvider : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Specialty { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>لیست کدهای مرحله‌ای که این طرف انجام می‌دهد — JSON آرایهٔ رشته</summary>
    public string? SupportedStepTypeCodesJson { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
