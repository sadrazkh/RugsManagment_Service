using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.DTOs.Rugs;

/// <summary>
/// <c>GET /api/rugs/{id}/cost-breakdown</c> (Business): «این عدد از کجا آمد؟» — هر سه منبع پول فرش (D.1) سطر به سطر،
/// و جمعشان. جمع سطرها باید با <see cref="Summary"/> یکی باشد، چون هر دو از همان قاعده‌ها (<c>RugCostMath</c>/CostSql)
/// می‌آیند؛ اگر نبود، <c>LedgerVerifier</c> همان را ناهمخوانی گزارش می‌کند.
/// </summary>
/// <param name="Purchase">سطرهای خرید خودِ فرش.</param>
/// <param name="Steps">مراحل فرایند با سهم هر کدام در <see cref="RugCostSummaryDto.StepCost"/>.</param>
/// <param name="Allocations">سهم این فرش از ثبت‌های هزینه (خرید یکجا و هزینه‌ها)؛ تا فاز هزینه‌ها همیشه خالی.</param>
/// <param name="Summary">همان خلاصهٔ <see cref="RugDto.Costs"/> (از <c>RugCostTotal</c>).</param>
public record RugCostBreakdownDto(
    IReadOnlyList<PurchaseLineDto> Purchase,
    IReadOnlyList<StepCostDto> Steps,
    IReadOnlyList<AllocationLineDto> Allocations,
    RugCostSummaryDto Summary);

/// <summary>
/// یک مرحلهٔ فرایند در ریز هزینه. چرا هر دو مبلغ: <see cref="EffectiveCost"/> مبلغ مؤثر مرحله است (همان
/// <c>RugCostMath.StepEffectiveCost</c>، با کف صفر حتی وقتی تعدیل منفی بزرگ‌تر از مبلغ است)، ولی فقط مرحلهٔ «انجام‌شده» یا
/// «در حال انجام» در بها حساب می‌شود (D.1)؛ <see cref="CountedCost"/> همان سهم واقعی است تا جمع ستون با
/// <see cref="RugCostSummaryDto.StepCost"/> بخواند.
/// </summary>
/// <param name="StepId">شناسهٔ مرحلهٔ فرش.</param>
/// <param name="ProcessStepTypeId">نوع مرحله.</param>
/// <param name="StepNameFa">نام فارسی مرحله.</param>
/// <param name="OrderIndex">ترتیب در مسیر.</param>
/// <param name="Status">وضعیت مرحله.</param>
/// <param name="ServiceProviderName">انجام‌دهنده؛ null یعنی ندارد.</param>
/// <param name="AppliedPricingModel">نام روش قیمت‌گذاری اعمال‌شده (مثل «PerSquareMeter»)؛ null یعنی قیمت‌گذاری نشده.</param>
/// <param name="AppliedUnitRate">نرخ اعمال‌شده.</param>
/// <param name="PricedLength">طولِ عکس قیمت‌گذاری (B.5)؛ null یعنی عکس ندارد.</param>
/// <param name="PricedWidth">عرضِ عکس قیمت‌گذاری.</param>
/// <param name="PricedQuantity">تعدادِ عکس قیمت‌گذاری (قاعدهٔ دسته، D.3).</param>
/// <param name="EffectiveCost">مبلغ مؤثر مرحله = max(۰، (مبلغ دستی ?? محاسبه‌شده ?? ۰) + تعدیل).</param>
/// <param name="CountedCost">سهم در بها: برای «انجام‌شده»/«در حال انجام» همان <paramref name="EffectiveCost"/>، وگرنه ۰.</param>
public record StepCostDto(
    Guid StepId,
    Guid ProcessStepTypeId,
    string StepNameFa,
    int OrderIndex,
    WorkflowStepStatus Status,
    string? ServiceProviderName,
    string? AppliedPricingModel,
    decimal? AppliedUnitRate,
    decimal? PricedLength,
    decimal? PricedWidth,
    int? PricedQuantity,
    decimal EffectiveCost,
    decimal CountedCost);

/// <summary>
/// سهم این فرش از یک ثبت هزینه (فاز هزینه‌ها پر می‌کند؛ از فاز ۲ شکلش ثابت است تا ریز هزینه بعداً تغییر قرارداد نخواهد).
/// </summary>
/// <param name="EntryId">شناسهٔ ثبت هزینه.</param>
/// <param name="EntryNumber">شمارهٔ ثبت (شمارندهٔ cost).</param>
/// <param name="Category">نام نوع هزینه.</param>
/// <param name="Group">خرید (در <see cref="RugCostSummaryDto.PurchaseCost"/>) یا هزینه (در <see cref="RugCostSummaryDto.ExpenseCost"/>).</param>
/// <param name="Date">تاریخ ثبت.</param>
/// <param name="Amount">سهم این فرش به ارز پایه.</param>
/// <param name="WeightUnits">وزن این فرش در تقسیم (واحد صحیح، مثل سانتی‌متر مربع).</param>
/// <param name="TotalWeightUnits">جمع وزن همهٔ اعضا.</param>
/// <param name="Method">روش تقسیم.</param>
/// <param name="IsPinned">مبلغ ثابت دستی (pin) است، نه حاصل تقسیم.</param>
/// <param name="EntryBase">کل مبلغ ثبت به ارز پایه.</param>
public record AllocationLineDto(
    Guid EntryId,
    int EntryNumber,
    string Category,
    CostGroup Group,
    DateOnly Date,
    decimal Amount,
    long WeightUnits,
    long TotalWeightUnits,
    AllocationMethod Method,
    bool IsPinned,
    decimal EntryBase);
