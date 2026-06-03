using System.Text.Json;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Services;

/// <summary>
/// محاسبهٔ هزینهٔ یک مرحله — تمام فرمول‌ها فقط اینجا هستند تا در فرانت تکرار نشود.
/// </summary>
public sealed class CostCalculationService : ICostCalculationService
{
    public decimal CalculateStepCost(
        Rug rug,
        ProcessStepType stepType,
        StepPricingModel? overrideModel,
        decimal? overrideRate,
        decimal? manualOverride,
        RugWorkflowStep? stepInstance = null)
    {
        if (manualOverride.HasValue)
            return manualOverride.Value;

        var model = stepInstance?.AppliedPricingModel ?? overrideModel ?? stepType.DefaultPricingModel;
        var rate = stepInstance?.AppliedUnitRate ?? overrideRate ?? stepType.DefaultUnitRate;
        var configJson = stepInstance?.PricingConfigJson;

        if (model == StepPricingModel.Combined && !string.IsNullOrWhiteSpace(configJson))
            return CalculateCombined(rug, configJson);

        return CalculateSingle(rug, model, rate);
    }

    private static decimal CalculateSingle(Rug rug, StepPricingModel model, decimal rate) =>
        model switch
        {
            StepPricingModel.Fixed => rate,
            StepPricingModel.PerSquareMeter => Math.Round(rate * rug.AreaSquareMeters, 2),
            StepPricingModel.PerLength => Math.Round(rate * rug.LengthMeters, 2),
            StepPricingModel.PerWidth => Math.Round(rate * rug.WidthMeters, 2),
            StepPricingModel.PerSquareFoot => Math.Round(rate * rug.AreaSquareMeters * 10.7639m, 2),
            _ => rate
        };

    private static decimal CalculateCombined(Rug rug, string configJson)
    {
        try
        {
            var config = JsonSerializer.Deserialize<CombinedPricingConfig>(configJson);
            if (config?.Items is null or { Count: 0 })
                return 0;
            return config.Items.Sum(item => CalculateSingle(rug, item.Model, item.Rate));
        }
        catch
        {
            return 0;
        }
    }
}

public sealed class CombinedPricingConfig
{
    public List<CombinedPricingItem> Items { get; set; } = [];
}

public sealed class CombinedPricingItem
{
    public StepPricingModel Model { get; set; }
    public decimal Rate { get; set; }
}
