using RugsManagment.Application.DTOs.Common;
using RugsManagment.Application.DTOs.Dashboard;
using RugsManagment.Application.DTOs.Providers;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.DTOs.Workflows;

namespace RugsManagment.Application.Common;

/// <summary>
/// پنهان کردن مبلغ‌های تومانی از کسی که سیاست Business را ندارد (انباردار) و رد کردن نوشتن پولی او.
///
/// چرا در سرویس‌ها و نه در view: هر جزیره، هر JSON و هر view از همان DTO می‌خواند؛ اگر پنهان‌کاری در
/// view بود، کافی بود یک صفحهٔ قدیمی یا یک API فراموش شود تا مبلغ به انباردار برسد. پس سرویسی که DTO
/// می‌سازد (فرش، داشبورد، مراحل، طرف‌های خدمات، خانه) عضوهای پولی را همین‌جا null می‌کند و آزمون
/// ماتریس دسترسی بررسی می‌کند هیچ کلید پولی در پاسخِ انباردار نیست.
///
/// بخش اول ابزار عمومی است؛ بخش دوم redact مخصوص هر DTO. هر redact برای کاربر Business همان شیء را
/// بی‌تغییر برمی‌گرداند (بدون کپی)، پس مسیر بازرگان و تنظیم‌کننده هزینه‌ای ندارد.
/// </summary>
public static class MoneyRedaction
{
    /// <summary>مبلغ برای بیننده: خودِ مقدار اگر Business است، وگرنه null.</summary>
    public static decimal? Money(this IMoneyVisibility visibility, decimal? value)
        => visibility.CanSeeMoney ? value : null;

    /// <summary>همان <see cref="Money(IMoneyVisibility, decimal?)"/> برای عضوی که در DTO اصلی nullable نبود.</summary>
    public static decimal? Money(this IMoneyVisibility visibility, decimal value)
        => visibility.CanSeeMoney ? value : null;

    /// <summary>
    /// شیء پولیِ کامل (مثل خلاصهٔ هزینهٔ فرش) برای بیننده: خودِ شیء اگر Business است، وگرنه null.
    /// </summary>
    public static T? Section<T>(this IMoneyVisibility visibility, T? value) where T : class
        => visibility.CanSeeMoney ? value : null;

    /// <summary>
    /// آیا درخواست هیچ عضو پولیِ پرشده‌ای ندارد. null یعنی «نفرستاده» و همیشه مجاز است.
    /// </summary>
    public static bool HasNoMoney(params object?[] moneyMembers)
        => moneyMembers.All(m => m is null);

    /// <summary>
    /// قاعدهٔ نوشتن پولی: اگر کاربر Business نیست و حتی یکی از عضوهای پولیِ درخواست (قیمت خرید،
    /// قیمت فروش، روش و نرخ مرحله، مبلغ دستی، تعدیل، …) مقدار دارد، <see cref="UnauthorizedAccessException"/>
    /// (→ ۴۰۳) با پیام فارسی پرتاب می‌کند.
    ///
    /// عضو null رد نمی‌شود: فرم قدیمی فرش تا فاز بعد همهٔ عضوها را دوباره می‌فرستد و برای انباردار
    /// مبلغ‌ها null است. سرویس باید در این حالت مقدار ذخیره‌شده را نگه دارد و null را روی آن ننویسد
    /// (erratum E5)، وگرنه ویرایش انباردار قیمت خرید را پاک می‌کرد.
    /// </summary>
    public static void EnsureMayWriteMoney(this IMoneyVisibility visibility, params object?[] moneyMembers)
    {
        if (visibility.CanSeeMoney || HasNoMoney(moneyMembers)) return;
        throw new UnauthorizedAccessException(Messages.MoneyWriteForbidden.Text);
    }

    /// <summary>
    /// قاعدهٔ نوشتن پولی برای تکمیل یا قیمت‌گذاری یک مرحله: روش، نرخ، پیکربندی ترکیبی، مبلغ دستی، تعدیل
    /// و «پاک کردن مبلغ دستی». تکمیلِ بدون هیچ‌کدام از این‌ها برای انباردار آزاد است و سرور نرخ توافقیِ
    /// انجام‌دهنده یا نرخ پیش‌فرض مرحله را می‌گذارد. «پاک کردن مبلغ دستی = false» یعنی «دست نزن» و پول نیست.
    /// </summary>
    public static void EnsureMayWriteStepPricing(this IMoneyVisibility visibility, AdvanceRugStepRequest request)
        => visibility.EnsureMayWriteMoney(
            request.PricingModel,
            request.UnitRate,
            string.IsNullOrWhiteSpace(request.PricingConfigJson) ? null : request.PricingConfigJson,
            request.ManualCostOverride,
            request.Adjustment,
            request.ClearManualOverride == true ? true : null);

    // ═════════════════════════════════════════════════════════════════════
    // redact مخصوص هر DTO
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// فرش کامل: قیمت خرید، قیمت هدف، خلاصهٔ هزینه و مبلغ‌های هر مرحله null می‌شوند. ابعاد، وضعیت،
    /// مراحل (نام، وضعیت، انجام‌دهنده، تاریخ) و فیلدهای سفارشی می‌مانند، چون کار انبار به آن‌ها نیاز دارد.
    /// </summary>
    public static RugDto Redact(this IMoneyVisibility visibility, RugDto rug)
        => visibility.CanSeeMoney
            ? rug
            : rug with
            {
                PurchaseCost = null,
                TargetSalePrice = null,
                Costs = null,
                WorkflowSteps = rug.WorkflowSteps.Select(StripStepMoney).ToList()
            };

    /// <summary>فهرست فرش‌های کامل (برچسب‌ها، نمونه‌های طراح، …).</summary>
    public static IReadOnlyList<RugDto> Redact(this IMoneyVisibility visibility, IReadOnlyList<RugDto> rugs)
        => visibility.CanSeeMoney ? rugs : rugs.Select(r => visibility.Redact(r)).ToList();

    /// <summary>یک مرحلهٔ فرش: مبلغ مؤثر، مبلغ محاسبه‌شده، نرخ، پیکربندی ترکیبی، مبلغ دستی و تعدیل.</summary>
    public static RugWorkflowStepDto Redact(this IMoneyVisibility visibility, RugWorkflowStepDto step)
        => visibility.CanSeeMoney ? step : StripStepMoney(step);

    /// <summary>ردیف فهرست فرش‌ها: «سرمایهٔ کل» null.</summary>
    public static RugListItemDto Redact(this IMoneyVisibility visibility, RugListItemDto item)
        => visibility.CanSeeMoney ? item : item with { TotalInvestment = null };

    /// <summary>یک صفحه از فهرست فرش‌ها.</summary>
    public static PagedResult<RugListItemDto> Redact(this IMoneyVisibility visibility, PagedResult<RugListItemDto> page)
        => visibility.CanSeeMoney
            ? page
            : page with { Items = page.Items.Select(i => i with { TotalInvestment = null }).ToList() };

    /// <summary>
    /// مرتب‌سازی بر اساس «هزینهٔ کل» برای کسی که هزینه نمی‌بیند به «تاریخ ثبت» برمی‌گردد؛ وگرنه
    /// ترتیب ردیف‌ها خودش ارزان و گران بودن فرش‌ها را لو می‌داد.
    /// </summary>
    public static RugQuery GuardSort(this IMoneyVisibility visibility, RugQuery query)
        => visibility.CanSeeMoney || query.SortBy != RugSortBy.TotalCost
            ? query
            : query with { SortBy = RugSortBy.CreatedAt };

    /// <summary>
    /// داشبورد: همهٔ کارت‌های مالی (سرمایه، در جریان، آمادهٔ فروش، سود تخمینی، فروش، سود واقعی، طلب) و
    /// سرمایهٔ فرش‌های اخیر null می‌شوند. شمارنده‌ها می‌مانند.
    /// </summary>
    public static DashboardStatsDto Redact(this IMoneyVisibility visibility, DashboardStatsDto stats)
        => visibility.CanSeeMoney
            ? stats
            : stats with
            {
                TotalInvestment = null,
                PipelineValue = null,
                ProfitEstimate = null,
                ReadyForSaleValue = null,
                ActualSalesTotal = null,
                ActualProfitTotal = null,
                OutstandingReceivable = null,
                RecentRugs = stats.RecentRugs.Select(r => r with { TotalInvestment = null }).ToList()
            };

    /// <summary>نوع مرحله: نرخ پیش‌فرض null (روش قیمت‌گذاری پول نیست و می‌ماند).</summary>
    public static ProcessStepTypeDto Redact(this IMoneyVisibility visibility, ProcessStepTypeDto stepType)
        => visibility.CanSeeMoney ? stepType : stepType with { DefaultUnitRate = null };

    /// <summary>فهرست انواع مرحله (<c>/api/lookups/step-types</c> و صفحهٔ مراحل).</summary>
    public static IReadOnlyList<ProcessStepTypeDto> Redact(
        this IMoneyVisibility visibility, IReadOnlyList<ProcessStepTypeDto> stepTypes)
        => visibility.CanSeeMoney ? stepTypes : stepTypes.Select(t => visibility.Redact(t)).ToList();

    /// <summary>قالب گردش کار: نرخ جایگزینِ هر مرحلهٔ قالب null.</summary>
    public static WorkflowTemplateDto Redact(this IMoneyVisibility visibility, WorkflowTemplateDto template)
        => visibility.CanSeeMoney
            ? template
            : template with { Steps = template.Steps.Select(s => s with { OverrideUnitRate = null }).ToList() };

    /// <summary>فهرست قالب‌های گردش کار.</summary>
    public static IReadOnlyList<WorkflowTemplateDto> Redact(
        this IMoneyVisibility visibility, IReadOnlyList<WorkflowTemplateDto> templates)
        => visibility.CanSeeMoney ? templates : templates.Select(t => visibility.Redact(t)).ToList();

    /// <summary>طرف خدمات: نرخ توافقیِ هر نوع مرحله null (نام مرحله و روش می‌مانند).</summary>
    public static ServiceProviderDetailDto Redact(this IMoneyVisibility visibility, ServiceProviderDetailDto provider)
        => visibility.CanSeeMoney
            ? provider
            : provider with { Rates = provider.Rates.Select(r => r with { UnitRate = null }).ToList() };

    private static RugWorkflowStepDto StripStepMoney(RugWorkflowStepDto step) => step with
    {
        EffectiveCost = null,
        CalculatedCost = null,
        AppliedUnitRate = null,
        PricingConfigJson = null,
        ManualCostOverride = null,
        Adjustment = null
    };
}
