using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.DTOs.Pricing;

/// <summary>ورودی پیش‌نمایش هزینه — بدون ذخیره، فقط محاسبه در بک‌اند.</summary>
public record PricingPreviewRequest(
    decimal WidthMeters,
    decimal LengthMeters,
    StepPricingModel Model,
    decimal? UnitRate,
    string? CombinedJson,
    decimal? ManualCost,
    decimal Adjustment = 0);

/// <summary>یک جزء از فرمول ترکیبی (مثلاً «متری × ۶ = ...»).</summary>
public record PricingComponent(string Label, decimal Amount);

/// <summary>نتیجهٔ محاسبه: مبنا + تعدیل = مجموع، همراه فرمول خوانا و اجزا.</summary>
public record PricingPreviewResult(
    decimal Base,
    decimal Adjustment,
    decimal Total,
    string Formula,
    IReadOnlyList<PricingComponent> Components);
