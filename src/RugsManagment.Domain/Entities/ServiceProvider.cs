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

    /// <summary>یادداشت داخلی دربارهٔ این طرف (شرایط همکاری، تاریخچه و…)</summary>
    public string? Notes { get; set; }

    // فیلد قدیمی SupportedStepTypeCodesJson حذف شد: جدول Rates هم می‌گوید چه کاری
    // انجام می‌دهد و هم با چه نرخی — نگه داشتن هر دو باعث ناهماهنگی می‌شد.

    /// <summary>
    /// نرخ توافقی برای هر نوع مرحله. هم نقش «چه کاری انجام می‌دهد» را دارد
    /// و هم «با چه قیمتی» — جایگزین فهرست JSONی قبلی.
    /// </summary>
    public ICollection<ServiceProviderRate> Rates { get; set; } = [];

    /// <summary>پرداخت‌های انجام‌شده به این طرف — پایهٔ محاسبهٔ مانده‌حساب</summary>
    public ICollection<ProviderPayment> Payments { get; set; } = [];

    public Tenant Tenant { get; set; } = null!;
}
