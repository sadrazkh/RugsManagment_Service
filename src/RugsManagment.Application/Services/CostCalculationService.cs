using System.Globalization;
using System.Text.Json;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.DTOs.Pricing;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Costing;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Services;

/// <summary>
/// قیمت مرحله: کدام روش و نرخ (مرحله ← جایگزین ← پیش‌فرض نوع مرحله)، روی کدام اندازه (عکس قیمت‌گذاری <c>Priced*</c>،
/// وگرنه اندازهٔ مؤثر و تعداد فرش)، و در کدام MoneyScale. خودِ فرمول‌ها فقط در <see cref="RugCostMath"/> اند (همتای C#ِ
/// CostSql؛ آزمون معماری «یک جای فرمول»)، پس مرحلهٔ واقعی، پیش‌نمایش و پاس سازگاری یک عدد می‌دهند.
///
/// <para><b>از فاز ۲ (spec D.3):</b> قیمت اول برای یک تخته با گرد کردن نصفه-دور-از-صفر در MoneyScale کارگاه و بعد × تعداد؛
/// جای «Math.Round» بانکی دو رقمی و ضریب تقریبی فوت مربع پیشین. مبلغ دستی و تعدیل برای کل ردیف‌اند و هرگز × تعداد نمی‌شوند.</para>
/// </summary>
/// <param name="currentUser">کاربر جاری، فقط برای MoneyScale پیش‌نمایش (بی آن ۰).</param>
/// <param name="scales">خوانندهٔ MoneyScale کارگاه (بی آن ۰).</param>
public sealed class CostCalculationService(
    ICurrentUser? currentUser = null,
    IRugCostTotalReader? scales = null) : ICostCalculationService
{
    public decimal CalculateStepCost(
        Rug rug,
        ProcessStepType stepType,
        StepPricingModel? overrideModel,
        decimal? overrideRate,
        decimal? manualOverride,
        RugWorkflowStep? stepInstance = null,
        int moneyScale = 0)
    {
        ArgumentNullException.ThrowIfNull(rug);
        ArgumentNullException.ThrowIfNull(stepType);

        if (manualOverride.HasValue)
            return manualOverride.Value;

        var model = stepInstance?.AppliedPricingModel ?? overrideModel ?? stepType.DefaultPricingModel;
        var rate = stepInstance?.AppliedUnitRate ?? overrideRate ?? stepType.DefaultUnitRate;

        // عکس اندازهٔ قیمت‌گذاری (B.5)؛ مرحلهٔ بی‌عکس (قدیمی یا پیش‌برآورد مسیر) روی اندازه و تعداد فعلی فرش
        var (length, width, quantity) = stepInstance is { HasPricingSnapshot: true } priced
            ? (priced.PricedLength!.Value, priced.PricedWidth!.Value, priced.PricedQuantity!.Value)
            : (rug.LengthMeters, rug.WidthMeters, Math.Max(1, rug.Quantity));

        return ComputeBase(width, length, model, rate, stepInstance?.PricingConfigJson, quantity, moneyScale);
    }

    public decimal ComputeBase(
        decimal widthMeters, decimal lengthMeters, StepPricingModel model, decimal rate, string? combinedJson,
        int quantity = 1, int moneyScale = 0)
    {
        IReadOnlyList<StepPricingItem>? items = null;
        if (model == StepPricingModel.Combined && !string.IsNullOrWhiteSpace(combinedJson))
        {
            var config = SafeDeserialize(combinedJson);
            // پیکربندی خراب نباید ثبت مرحله را بشکند؛ صفر (رفتار پیشین)
            if (config?.Items is null)
                return 0m;
            items = config.Items.Select(i => new StepPricingItem(i.Model, i.Rate)).ToList();
        }

        return RugCostMath.StepLinePrice(model, rate, lengthMeters, widthMeters, Math.Max(1, quantity), moneyScale, items);
    }

    public PricingPreviewResult Preview(PricingPreviewRequest r)
    {
        ArgumentNullException.ThrowIfNull(r);
        var scale = CurrentScale();
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
                var itemModel = item.Model == StepPricingModel.Combined ? StepPricingModel.Fixed : item.Model;
                var amount = RugCostMath.StepPiecePrice(itemModel, item.Rate, r.LengthMeters, r.WidthMeters, scale);
                components.Add(new PricingComponent(ComponentLabel(r.WidthMeters, r.LengthMeters, itemModel, item.Rate, amount), amount));
            }
            baseCost = components.Sum(c => c.Amount);
            formula = "جمع اجزا";
        }
        else
        {
            var rate = r.UnitRate ?? 0;
            baseCost = RugCostMath.StepPiecePrice(r.Model, rate, r.LengthMeters, r.WidthMeters, scale);
            formula = ComponentLabel(r.WidthMeters, r.LengthMeters, r.Model, rate, baseCost);
            components.Add(new PricingComponent(formula, baseCost));
        }

        // همان کف صفرِ هزینهٔ مؤثر مرحله (RugCostMath.StepEffectiveCost)
        var total = RugCostMath.StepEffectiveCost(baseCost, null, r.Adjustment);
        return new PricingPreviewResult(baseCost, r.Adjustment, total, formula, components);
    }

    // ── جزئیات ──

    private int CurrentScale()
        => scales is not null && currentUser?.TenantId is Guid tenantId ? scales.MoneyScale(tenantId) : 0;

    private static CombinedPricingConfig? SafeDeserialize(string json)
    {
        try { return JsonSerializer.Deserialize<CombinedPricingConfig>(json, JsonOpts); }
        catch (JsonException) { return null; }
    }

    /// <summary>برچسب خوانا مثل «۸۵٬۰۰۰ × ۶ م² = ۵۱۰٬۰۰۰» (عدد با فرهنگ ثابت؛ جزیره ارقام را فارسی می‌کند).</summary>
    private static string ComponentLabel(decimal width, decimal length, StepPricingModel model, decimal rate, decimal result)
    {
        var area = AreaMath.PieceArea(length, width);
        var (unit, quantity) = model switch
        {
            StepPricingModel.PerSquareMeter => ("م²", area),
            StepPricingModel.PerLength => ("م طول", Num.Round(length, AreaMath.DimensionDecimals)),
            StepPricingModel.PerWidth => ("م عرض", Num.Round(width, AreaMath.DimensionDecimals)),
            StepPricingModel.PerSquareFoot => ("فوت²", Num.Round(AreaMath.ToSquareFeet(area), 2)),
            _ => (string.Empty, 0m)
        };
        return unit.Length == 0
            ? $"ثابت {N(rate)}"
            : $"{N(rate)} × {N(quantity)} {unit} = {N(result)}";
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
