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

    /// <summary>کاربری که این مرحله را تکمیل کرد — «چه کسی این کار را انجام داد»</summary>
    public Guid? CompletedByUserId { get; set; }

    /// <summary>نام کاربر در لحظهٔ تکمیل؛ کپی می‌شود تا با حذف کاربر تاریخچه خالی نشود</summary>
    public string? CompletedByName { get; set; }

    /// <summary>هزینه محاسبه‌شده توسط سیستم</summary>
    public decimal? CalculatedCost { get; set; }

    /// <summary>اگر اپراتور مبلغ دستی وارد کند، این مقدار جایگزین CalculatedCost در گزارش می‌شود</summary>
    public decimal? ManualCostOverride { get; set; }

    public StepPricingModel? AppliedPricingModel { get; set; }
    public decimal? AppliedUnitRate { get; set; }
    public string? PricingConfigJson { get; set; }

    /// <summary>تخفیف (منفی) یا هزینهٔ اضافه (مثبت) که به نتیجهٔ فرمول اعمال می‌شود</summary>
    public decimal? Adjustment { get; set; }

    /// <summary>مقادیر فرم داینامیک مرحله (JSON)</summary>
    public string? FieldValuesJson { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// عکس اندازه و تعدادی که مرحله با آن قیمت خورد (spec B.5). قیمت دوباره همین را به کار می‌برد مگر کاربر
    /// «با اندازهٔ تازه» را بخواهد؛ پس عوض شدن اندازهٔ نهایی بعد از شستشو، قیمت شستشو را بی‌صدا عوض نمی‌کند.
    /// null یعنی مرحله هنوز با این قاعده قیمت نخورده (مرحله‌های قدیمی).
    /// </summary>
    public decimal? PricedLength { get; set; }

    /// <summary>عرضِ عکس قیمت‌گذاری (numeric(9,2)).</summary>
    public decimal? PricedWidth { get; set; }

    /// <summary>تعدادِ عکس قیمت‌گذاری؛ قیمت ردیف = قیمت یک تخته × همین.</summary>
    public int? PricedQuantity { get; set; }

    public Rug Rug { get; set; } = null!;
    public ProcessStepType ProcessStepType { get; set; } = null!;
    public ServiceProvider? ServiceProvider { get; set; }

    /// <summary>عکس اندازه برای قیمت‌گذاری گرفته شده است.</summary>
    public bool HasPricingSnapshot => PricedLength is not null && PricedWidth is not null && PricedQuantity is not null;

    /// <summary>عکس اندازه و تعداد را می‌گیرد (هنگام قیمت‌گذاری، یا «با اندازهٔ تازه»).</summary>
    public void SnapshotPricingSize(decimal length, decimal width, int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);
        PricedLength = Num.Round(length, AreaMath.DimensionDecimals);
        PricedWidth = Num.Round(width, AreaMath.DimensionDecimals);
        PricedQuantity = quantity;
    }

    /// <summary>
    /// هزینهٔ مؤثر: (دستی یا محاسبه‌شده) + تخفیف/اضافه؛ هرگز منفی نمی‌شود. فرمول در
    /// <see cref="Costing.RugCostMath.StepEffectiveCost"/> است (تنها جای C#؛ همتای SQL در CostSql).
    /// </summary>
    public decimal EffectiveCost => Costing.RugCostMath.StepEffectiveCost(CalculatedCost, ManualCostOverride, Adjustment);
}
