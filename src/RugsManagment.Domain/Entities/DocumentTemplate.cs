using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;
namespace RugsManagment.Domain.Entities;
/// <summary>قالب کارگاه؛ نسخهٔ هر ذخیره برای بازسازی خروجی‌های گذشته نگه داشته می‌شود.</summary>
public class DocumentTemplate : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public DocumentKind Kind { get; set; }
    public string? SeedKey { get; set; }
    public bool IsDefault { get; set; }
    public int Revision { get; set; } = 1;
    public string LayoutJson { get; set; } = "{}";
    public bool UsesMoneyFields { get; set; }
}
