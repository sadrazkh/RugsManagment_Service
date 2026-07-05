using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// موجودیت اصلی: یک فرش مشخص در یک کارگاه — با ابعاد، هزینه خرید، و مسیر فرایند نمونه‌برداری‌شده.
/// </summary>
public class Rug : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>کد انبار خودکار مثل RUG-202606-0001</summary>
    public string Sku { get; set; } = string.Empty;

    public string? Title { get; set; }
    public string? Origin { get; set; }
    public string? Pattern { get; set; }
    public string? Material { get; set; }
    public int? KnotDensity { get; set; }

    public decimal WidthMeters { get; set; }
    public decimal LengthMeters { get; set; }

    public decimal? PurchaseCost { get; set; }
    public decimal? TargetSalePrice { get; set; }

    public RugStatus Status { get; set; } = RugStatus.Draft;
    public string? ImageUrl { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// مقادیر فیلدهای سفارشی این کارگاه به‌صورت JSON (ستون jsonb).
    /// کلیدها از CustomFieldDefinition.Key می‌آیند — انعطاف NoSQL‌گونه بدون تغییر اسکیمـا.
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>اگر از قالب ساخته شده؛ null یعنی مسیر کاملاً سفارشی</summary>
    public Guid? WorkflowTemplateId { get; set; }

    /// <summary>گروه انبار/محموله — برای عملیات گروهی</summary>
    public Guid? BatchId { get; set; }

    /// <summary>شاخص مرحلهٔ جاری در مسیر (برای UI)</summary>
    public int CurrentStepIndex { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public RugBatch? Batch { get; set; }
    public WorkflowTemplate? WorkflowTemplate { get; set; }

    /// <summary>کپی زندهٔ مراحل این فرش — با پیش بردن مرحله وضعیت‌ها عوض می‌شود</summary>
    public ICollection<RugWorkflowStep> WorkflowSteps { get; set; } = [];

    /// <summary>مساحت متر مربع — فقط محاسباتی، در دیتابیس ذخیره نمی‌شود</summary>
    public decimal AreaSquareMeters => WidthMeters * LengthMeters;
}
