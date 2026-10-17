using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.DTOs.Providers;

/// <summary>یک طرف خدمات همراه نرخ‌هایش.</summary>
public record ServiceProviderDetailDto(
    Guid Id,
    string Name,
    string? Specialty,
    string? Phone,
    string? Address,
    string? Notes,
    bool IsActive,
    IReadOnlyList<ProviderRateDto> Rates);

public record ProviderRateDto(
    Guid Id,
    Guid ProcessStepTypeId,
    string StepNameFa,
    StepPricingModel PricingModel,
    /// <summary>نرخ توافقی (تومان)؛ در فهرست و جزئیات طرف برای غیر Business null</summary>
    decimal? UnitRate,
    string? Notes);

public record SaveServiceProviderRequest(
    string Name,
    string? Specialty,
    string? Phone,
    string? Address,
    string? Notes,
    bool IsActive,
    IReadOnlyList<SaveProviderRateRequest>? Rates);

public record SaveProviderRateRequest(
    Guid ProcessStepTypeId,
    StepPricingModel PricingModel,
    decimal UnitRate,
    string? Notes);

/// <summary>
/// خلاصهٔ مالی یک طرف خدمات.
///
/// مانده هرگز ذخیره نمی‌شود و همیشه اینجا محاسبه می‌شود:
/// بدهی قطعی (مراحل تکمیل‌شده) − پرداخت‌ها.
/// «در جریان» جدا نمایش داده می‌شود چون هنوز کار تمام نشده و بدهی قطعی نیست.
/// </summary>
public record ProviderBalanceDto(
    Guid ProviderId,
    string ProviderName,
    bool IsActive,
    string? Phone,
    /// <summary>مجموع هزینهٔ مراحلِ تکمیل‌شدهٔ این طرف</summary>
    decimal CompletedWorkTotal,
    /// <summary>مجموع هزینهٔ مراحلی که هنوز در حال انجام‌اند</summary>
    decimal InProgressTotal,
    decimal PaidTotal,
    int CompletedStepCount,
    int InProgressStepCount,
    DateTimeOffset? LastPaymentAt)
{
    /// <summary>مانده = بدهی قطعی − پرداختی. منفی یعنی بیش از بدهی پرداخت شده (علی‌الحساب).</summary>
    public decimal Balance => CompletedWorkTotal - PaidTotal;
}

/// <summary>
/// یک ردیف کار انجام‌شده توسط طرف خدمات — برای صورت‌حساب: یا یک مرحلهٔ کارگاه روی یک فرش، یا (از فاز هزینه‌ها) یک «رسید» = ثبت
/// هزینه‌ای با این طرف که چند فرش را می‌پوشاند (I.3). برای رسید، <see cref="StepId"/> شناسهٔ ثبت، <see cref="RugId"/> خالی،
/// <see cref="StepNameFa"/> نام نوع هزینه، <see cref="RugTitle"/> توضیح ثبت و <see cref="Cost"/> مبلغ منجمد رسید است.
/// <para>این رکورد در projection ساخته می‌شود؛ هر عضو افزوده همان‌جا صریح پاس داده می‌شود (I.1).</para>
/// </summary>
public record ProviderWorkItemDto(
    Guid StepId,
    Guid RugId,
    string RugSku,
    string? RugTitle,
    string StepNameFa,
    DateTimeOffset? CompletedAt,
    WorkflowStepStatus Status,
    decimal Cost,
    // ── فاز هزینه‌ها (I.3) ──────────────────────────────────────────────
    /// <summary>مرحله یا رسید.</summary>
    ProviderWorkKind Kind = ProviderWorkKind.Step,
    /// <summary>شناسهٔ ثبت هزینه برای ردیف رسید؛ null برای مرحله.</summary>
    Guid? CostEntryId = null,
    /// <summary>شمارهٔ ثبت هزینه («هزینهٔ شمارهٔ ۱۲»)؛ null برای مرحله.</summary>
    int? CostEntryNumber = null,
    /// <summary>چند فرش زیر این رسید است؛ null برای مرحله.</summary>
    int? RugCount = null);

public record ProviderPaymentDto(
    Guid Id,
    decimal Amount,
    DateTimeOffset PaidAt,
    string? Reference,
    string? Notes);

public record CreateProviderPaymentRequest(
    decimal Amount,
    DateTimeOffset? PaidAt,
    string? Reference,
    string? Notes);

/// <summary>صفحهٔ صورت‌حساب یک طرف: مانده + کارها + پرداخت‌ها.</summary>
public record ProviderStatementDto(
    ProviderBalanceDto Balance,
    IReadOnlyList<ProviderWorkItemDto> Work,
    IReadOnlyList<ProviderPaymentDto> Payments);
