using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.DTOs.Pricing;

/// <summary>ورودی پیش‌نمایش هزینه — بدون ذخیره، فقط محاسبه در بک‌اند.</summary>
/// <param name="WidthMeters">عرض یک تخته: عکس قیمت‌گذاری مرحله (<c>PricedWidth</c>) اگر هست، وگرنه عرض مؤثر فرش.</param>
/// <param name="LengthMeters">طول یک تخته، به همان قاعدهٔ عرض.</param>
/// <param name="Quantity">
/// تعداد تخته‌هایی که مرحله رویشان قیمت می‌خورد (<c>PricedQuantity</c>، وگرنه تعداد فرش). مرحلهٔ واقعی قیمت یک تخته × تعداد
/// است و تعدیل و کف صفر روی کل ردیف می‌آیند (D.3)؛ پیش‌نمایش بی تعداد برای دسته‌ای ۵۰تایی یک‌پنجاهم هزینه را «نهایی» نشان
/// می‌داد. نبودن یا کمتر از ۱ یعنی یک تخته (رفتار پیشین).
/// </param>
public record PricingPreviewRequest(
    decimal WidthMeters,
    decimal LengthMeters,
    StepPricingModel Model,
    decimal? UnitRate,
    string? CombinedJson,
    decimal? ManualCost,
    decimal Adjustment = 0,
    int Quantity = 1);

/// <summary>یک جزء از فرمول ترکیبی (مثلاً «متری × ۶ = ...»).</summary>
public record PricingComponent(string Label, decimal Amount);

/// <summary>نتیجهٔ محاسبه: مبنا + تعدیل = مجموع، همراه فرمول خوانا و اجزا.</summary>
public record PricingPreviewResult(
    decimal Base,
    decimal Adjustment,
    decimal Total,
    string Formula,
    IReadOnlyList<PricingComponent> Components);
