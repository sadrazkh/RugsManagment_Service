using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// یک مرحله داخل قالب — مشخص می‌کند در این مسیر چه نوع کاری، با چه ترتیبی، اختیاری یا اجباری است.
/// </summary>
public class WorkflowTemplateStep : BaseEntity
{
    public Guid WorkflowTemplateId { get; set; }
    public Guid ProcessStepTypeId { get; set; }

    /// <summary>ترتیب اجرا: 0 اول، بعد 1، …</summary>
    public int OrderIndex { get; set; }

    /// <summary>اگر true باشد کاربر هنگام ثبت فرش می‌تواند این مرحله را حذف کند (مثلاً دارکشی)</summary>
    public bool IsOptional { get; set; }

    /// <summary>قالیشوی/رفوگر پیش‌فرض برای این مرحله در قالب</summary>
    public Guid? DefaultServiceProviderId { get; set; }

    /// <summary>اگر پر باشد نرخ قالب بر نرخ نوع مرحله غلبه می‌کند</summary>
    public StepPricingModel? OverridePricingModel { get; set; }
    public decimal? OverrideUnitRate { get; set; }

    /// <summary>تنظیمات اضافی JSON برای این مرحله در قالب</summary>
    public string? ConfigJson { get; set; }

    public WorkflowTemplate WorkflowTemplate { get; set; } = null!;
    public ProcessStepType ProcessStepType { get; set; } = null!;
    public ServiceProvider? DefaultServiceProvider { get; set; }
}
