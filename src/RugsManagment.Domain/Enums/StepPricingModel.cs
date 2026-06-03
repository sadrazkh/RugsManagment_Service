namespace RugsManagment.Domain.Enums;

/// <summary>
/// روش محاسبهٔ هزینهٔ یک مرحله — در موتور هزینه (CostCalculationService) استفاده می‌شود.
/// </summary>
public enum StepPricingModel
{
    /// <summary>مبلغ ثابت برای کل فرش، مستقل از متراژ</summary>
    Fixed = 0,

    /// <summary>نرخ × مساحت متر مربع (عرض × طول)</summary>
    PerSquareMeter = 1,

    /// <summary>نرخ × مساحت به فوت مربع (برای قراردادهای قدیمی)</summary>
    PerSquareFoot = 2,

    /// <summary>فرمول سفارشی؛ فعلاً همان نرخ پایه برمی‌گردد — قابل توسعه</summary>
    CustomFormula = 3,

    /// <summary>نرخ × طول (متر)</summary>
    PerLength = 4,

    /// <summary>نرخ × عرض (متر)</summary>
    PerWidth = 5,

    /// <summary>جمع چند بند — JSON در PricingConfigJson</summary>
    Combined = 6
}
