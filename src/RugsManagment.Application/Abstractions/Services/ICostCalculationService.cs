using RugsManagment.Application.DTOs.Pricing;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Abstractions.Services;

/// <summary>
/// قیمت یک مرحله — روش و نرخ از کجا می‌آید (مرحله، جایگزین، پیش‌فرض نوع مرحله). خودِ فرمول‌ها (قاعدهٔ دسته، گرد کردن
/// نصفه-دور-از-صفر در MoneyScale) فقط در <c>Domain/Costing/RugCostMath</c> اند.
/// </summary>
public interface ICostCalculationService
{
    /// <summary>
    /// هزینهٔ محاسبه‌شدهٔ یک مرحله (spec D.3 «Step pricing on lots»): قیمت یک تخته روی عکس اندازهٔ قیمت‌گذاری
    /// (<c>Priced*</c> مرحله؛ بی عکس، اندازهٔ مؤثر و تعداد فعلی فرش) در MoneyScale گرد و بعد × تعداد. مبلغ دستی برای کل ردیف
    /// است و همان برمی‌گردد.
    /// </summary>
    decimal CalculateStepCost(
        Rug rug,
        ProcessStepType stepType,
        StepPricingModel? overrideModel,
        decimal? overrideRate,
        decimal? manualOverride,
        RugWorkflowStep? stepInstance = null,
        int moneyScale = 0);

    /// <summary>
    /// قیمت خام از روی ابعاد و مدل — بدون نیاز به موجودیت فرش: قیمت یک تخته (گرد در <paramref name="moneyScale"/>) ×
    /// <paramref name="quantity"/>. ترکیبی: جمع بندها، هر بند جدا گرد؛ JSON خراب صفر.
    /// </summary>
    decimal ComputeBase(
        decimal widthMeters, decimal lengthMeters, StepPricingModel model, decimal rate, string? combinedJson,
        int quantity = 1, int moneyScale = 0);

    /// <summary>پیش‌نمایش کامل هزینه با فرمول خوانا و اجزا — بدون ذخیره (یک تخته، در MoneyScale کارگاه جاری).</summary>
    PricingPreviewResult Preview(PricingPreviewRequest request);
}
