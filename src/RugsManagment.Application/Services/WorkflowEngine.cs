using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Rugs;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Costing;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Services;

/// <summary>
/// موتور گردش کار — قلب منطق «فرش از کدام مراحل رد می‌شود و چه زمانی مرحله بعد فعال می‌شود».
/// این کلاس به دیتابیس نمی‌نویسد؛ فقط روی موجودیت Rug در حافظه کار می‌کند و سرویس‌ها بعد از آن ذخیره می‌کنند (پاس سازگاری
/// بهای فرش را همان‌جا از نو حساب می‌کند).
///
/// <para><b>از فاز ۲:</b></para>
/// <list type="bullet">
/// <item>قیمت مرحله روی <b>عکس اندازه</b> (<c>Priced*</c>) حساب می‌شود که اولین قیمت‌گذاری (تکمیل یا «فقط ذخیره») می‌گیرد؛
/// قیمت دوباره همان عکس را به کار می‌برد مگر «با اندازهٔ تازه» (<see cref="AdvanceStepRequest.RefreshPricedSize"/>). پس
/// عوض شدن اندازهٔ نهایی بعد از شستشو قیمت شستشو را بی‌صدا عوض نمی‌کند (B.5). گرد کردن در MoneyScale و قاعدهٔ دسته (D.3).</item>
/// <item>وضعیت فرش از <see cref="RugStatusResolver"/> می‌گذرد: فرش فروخته‌شده یا صادرشده با پیش بردن مرحله «آمادهٔ فروش» یا
/// «در جریان» نمی‌شود.</item>
/// <item><see cref="CalculateRugCosts"/> ردیف <c>RugCostTotal</c> را می‌خواند (جای ۱ از هفت جای «سرمایهٔ کل»، D.4).</item>
/// <item>مسیر قالب، نوع مرحلهٔ کارگاه دیگر را رد و انجام‌دهندهٔ کارگاه دیگر را برمی‌دارد (E17: قالب‌های ذخیره‌شده پیش از رفع
/// نشت میان کارگاه‌ها).</item>
/// </list>
/// </summary>
/// <param name="costCalculation">انتخاب روش و نرخ مرحله (فرمول در RugCostMath).</param>
/// <param name="stepTypes">نوع مرحله (فقط سیستمی یا همین کارگاه).</param>
/// <param name="costTotals">خوانندهٔ جمع ذخیره‌شده و MoneyScale؛ بی آن (آزمون واحد) جمع درون‌حافظه و رقم اعشار ۰.</param>
/// <param name="versions">نسخهٔ فرش برای DTO؛ بی آن ۰.</param>
public sealed class WorkflowEngine(
    ICostCalculationService costCalculation,
    IProcessStepTypeLookup stepTypes,
    IRugCostTotalReader? costTotals = null,
    IConcurrencyGuard? versions = null) : IWorkflowEngine
{
    /// <summary>
    /// ساخت مسیر فرش از روی یک قالب آماده.
    /// مراحل اختیاری که کاربر در skippedOptionalStepIds فرستاده باشد، اصلاً به فرش اضافه نمی‌شوند.
    /// </summary>
    public async Task<Rug> InitializeWorkflowFromTemplateAsync(
        Rug rug,
        WorkflowTemplate template,
        IReadOnlyList<Guid>? skippedOptionalStepIds,
        CancellationToken cancellationToken = default)
    {
        var skipped = skippedOptionalStepIds?.ToHashSet() ?? [];
        var ordered = template.Steps.OrderBy(s => s.OrderIndex).ToList();

        // E17: پیش از دست زدن به مسیر فرش؛ قالبِ آلوده هیچ مرحله‌ای روی فرش نمی‌گذارد
        var instances = new List<RugWorkflowStep>();
        var index = 0;
        foreach (var templateStep in ordered)
        {
            // مرحله اختیاری + کاربر خواسته رد شود → در مسیر این فرش نیست
            if (templateStep.IsOptional && skipped.Contains(templateStep.Id))
                continue;

            var stepType = await OwnedStepTypeAsync(rug, templateStep, cancellationToken);
            instances.Add(MapTemplateStep(rug, templateStep, stepType, index++));
        }

        rug.WorkflowSteps.Clear();
        rug.WorkflowTemplateId = template.Id;
        foreach (var instance in instances)
            rug.WorkflowSteps.Add(instance);

        RugStatusResolver.ApplyWorkflowStatus(rug, RugStatus.InProgress);
        rug.CurrentStepIndex = 0;
        ActivateCurrentStep(rug);
        return rug;
    }

    /// <summary>
    /// مسیر کاملاً دستی: کاربر خودش ترتیب انواع مرحله را انتخاب کرده (بدون قالب).
    /// </summary>
    public async Task<Rug> BuildCustomWorkflowAsync(
        Rug rug,
        IReadOnlyList<CustomWorkflowStepRequest> steps,
        CancellationToken cancellationToken = default)
    {
        var scale = await MoneyScaleAsync(rug, cancellationToken);
        rug.WorkflowSteps.Clear();
        rug.WorkflowTemplateId = null;

        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            var stepType = await stepTypes.GetRequiredAsync(step.ProcessStepTypeId, cancellationToken);
            var instance = new RugWorkflowStep
            {
                RugId = rug.Id,
                ProcessStepTypeId = stepType.Id,
                // خودِ ناوبری هم ست می‌شود: بدون آن، هر کدی که مرحله را بسازد و
                // در همان درخواست پیش ببرد به NullReferenceException می‌خورد.
                ProcessStepType = stepType,
                OrderIndex = index,
                IsOptional = step.IsOptional,
                ServiceProviderId = step.ServiceProviderId,
                Status = WorkflowStepStatus.Pending
            };
            ApplyPricing(instance, rug, stepType, step.Pricing, scale);
            rug.WorkflowSteps.Add(instance);
        }

        RugStatusResolver.ApplyWorkflowStatus(rug, RugStatus.InProgress);
        rug.CurrentStepIndex = 0;
        ActivateCurrentStep(rug);
        return rug;
    }

    /// <summary>
    /// پیش بردن مرحله: ثبت یادداشت، هزینه دستی، تکمیل یا فقط «در حال انجام».
    /// اگر MarkCompleted=true باشد، مرحله بعدی خودکار فعال می‌شود.
    /// </summary>
    public async Task<RugWorkflowStep> AdvanceStepAsync(
        Rug rug,
        Guid stepId,
        AdvanceStepRequest request,
        CancellationToken cancellationToken = default)
    {
        var step = GetActiveOrPendingStep(rug, stepId);
        ApplyServiceProvider(step, request);

        step.FieldValuesJson = request.FieldValuesJson ?? step.FieldValuesJson;
        step.Notes = request.Notes ?? step.Notes;
        EnsureStepMoneyFits(request, await MoneyScaleAsync(rug, cancellationToken));
        ApplyStepPricing(step, request);
        await PriceAsync(rug, step, request.RefreshPricedSize, cancellationToken);

        if (request.MarkCompleted)
        {
            step.Status = WorkflowStepStatus.Completed;
            step.CompletedAt = DateTimeOffset.UtcNow;
            MoveToNextStep(rug);
        }
        else
        {
            step.Status = WorkflowStepStatus.InProgress;
            step.StartedAt ??= DateTimeOffset.UtcNow;
        }

        return step;
    }

    public async Task<RugWorkflowStep> UpdateStepPricingAsync(
        Rug rug,
        Guid stepId,
        AdvanceStepRequest request,
        CancellationToken cancellationToken = default)
    {
        var step = rug.WorkflowSteps.FirstOrDefault(s => s.Id == stepId)
            ?? throw new InvalidOperationException("مرحله یافت نشد.");
        if (step.Status is WorkflowStepStatus.Cancelled)
            throw new InvalidOperationException("مرحله لغو شده قابل ویرایش نیست.");

        ApplyServiceProvider(step, request);
        if (request.Notes is not null)
            step.Notes = request.Notes;
        EnsureStepMoneyFits(request, await MoneyScaleAsync(rug, cancellationToken));
        ApplyStepPricing(step, request);
        await PriceAsync(rug, step, request.RefreshPricedSize, cancellationToken);

        return step;
    }

    /// <summary>
    /// رد کردن مرحله — فقط وقتی IsOptional=true مجاز است (مثلاً دارکشی).
    /// </summary>
    public Task<RugWorkflowStep> SkipStepAsync(Rug rug, Guid stepId, CancellationToken cancellationToken = default)
    {
        var step = rug.WorkflowSteps.FirstOrDefault(s => s.Id == stepId)
            ?? throw new InvalidOperationException("مرحله یافت نشد.");

        if (!step.IsOptional)
            throw new InvalidOperationException("فقط مراحل اختیاری قابل رد کردن هستند.");

        step.Status = WorkflowStepStatus.Skipped;
        step.CompletedAt = DateTimeOffset.UtcNow;
        MoveToNextStep(rug);
        return Task.FromResult(step);
    }

    public Task<Rug> GoBackStepAsync(Rug rug, CancellationToken cancellationToken = default)
    {
        var ordered = rug.WorkflowSteps.OrderBy(s => s.OrderIndex).ToList();
        var current = ordered.FirstOrDefault(s => s.Status == WorkflowStepStatus.InProgress);

        if (current is not null)
        {
            current.Status = WorkflowStepStatus.Pending;
            current.StartedAt = null;

            var previous = ordered
                .Where(s => s.OrderIndex < current.OrderIndex && s.Status == WorkflowStepStatus.Completed)
                .OrderByDescending(s => s.OrderIndex)
                .FirstOrDefault();

            if (previous is not null)
            {
                previous.Status = WorkflowStepStatus.InProgress;
                previous.CompletedAt = null;
                rug.CurrentStepIndex = previous.OrderIndex;
            }
            else
                ActivateCurrentStep(rug);
        }
        else
        {
            var lastCompleted = ordered.LastOrDefault(s => s.Status == WorkflowStepStatus.Completed);
            if (lastCompleted is null)
                throw new InvalidOperationException("مرحله‌ای برای بازگشت وجود ندارد.");

            foreach (var s in ordered.Where(x => x.OrderIndex > lastCompleted.OrderIndex && x.Status != WorkflowStepStatus.Skipped))
            {
                s.Status = WorkflowStepStatus.Pending;
                s.StartedAt = null;
                s.CompletedAt = null;
            }

            lastCompleted.Status = WorkflowStepStatus.InProgress;
            lastCompleted.CompletedAt = null;
            rug.CurrentStepIndex = lastCompleted.OrderIndex;
        }

        RugStatusResolver.ApplyWorkflowStatus(rug, RugStatus.InProgress);
        return Task.FromResult(rug);
    }

    public Task<Rug> ActivateStepAsync(Rug rug, Guid stepId, CancellationToken cancellationToken = default)
    {
        var target = rug.WorkflowSteps.FirstOrDefault(s => s.Id == stepId)
            ?? throw new InvalidOperationException("مرحله یافت نشد.");

        if (target.Status == WorkflowStepStatus.Skipped)
            throw new InvalidOperationException("مرحله رد شده قابل فعال‌سازی نیست.");

        foreach (var s in rug.WorkflowSteps.OrderBy(x => x.OrderIndex))
        {
            if (s.Status == WorkflowStepStatus.Skipped)
                continue;

            if (s.OrderIndex < target.OrderIndex)
            {
                s.Status = WorkflowStepStatus.Completed;
                s.CompletedAt ??= DateTimeOffset.UtcNow;
            }
            else if (s.OrderIndex == target.OrderIndex)
            {
                s.Status = WorkflowStepStatus.InProgress;
                s.StartedAt = DateTimeOffset.UtcNow;
                s.CompletedAt = null;
            }
            else
            {
                s.Status = WorkflowStepStatus.Pending;
                s.StartedAt = null;
                s.CompletedAt = null;
            }
        }

        rug.CurrentStepIndex = target.OrderIndex;
        RugStatusResolver.ApplyWorkflowStatus(rug, RugStatus.InProgress);
        return Task.FromResult(rug);
    }

    public async Task<Rug> UpdateWorkflowPathAsync(
        Rug rug,
        IReadOnlyList<CustomWorkflowStepRequest> pendingSteps,
        CancellationToken cancellationToken = default)
    {
        var scale = await MoneyScaleAsync(rug, cancellationToken);
        var locked = rug.WorkflowSteps
            .Where(s => s.Status is WorkflowStepStatus.Completed or WorkflowStepStatus.Skipped)
            .OrderBy(s => s.OrderIndex)
            .ToList();

        var removable = rug.WorkflowSteps
            .Where(s => s.Status is WorkflowStepStatus.Pending or WorkflowStepStatus.InProgress)
            .ToList();
        foreach (var s in removable)
            rug.WorkflowSteps.Remove(s);

        var startIndex = locked.Count;
        for (var i = 0; i < pendingSteps.Count; i++)
        {
            var input = pendingSteps[i];
            var stepType = await stepTypes.GetRequiredAsync(input.ProcessStepTypeId, cancellationToken);
            var instance = new RugWorkflowStep
            {
                RugId = rug.Id,
                ProcessStepTypeId = stepType.Id,
                // خودِ ناوبری هم ست می‌شود: بدون آن، هر کدی که مرحله را بسازد و
                // در همان درخواست پیش ببرد به NullReferenceException می‌خورد.
                ProcessStepType = stepType,
                OrderIndex = startIndex + i,
                IsOptional = input.IsOptional,
                ServiceProviderId = input.ServiceProviderId,
                Status = WorkflowStepStatus.Pending
            };
            ApplyPricing(instance, rug, stepType, input.Pricing, scale);
            rug.WorkflowSteps.Add(instance);
        }

        rug.WorkflowTemplateId = null;
        if (!rug.WorkflowSteps.Any(s => s.Status == WorkflowStepStatus.InProgress))
            ActivateCurrentStep(rug);

        return rug;
    }

    /// <summary>
    /// بهای فرش برای نمایش: از ردیف <c>RugCostTotal</c> (پاس سازگاری؛ بعد از ذخیره همان عدد تازه)، وگرنه همان قاعده‌ها روی
    /// گراف درون‌حافظه. سود تخمینی = قیمت هدف − بهای تمام‌شده.
    /// </summary>
    public RugCostSummary CalculateRugCosts(Rug rug)
    {
        ArgumentNullException.ThrowIfNull(rug);

        var stored = costTotals?.Find(rug.TenantId, rug.Id);
        if (stored is not null)
        {
            return new RugCostSummary(
                stored.StepCost, stored.PurchaseCost, stored.Total, rug.TargetSalePrice, Margin(rug, stored.Total),
                stored.StepCost, stored.ExpenseCost, stored.CostPerM2, stored.CostPerPiece, stored.ExportRevenueShare,
                Computed: true);
        }

        // ردیفی نیست (فرش تازه پیش از ذخیره، پیش از بازسازی استارت، یا آزمون بی دیتابیس): همان RugCostMath، بی سهم ثبت‌ها
        var scale = costTotals?.MoneyScale(rug.TenantId) ?? 0;
        var lines = rug.PurchaseLines.OrderBy(l => l.SortOrder).ToList();
        var purchase = lines.Count == 0 ? 0m : PurchaseMath.Total(rug, lines, scale);
        var breakdown = RugCostMath.Compose(
            purchase, 0m, RugCostMath.StepCost(rug.WorkflowSteps), 0m, rug.TotalAreaM2, Math.Max(1, rug.Quantity), scale);

        return new RugCostSummary(
            breakdown.StepCost, breakdown.PurchaseCost, breakdown.Total, rug.TargetSalePrice, Margin(rug, breakdown.Total),
            breakdown.StepCost, breakdown.ExpenseCost, breakdown.CostPerM2, breakdown.CostPerPiece, null,
            Computed: false);
    }

    public uint RowVersion(Rug rug) => versions?.VersionOf(rug) ?? 0;

    /// <summary>
    /// وضعیت واقعی فرش بر اساس مراحلش:
    ///   بدون مرحله → پیش‌نویس، همهٔ مراحل تمام → آمادهٔ فروش، وگرنه در جریان.
    /// «فروخته‌شده»، «صادر شده» و «بایگانی» اینجا برنمی‌گردند چون از گردش کار نمی‌آیند (تقدمشان در RugStatusResolver).
    /// </summary>
    public RugStatus ResolveStatusFromWorkflow(Rug rug) => RugStatusResolver.FromWorkflow(rug);

    // ═════════════════════════════════════════════════════════════════════
    // جزئیات
    // ═════════════════════════════════════════════════════════════════════

    private static decimal? Margin(Rug rug, decimal total)
        => rug.TargetSalePrice.HasValue ? rug.TargetSalePrice.Value - total : null;

    private async Task<int> MoneyScaleAsync(Rug rug, CancellationToken cancellationToken)
        => costTotals is null ? 0 : await costTotals.MoneyScaleAsync(rug.TenantId, cancellationToken);

    /// <summary>
    /// قیمت‌گذاری مرحله: عکس اندازه و تعداد اگر هنوز نیست (یا «با اندازهٔ تازه») و بعد هزینهٔ محاسبه‌شده روی همان عکس.
    /// </summary>
    private async Task PriceAsync(Rug rug, RugWorkflowStep step, bool refreshSize, CancellationToken cancellationToken)
    {
        if (!step.HasPricingSnapshot || refreshSize)
            step.SnapshotPricingSize(rug.LengthMeters, rug.WidthMeters, Math.Max(1, rug.Quantity));

        var scale = await MoneyScaleAsync(rug, cancellationToken);
        step.CalculatedCost = costCalculation.CalculateStepCost(
            rug,
            step.ProcessStepType!,
            null,
            null,
            step.ManualCostOverride,
            step,
            scale);
    }

    /// <summary>
    /// نوع مرحلهٔ یک ردیف قالب، فقط اگر سیستمی یا مال همین کارگاه باشد (E17). قالب‌هایی که پیش از رفع نشت میان کارگاه‌ها
    /// ذخیره شدند ممکن است هنوز شناسهٔ نوع مرحلهٔ کارگاه دیگر را داشته باشند؛ آن مرحله نام و نرخ و فرم کارگاه دیگر را به این
    /// فرش می‌چسباند، پس رد می‌شود («یافت نشد»، مثل هر شناسهٔ کارگاه دیگر).
    /// </summary>
    private async Task<ProcessStepType> OwnedStepTypeAsync(
        Rug rug, WorkflowTemplateStep templateStep, CancellationToken cancellationToken)
    {
        var stepType = templateStep.ProcessStepType
                       ?? await stepTypes.GetRequiredAsync(templateStep.ProcessStepTypeId, cancellationToken);
        if (stepType.TenantId is Guid owner && owner != rug.TenantId)
            throw new KeyNotFoundException($"نوع مرحله {templateStep.ProcessStepTypeId} یافت نشد.");
        return stepType;
    }

    /// <summary>
    /// تبدیل یک ردیف قالب به یک ردیف مرحله روی فرش. انجام‌دهندهٔ پیش‌فرضی که مال کارگاه دیگر است برداشته می‌شود (E17)؛
    /// کار این فرش نباید به حساب طرفِ کارگاه دیگر برود.
    /// </summary>
    private static RugWorkflowStep MapTemplateStep(
        Rug rug, WorkflowTemplateStep templateStep, ProcessStepType stepType, int orderIndex)
    {
        var providerId = templateStep.DefaultServiceProvider is { } provider && provider.TenantId != rug.TenantId
            ? null
            : templateStep.DefaultServiceProviderId;

        return new RugWorkflowStep
        {
            RugId = rug.Id,
            ProcessStepTypeId = stepType.Id,
            ProcessStepType = stepType,
            OrderIndex = orderIndex,
            IsOptional = templateStep.IsOptional,
            ServiceProviderId = providerId,
            Status = WorkflowStepStatus.Pending
        };
    }

    /// <summary>
    /// برآورد اولیهٔ هزینهٔ مرحلهٔ تازه روی اندازه و تعداد فعلی (بی عکس؛ عکس در اولین قیمت‌گذاری گرفته می‌شود). مرحلهٔ در
    /// صف در بها شمرده نمی‌شود، پس این عدد فقط پیش‌نمایش است.
    /// </summary>
    private void ApplyPricing(RugWorkflowStep instance, Rug rug, ProcessStepType stepType, StepPricingOverride? pricing, int scale)
    {
        instance.CalculatedCost = costCalculation.CalculateStepCost(
            rug,
            stepType,
            pricing?.Model,
            pricing?.UnitRate,
            null,
            null,
            scale);
    }

    /// <summary>
    /// مبلغ دستی و تعدیل مرحله مبلغ‌اند، پس رقم اعشارشان بیش از MoneyScale کارگاه نیست و از سقف مبلغ نمی‌گذرند (مثل قیمت
    /// فروش هدف). چرا: ستون‌های مرحله numeric بی‌سقف‌اند ولی جمع فرش (RugCostTotals) numeric(18,2) است؛ ۱۰۰٫۱۲۵ در جمع
    /// ۱۰۰٫۱۳ می‌شد و بررسی حساب‌ها برای همیشه «ناهمخوان» می‌گفت (بازسازی همان عدد گردشده را می‌نوشت)، و در MoneyScale ۰
    /// بهای تومانیِ اعشاری می‌ماند. نادرست → ۴۰۰ روی همان عضو درخواست.
    /// </summary>
    private static void EnsureStepMoneyFits(AdvanceStepRequest request, int moneyScale)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (request.ManualCostOverride is { } manual && !FitsMoney(manual, moneyScale))
            errors["manualCostOverride"] = [Messages.InvalidValue.Format(("label", "مبلغ دستی"))];
        if (request.Adjustment is { } adjustment && !FitsMoney(adjustment, moneyScale))
            errors["adjustment"] = [Messages.InvalidValue.Format(("label", "تخفیف یا اضافه"))];
        if (errors.Count > 0)
            throw new AppValidationException(errors.Values.First()[0], errors);
    }

    private static bool FitsMoney(decimal value, int moneyScale)
        => Num.HasAtMostDecimals(value, moneyScale) && Math.Abs(value) <= RugFieldWriter.MaxAmount;

    /// <summary>
    /// فقط مقادیرِ دارای مقدار روی مرحله نوشته می‌شوند؛ null یعنی «دست نزن».
    ///
    /// مبلغ دستی با یک مبلغ دستیِ تازه عوض می‌شود و فقط با پرچم صریح <c>ClearManualOverride</c> پاک می‌شود.
    /// «روش قیمت‌گذاری بدون مبلغ دستی» به‌تنهایی پاکش نمی‌کند، چون تزریق نرخ توافقیِ طرف خدمات (و تکمیل
    /// سریع/گروهی) دقیقاً همین شکل درخواست را می‌سازند و مبلغی که اپراتور دستی وارد کرده بود بی‌صدا با نرخ
    /// دیگری جایگزین می‌شد. از طرف دیگر بدون پرچم، مبلغ دستی هرگز پاک‌شدنی نبود و انتخاب روش/نرخ بعد از آن بی‌اثر
    /// می‌ماند؛ پرچم را فقط ویرایشگر هزینه وقتی می‌فرستد که کاربر حالت دستی را خاموش کرده باشد.
    /// </summary>
    private static void ApplyStepPricing(RugWorkflowStep step, AdvanceStepRequest request)
    {
        if (request.PricingModel.HasValue)
            step.AppliedPricingModel = request.PricingModel;
        if (request.UnitRate.HasValue)
            step.AppliedUnitRate = request.UnitRate;
        if (request.PricingConfigJson is not null)
            step.PricingConfigJson = string.IsNullOrWhiteSpace(request.PricingConfigJson) ? null : request.PricingConfigJson;

        if (request.ManualCostOverride.HasValue)
            step.ManualCostOverride = request.ManualCostOverride;
        else if (request.ClearManualOverride == true)
            step.ManualCostOverride = null;

        if (request.Adjustment.HasValue)
            step.Adjustment = request.Adjustment;
    }

    /// <summary>
    /// انجام‌دهندهٔ مرحله: شناسهٔ صریح جایگزین می‌شود؛ null یعنی «همان قبلی»؛ و فقط پرچم صریح
    /// <c>ClearServiceProvider</c> (انتخاب «—» در پنل) انجام‌دهنده را برمی‌دارد. شناسهٔ صریح بر پرچم مقدم است.
    /// </summary>
    private static void ApplyServiceProvider(RugWorkflowStep step, AdvanceStepRequest request)
    {
        if (request.ServiceProviderId.HasValue)
            step.ServiceProviderId = request.ServiceProviderId;
        else if (request.ClearServiceProvider == true)
            step.ServiceProviderId = null;
    }

    /// <summary>اولین مرحله Pending را InProgress می‌کند؛ اگر مرحله‌ای نماند → آماده فروش (با تقدم فروش و صادرات).</summary>
    private static void ActivateCurrentStep(Rug rug)
    {
        var current = rug.WorkflowSteps
            .OrderBy(s => s.OrderIndex)
            .FirstOrDefault(s => s.Status == WorkflowStepStatus.Pending);

        if (current is null)
        {
            RugStatusResolver.ApplyWorkflowStatus(rug, RugStatus.ReadyForSale);
            return;
        }

        current.Status = WorkflowStepStatus.InProgress;
        current.StartedAt = DateTimeOffset.UtcNow;
        rug.CurrentStepIndex = current.OrderIndex;
    }

    /// <summary>بعد از تکمیل/رد مرحله، نوبت بعدی را فعال می‌کند</summary>
    private static void MoveToNextStep(Rug rug)
    {
        var next = rug.WorkflowSteps
            .OrderBy(s => s.OrderIndex)
            .FirstOrDefault(s => s.Status == WorkflowStepStatus.Pending);

        if (next is null)
        {
            RugStatusResolver.ApplyWorkflowStatus(rug, RugStatus.ReadyForSale);
            rug.CurrentStepIndex = rug.WorkflowSteps.Count;
            return;
        }

        next.Status = WorkflowStepStatus.InProgress;
        next.StartedAt = DateTimeOffset.UtcNow;
        rug.CurrentStepIndex = next.OrderIndex;
        RugStatusResolver.ApplyWorkflowStatus(rug, RugStatus.InProgress);
    }

    private static RugWorkflowStep GetActiveOrPendingStep(Rug rug, Guid stepId)
    {
        var step = rug.WorkflowSteps.FirstOrDefault(s => s.Id == stepId)
            ?? throw new InvalidOperationException("مرحله یافت نشد.");

        if (step.Status is WorkflowStepStatus.Completed or WorkflowStepStatus.Skipped)
            throw new InvalidOperationException("این مرحله قبلاً بسته شده است.");

        return step;
    }
}

/// <summary>واسط بارگذاری نوع مرحله از دیتابیس — پیاده‌سازی در Infrastructure</summary>
public interface IProcessStepTypeLookup
{
    Task<ProcessStepType> GetRequiredAsync(Guid id, CancellationToken cancellationToken = default);
}
