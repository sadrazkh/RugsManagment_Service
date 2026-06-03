using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Abstractions.Services;

/// <summary>محاسبهٔ هزینهٔ یک مرحله — تمام فرمول‌ها متمرکز اینجا</summary>
public interface ICostCalculationService
{
    decimal CalculateStepCost(
        Rug rug,
        ProcessStepType stepType,
        StepPricingModel? overrideModel,
        decimal? overrideRate,
        decimal? manualOverride,
        RugWorkflowStep? stepInstance = null);
}
