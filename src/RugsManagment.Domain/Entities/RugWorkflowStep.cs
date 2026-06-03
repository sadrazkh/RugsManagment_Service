using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// یک مرحلهٔ واقعی روی یک فرش — از قالب کپی شده و سپس مستقل پیش می‌رود.
/// مثلاً «قالیشویی فرش SKU-123» با وضعیت InProgress.
/// </summary>
public class RugWorkflowStep : BaseEntity
{
    public Guid RugId { get; set; }
    public Guid ProcessStepTypeId { get; set; }
    public int OrderIndex { get; set; }
    public bool IsOptional { get; set; }

    public WorkflowStepStatus Status { get; set; } = WorkflowStepStatus.Pending;

    /// <summary>طرفی که این مرحله را انجام می‌دهد (اختیاری)</summary>
    public Guid? ServiceProviderId { get; set; }

    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>هزینه محاسبه‌شده توسط سیستم</summary>
    public decimal? CalculatedCost { get; set; }

    /// <summary>اگر اپراتور مبلغ دستی وارد کند، این مقدار جایگزین CalculatedCost در گزارش می‌شود</summary>
    public decimal? ManualCostOverride { get; set; }

    public StepPricingModel? AppliedPricingModel { get; set; }
    public decimal? AppliedUnitRate { get; set; }
    public string? PricingConfigJson { get; set; }

    /// <summary>مقادیر فرم داینامیک مرحله (JSON)</summary>
    public string? FieldValuesJson { get; set; }
    public string? Notes { get; set; }

    public Rug Rug { get; set; } = null!;
    public ProcessStepType ProcessStepType { get; set; } = null!;
    public ServiceProvider? ServiceProvider { get; set; }

    /// <summary>هزینهٔ مؤثر برای جمع‌زنی: اول دستی، بعد محاسبه‌شده، وگرنه صفر</summary>
    public decimal EffectiveCost => ManualCostOverride ?? CalculatedCost ?? 0;
}
