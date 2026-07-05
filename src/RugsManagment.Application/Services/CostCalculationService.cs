using System.Globalization;
using System.Text.Json;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.DTOs.Pricing;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Services;

/// <summary>
/// موتور هزینه — تنها نقطهٔ محاسبهٔ فرمول‌ها (ثابت، متری، طولی، عرضی، ترکیبی، دستی + تخفیف/اضافه).
/// هم مرحلهٔ واقعی و هم پیش‌نمایش UI از همین توابع استفاده می‌کنند تا محاسبه در فرانت تکرار نشود.
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
        return ComputeBase(rug.WidthMeters, rug.LengthMeters, model, rate, stepInstance?.PricingConfigJson);
    }

    public decimal ComputeBase(decimal widthMeters, decimal lengthMeters, StepPricingModel model, decimal rate, string? combinedJson)
    {
        if (model == StepPricingModel.Combined && !string.IsNullOrWhiteSpace(combinedJson))
            return ComputeCombined(widthMeters, lengthMeters, combinedJson);
        return ComputeSingle(widthMeters, lengthMeters, model, rate);
    }

    public PricingPreviewResult Preview(PricingPreviewRequest r)
    {
        var components = new List<PricingComponent>();
        decimal baseCost;
        string formula;

        if (r.ManualCost.HasValue)
        {
            baseCost = r.ManualCost.Value;
            formula = "مبلغ دستی";
            components.Add(new PricingComponent("مبلغ دستی", baseCost));
        }
        else if (r.Model == StepPricingModel.Combined && !string.IsNullOrWhiteSpace(r.CombinedJson))
        {
            var config = SafeDeserialize(r.CombinedJson);
            foreach (var item in config?.Items ?? [])
            {
                var amount = ComputeSingle(r.WidthMeters, r.LengthMeters, item.Model, item.Rate);
                components.Add(new PricingComponent(ComponentLabel(r.WidthMeters, r.LengthMeters, item.Model, item.Rate), amount));
            }
            baseCost = components.Sum(c => c.Amount);
            formula = "جمع اجزا";
        }
        else
        {
            var rate = r.UnitRate ?? 0;
            baseCost = ComputeSingle(r.WidthMeters, r.LengthMeters, r.Model, rate);
            formula = ComponentLabel(r.WidthMeters, r.LengthMeters, r.Model, rate);
            components.Add(new PricingComponent(formula, baseCost));
        }

        var total = Math.Max(0, baseCost + r.Adjustment);
        return new PricingPreviewResult(baseCost, r.Adjustment, total, formula, components);
    }

    // ── هستهٔ فرمول‌ها ──

    private static decimal ComputeSingle(decimal width, decimal length, StepPricingModel model, decimal rate)
    {
        var area = width * length;
        return model switch
        {
            StepPricingModel.Fixed => rate,
            StepPricingModel.PerSquareMeter => Math.Round(rate * area, 2),
            StepPricingModel.PerLength => Math.Round(rate * length, 2),
            StepPricingModel.PerWidth => Math.Round(rate * width, 2),
            StepPricingModel.PerSquareFoot => Math.Round(rate * area * 10.7639m, 2),
            _ => rate
        };
    }

    private static decimal ComputeCombined(decimal width, decimal length, string combinedJson)
    {
        var config = SafeDeserialize(combinedJson);
        if (config?.Items is null or { Count: 0 })
            return 0;
        return config.Items.Sum(item => ComputeSingle(width, length, item.Model, item.Rate));
    }

    private static CombinedPricingConfig? SafeDeserialize(string json)
    {
        try { return JsonSerializer.Deserialize<CombinedPricingConfig>(json, JsonOpts); }
        catch { return null; }
    }

    /// <summary>برچسب خوانا مثل «۸۵٬۰۰۰ × ۶ م² = ۵۱۰٬۰۰۰».</summary>
    private static string ComponentLabel(decimal width, decimal length, StepPricingModel model, decimal rate)
    {
        var area = width * length;
        var (unit, qty) = model switch
        {
            StepPricingModel.PerSquareMeter => ("م²", area),
            StepPricingModel.PerLength => ("م طول", length),
            StepPricingModel.PerWidth => ("م عرض", width),
            StepPricingModel.PerSquareFoot => ("فوت²", Math.Round(area * 10.7639m, 2)),
            _ => (string.Empty, 0m)
        };
        var result = ComputeSingle(width, length, model, rate);
        return model == StepPricingModel.Fixed
            ? $"ثابت {N(rate)}"
            : $"{N(rate)} × {N(qty)} {unit} = {N(result)}";
    }

    private static string N(decimal v) => v.ToString("#,0.##", CultureInfo.InvariantCulture);

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
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
