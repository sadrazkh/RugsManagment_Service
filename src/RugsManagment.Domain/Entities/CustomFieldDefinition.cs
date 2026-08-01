using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// تعریف یک فیلد سفارشی برای فرش‌های یک کارگاه (مثلاً «رنگ زمینه» یا «شماره طاقه»).
/// مقدارِ هر فرش در Rug.MetadataJson با کلید Key ذخیره می‌شود.
/// </summary>
public class CustomFieldDefinition : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>کلید یکتا در JSON متادیتا (انگلیسی/بدون فاصله)</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>برچسب نمایشی فارسی</summary>
    public string Label { get; set; } = string.Empty;

    public CustomFieldType FieldType { get; set; } = CustomFieldType.Text;

    /// <summary>برای نوع Select — آرایهٔ JSON از گزینه‌ها</summary>
    public string? OptionsJson { get; set; }

    public bool IsRequired { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = null!;
}
