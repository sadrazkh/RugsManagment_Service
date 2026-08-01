using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// نرخ توافقی یک طرف خدمات برای یک نوع مرحله — مثلاً «قالیشویی نوری: متری ۸۵٬۰۰۰ تومان».
///
/// جای فهرست JSONی «مراحلی که انجام می‌دهد» را گرفته است: هم مشخص می‌کند طرف چه کاری
/// انجام می‌دهد و هم با چه قیمتی. هنگام ثبت مرحله، اگر اپراتور نرخ دستی ندهد
/// همین نرخ به‌صورت خودکار اعمال می‌شود.
/// </summary>
public class ServiceProviderRate : BaseEntity
{
    public Guid ServiceProviderId { get; set; }
    public Guid ProcessStepTypeId { get; set; }

    public StepPricingModel PricingModel { get; set; } = StepPricingModel.PerSquareMeter;

    /// <summary>نرخ واحد (تومان) — معنایش به PricingModel بستگی دارد</summary>
    public decimal UnitRate { get; set; }

    /// <summary>برای مدل ترکیبی — همان ساختار PricingConfigJson مرحله</summary>
    public string? PricingConfigJson { get; set; }

    public string? Notes { get; set; }

    public ServiceProvider ServiceProvider { get; set; } = null!;
    public ProcessStepType ProcessStepType { get; set; } = null!;
}
