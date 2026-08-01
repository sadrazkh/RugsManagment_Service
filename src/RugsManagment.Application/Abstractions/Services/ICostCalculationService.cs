using RugsManagment.Application.DTOs.Pricing;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Abstractions.Services;

/// <summary>محاسبهٔ هزینهٔ یک مرحله — تمام فرمول‌ها متمرکز اینجا (فقط یک نقطهٔ محاسبه)</summary>
public interface ICostCalculationService
{
    decimal CalculateStepCost(
        Rug rug,
        ProcessStepType stepType,
        StepPricingModel? overrideModel,
        decimal? overrideRate,
        decimal? manualOverride,
        RugWorkflowStep? stepInstance = null);

    /// <summary>محاسبهٔ خام از روی ابعاد و مدل — بدون نیاز به موجودیت فرش (برای پیش‌نمایش و مرحله).</summary>
    decimal ComputeBase(decimal widthMeters, decimal lengthMeters, StepPricingModel model, decimal rate, string? combinedJson);

    /// <summary>پیش‌نمایش کامل هزینه با فرمول خوانا و اجزا — بدون ذخیره.</summary>
    PricingPreviewResult Preview(PricingPreviewRequest request);
}
