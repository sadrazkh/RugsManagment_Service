using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Abstractions.Services;

/// <summary>
/// قرارداد موتور گردش کار — منطق «کدام مرحله فعال است و چطور جلو می‌رود» بدون وابستگی به EF.
/// </summary>
public interface IWorkflowEngine
{
    Task<Rug> InitializeWorkflowFromTemplateAsync(
        Rug rug,
        WorkflowTemplate template,
        IReadOnlyList<Guid>? skippedOptionalStepIds,
        CancellationToken cancellationToken = default);

    Task<Rug> BuildCustomWorkflowAsync(
        Rug rug,
        IReadOnlyList<CustomWorkflowStepRequest> steps,
        CancellationToken cancellationToken = default);

    Task<RugWorkflowStep> AdvanceStepAsync(
        Rug rug,
        Guid stepId,
        AdvanceStepRequest request,
        CancellationToken cancellationToken = default);

    Task<RugWorkflowStep> UpdateStepPricingAsync(
        Rug rug,
        Guid stepId,
        AdvanceStepRequest request,
        CancellationToken cancellationToken = default);

    Task<RugWorkflowStep> SkipStepAsync(Rug rug, Guid stepId, CancellationToken cancellationToken = default);

    Task<Rug> GoBackStepAsync(Rug rug, CancellationToken cancellationToken = default);

    Task<Rug> ActivateStepAsync(Rug rug, Guid stepId, CancellationToken cancellationToken = default);

    /// <summary>مراحل تکمیل‌شده/ردشده حفظ می‌شود؛ بقیه با لیست جدید جایگزین می‌شود</summary>
    Task<Rug> UpdateWorkflowPathAsync(
        Rug rug,
        IReadOnlyList<CustomWorkflowStepRequest> pendingSteps,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// بهای تمام‌شدهٔ فرش برای نمایش (جای ۱ از هفت جای «سرمایهٔ کل»، spec D.4): از ردیف <c>RugCostTotal</c> که پاس سازگاری
    /// نوشته (بعد از ذخیره همان عدد تازه)؛ اگر ردیف هنوز ساخته نشده، همان قاعده‌ها (<c>RugCostMath</c>) روی گراف درون‌حافظه
    /// (سطرهای خرید و مراحل، بی سهم ثبت‌ها) و <see cref="RugCostSummary.Computed"/> = false.
    /// </summary>
    RugCostSummary CalculateRugCosts(Rug rug);

    /// <summary>
    /// وضعیتی که فقط از مراحل می‌آید (بدون تغییر خود مراحل): بی‌مرحله → ثبت‌شده، همه بسته → آمادهٔ فروش، وگرنه در جریان.
    /// جایی لازم است که وضعیت از بیرونِ گردش کار عوض شده و باید برگردد — مثلاً وقتی فروش یک فرش لغو می‌شود. تقدم
    /// «فروخته‌شده» و «صادر شده» در <c>RugStatusResolver</c> است.
    /// </summary>
    Domain.Enums.RugStatus ResolveStatusFromWorkflow(Rug rug);

    /// <summary>
    /// نسخهٔ فعلی فرش (xmin) برای DTO؛ بعد از ذخیره همان نسخهٔ تازه. فرش ردیابی‌نشده یا تازه → ۰.
    /// </summary>
    uint RowVersion(Rug rug);
}

/// <summary>یک مرحله در مسیر سفارشی هنگام ثبت فرش</summary>
public record CustomWorkflowStepRequest(
    Guid ProcessStepTypeId,
    bool IsOptional,
    Guid? ServiceProviderId,
    StepPricingOverride? Pricing);

/// <summary>جایگزینی نرخ پیش‌فرض نوع مرحله</summary>
public record StepPricingOverride(
    Domain.Enums.StepPricingModel? Model,
    decimal? UnitRate);

/// <summary>
/// دادهٔ ارسالی هنگام تکمیل مرحله از UI. هر فیلد null یعنی «دست نزن».
/// دو پرچم آخر تنها راهِ صریحِ «پاک کن» هستند، چون null برای مبلغ دستی و انجام‌دهنده معنای «نگه دار» دارد.
/// </summary>
public record AdvanceStepRequest(
    Guid? ServiceProviderId,
    decimal? ManualCostOverride,
    Domain.Enums.StepPricingModel? PricingModel,
    decimal? UnitRate,
    string? PricingConfigJson,
    string? FieldValuesJson,
    string? Notes,
    bool MarkCompleted = true,
    decimal? Adjustment = null,
    /// <summary>true = مبلغ دستیِ ذخیره‌شده پاک شود (مگر همین درخواست مبلغ دستی تازه‌ای بفرستد).</summary>
    bool? ClearManualOverride = null,
    /// <summary>true = انجام‌دهنده برداشته شود (مگر همین درخواست انجام‌دهندهٔ صریحی بفرستد).</summary>
    bool? ClearServiceProvider = null,
    /// <summary>
    /// «با اندازهٔ تازه» (spec B.5): عکس اندازه و تعدادِ قیمت‌گذاری (<c>Priced*</c>) از اندازهٔ مؤثر و تعداد فعلی دوباره
    /// گرفته شود. بی آن، مرحله‌ای که یک بار قیمت خورده با همان عکس قبلی دوباره قیمت می‌خورد.
    /// </summary>
    bool RefreshPricedSize = false);

/// <summary>
/// خلاصهٔ مالی یک فرش برای نمایش. نام‌های قدیمی ماندند: <see cref="TotalProcessCost"/> = هزینهٔ مراحل،
/// <see cref="PurchaseCost"/> = خرید با سهم «خرید یکجا»، <see cref="TotalInvestment"/> = بهای تمام‌شده (<c>RugCostTotal</c>).
/// </summary>
public record RugCostSummary(
    decimal TotalProcessCost,
    decimal PurchaseCost,
    decimal TotalInvestment,
    decimal? TargetSalePrice,
    decimal? EstimatedMargin,
    // ── فاز ۲: ستون‌های دیگر RugCostTotal ─────────────────────────────────
    decimal StepCost = 0,
    decimal ExpenseCost = 0,
    decimal? CostPerM2 = null,
    decimal CostPerPiece = 0,
    decimal? ExportRevenueShare = null,
    /// <summary>از ردیف ذخیره‌شدهٔ <c>RugCostTotal</c> آمده؛ false یعنی محاسبهٔ درون‌حافظه (پیش از بازسازی، یا آزمون).</summary>
    bool Computed = false);
