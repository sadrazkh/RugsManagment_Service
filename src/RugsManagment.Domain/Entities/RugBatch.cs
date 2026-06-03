using RugsManagment.Domain.Common;

namespace RugsManagment.Domain.Entities;

/// <summary>گروه فرش‌ها برای پیش بردن هم‌زمان مسیر (مثلاً محموله دیروز)</summary>
public class RugBatch : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset? ReceivedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public ICollection<Rug> Rugs { get; set; } = [];
}
