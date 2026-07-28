using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// کاتالوگ انواع مرحله (قالیشویی، دارکشی، رفوگری، …) — در سطح کل سیستم یکسان است.
/// هر کارگاه در قالب خودش از این انواع استفاده می‌کند؛ نرخ پیش‌فرض اینجا تعریف می‌شود.
/// </summary>
public class ProcessStepType : BaseEntity
{
    /// <summary>
    /// null یعنی مرحلهٔ سیستمی که همهٔ کارگاه‌ها می‌بینند؛
    /// مقداردار یعنی مرحلهٔ اختصاصیِ همان کارگاه.
    ///
    /// عمداً ITenantScoped پیاده نشده: این موجودیت گاهی بدون کارگاه معنا دارد
    /// و علامت‌گذاری آن باعث می‌شد فیلترهای عمومی مرحله‌های سیستمی را حذف کنند.
    /// </summary>
    public Guid? TenantId { get; set; }

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

    /// <summary>
    /// اسکیمای فیلدهای اضافی این مرحله — آرایهٔ JSON از تعریف فیلد.
    /// هنگام تکمیل مرحله فرمی از روی آن ساخته می‌شود و مقادیر در
    /// <see cref="RugWorkflowStep.FieldValuesJson"/> ذخیره می‌شوند.
    /// </summary>
    public string? FieldSchemaJson { get; set; }

    /// <summary>
    /// مدت معمول انجام این مرحله (روز). مبنای هشدار کهنگی است:
    /// اگر فرشی بیش از این بماند، در گزارش گلوگاه علامت می‌خورد.
    /// null یعنی از آستانه‌های پیش‌فرض سامانه استفاده شود.
    /// </summary>
    public int? ExpectedDurationDays { get; set; }

    /// <summary>مرحلهٔ غیرفعال در فهرست انتخاب نمایش داده نمی‌شود ولی تاریخچه حفظ می‌ماند</summary>
    public bool IsActive { get; set; } = true;

    public ICollection<WorkflowTemplateStep> TemplateSteps { get; set; } = [];
}
