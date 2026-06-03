using RugsManagment.Domain.Common;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// قالب مسیر فرایند — مثلاً «مسیر کامل با دارکشی» یا «مسیر سریع».
/// هر کارگاه چند قالب دارد؛ هنگام ثبت فرش یکی انتخاب می‌شود.
/// </summary>
public class WorkflowTemplate : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>قالب پیش‌فرض هنگام ثبت فرش جدید پیشنهاد می‌شود</summary>
    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = null!;

    /// <summary>مراحل این قالب به ترتیب OrderIndex</summary>
    public ICollection<WorkflowTemplateStep> Steps { get; set; } = [];
}
