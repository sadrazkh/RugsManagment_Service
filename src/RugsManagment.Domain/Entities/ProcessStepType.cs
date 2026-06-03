using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// کاتالوگ انواع مرحله (قالیشویی، دارکشی، رفوگری، …) — در سطح کل سیستم یکسان است.
/// هر کارگاه در قالب خودش از این انواع استفاده می‌کند؛ نرخ پیش‌فرض اینجا تعریف می‌شود.
/// </summary>
public class ProcessStepType : BaseEntity
{
    /// <summary>کد انگلیسی یکتا مثل washing — در API و گزارش</summary>
    public string Code { get; set; } = string.Empty;

    public string NameFa { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;

    /// <summary>نام آیکون Material Symbols در فرانت</summary>
    public string Icon { get; set; } = "circle";

    /// <summary>ترتیب نمایش در لیست انتخاب مرحله</summary>
    public int SortOrder { get; set; }

    public StepPricingModel DefaultPricingModel { get; set; } = StepPricingModel.PerSquareMeter;

    /// <summary>نرخ پیش‌فرض (ریال) — بسته به مدل: ثابت یا به ازای m²</summary>
    public decimal DefaultUnitRate { get; set; }

    /// <summary>اسکیمای فیلدهای اضافی مرحله (JSON) — برای فرم‌های داینامیک آینده</summary>
    public string? FieldSchemaJson { get; set; }

    public ICollection<WorkflowTemplateStep> TemplateSteps { get; set; } = [];
}
