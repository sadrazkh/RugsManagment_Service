using System.Text.Json;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.DTOs.Rugs;

/// <summary>
/// نمای کامل یک فرش برای API — همان چیزی که Vue مصرف می‌کند.
///
/// عضوهای پولی (<see cref="PurchaseCost"/>، <see cref="TargetSalePrice"/>، <see cref="Costs"/>،
/// <see cref="PurchaseLines"/> و مبلغ‌های هر مرحله) برای کسی که سیاست Business را ندارد null است
/// (<c>MoneyRedaction.Redact</c>)؛ null یعنی «نمی‌بیند»، نه «صفر». فرانت هر جا این‌ها را نشان می‌دهد باید null را
/// تحمل کند. دلار گمرکی (<see cref="UsdPerM2"/>، <see cref="LineUsd"/>) هزینهٔ داخلی نیست و برای همه دیده می‌شود (A.5).
///
/// <para><b>عضوهای فاز ۲ (I.3)</b> همه دنباله‌دار و با پیش‌فرض‌اند تا سازنده‌های قدیمی نشکنند؛ ترتیبشان ثابت است
/// و فقط به انتها اضافه می‌شود. معنای چند عضو قدیمی از فاز ۲ عوض شد:</para>
/// <list type="bullet">
/// <item><see cref="WidthMeters"/>/<see cref="LengthMeters"/>: جفت «مؤثر» (= نهایی ?? اول).</item>
/// <item><see cref="AreaSquareMeters"/>: مساحت یک تخته در اندازهٔ مؤثر، همان ستون ذخیره‌شدهٔ گردشدهٔ <c>AreaM2</c>.</item>
/// <item><see cref="PurchaseCost"/>: جمع مبلغ سطرهای خرید همین فرش، فقط برای نمایش؛ نه سهم «خرید یکجا» و نه ستون منسوخ
/// <c>Rug.PurchaseCost</c>. جمع خرید با سهم‌ها در <see cref="RugCostSummaryDto.PurchaseCost"/> است.</item>
/// <item><see cref="Costs"/>: از <c>RugCostTotal</c> (پاس سازگاری) پر می‌شود.</item>
/// </list>
/// عضوهای محموله/نگله/دلار را <c>IRugDtoEnricher</c> پر می‌کند (فاز ۲ فقط درز است؛ فاز محموله با یک کوئری
/// بر اساس RugId)؛ تا آن موقع null می‌مانند.
/// </summary>
public record RugDto(
    Guid Id,
    string Sku,
    string? Title,
    string? Origin,
    string? Pattern,
    string? Material,
    int? KnotDensity,
    decimal WidthMeters,
    decimal LengthMeters,
    decimal AreaSquareMeters,
    decimal? PurchaseCost,
    decimal? TargetSalePrice,
    RugStatus Status,
    string? ImageUrl,
    string? Notes,
    Guid? WorkflowTemplateId,
    Guid? BatchId,
    string? BatchName,
    string? CurrentStepNameFa,
    int CurrentStepIndex,
    IReadOnlyList<RugWorkflowStepDto> WorkflowSteps,
    /// <summary>خلاصهٔ هزینه؛ برای غیر Business null</summary>
    RugCostSummaryDto? Costs,
    string? MetadataJson = null,
    /// <summary>گالری عکس‌ها به ترتیب نمایش؛ خالی یعنی هنوز عکسی آپلود نشده</summary>
    IReadOnlyList<RugImageDto>? Images = null,
    // ── فاز ۲ (I.3): به همین ترتیب؛ فقط به انتها اضافه کنید ───────────────
    /// <summary>شمارهٔ بازرگان به شکل نمایشی («3778»، «3911 - 3960» یا «A-12»)؛ null یعنی بی‌شماره.</summary>
    string? SerialNo = null,
    /// <summary>تعداد تخته‌های این ردیف (دسته)؛ دست‌کم ۱.</summary>
    int Quantity = 1,
    /// <summary>طول هنگام رسیدن (متر)؛ سطر خرید «هر متر» همیشه روی اندازهٔ اول است.</summary>
    decimal IntakeLength = 0,
    /// <summary>عرض هنگام رسیدن (متر).</summary>
    decimal IntakeWidth = 0,
    /// <summary>طول نهایی؛ با <see cref="FinalWidth"/> یا هر دو پر یا هر دو null.</summary>
    decimal? FinalLength = null,
    /// <summary>عرض نهایی.</summary>
    decimal? FinalWidth = null,
    /// <summary>مساحت کل ردیف = مساحت یک تخته × تعداد (۰٫۱۶ × ۵۰ = ۸٫۰۰)؛ ستون ذخیره‌شدهٔ <c>TotalAreaM2</c>.</summary>
    decimal TotalAreaM2 = 0,
    /// <summary>جای فرش (C.10)؛ محاسبه می‌شود، ذخیره نمی‌شود.</summary>
    RugLocation Location = RugLocation.InStock,
    /// <summary>محمولهٔ خطِ این فرش (enricher فاز محموله)؛ null یعنی در هیچ نگله‌ای نیست.</summary>
    Guid? ShipmentId = null,
    /// <summary>شمارهٔ همان محموله.</summary>
    int? ShipmentNumber = null,
    /// <summary>نگلهٔ این فرش.</summary>
    Guid? BaleId = null,
    /// <summary>شمارهٔ نگله در محموله.</summary>
    int? BaleNumber = null,
    /// <summary>ارزش گمرکی هر متر مربع (دلار) که روی خط محموله اعمال شده؛ هزینهٔ داخلی نیست و پنهان نمی‌شود.</summary>
    decimal? UsdPerM2 = null,
    /// <summary>دلار کل خط = دلار یک تخته × تعداد.</summary>
    decimal? LineUsd = null,
    /// <summary>
    /// نسخهٔ ردیف (xmin) به‌صورت عدد. هر نوشتنی روی فرش (PUT، اندازهٔ نهایی، سطرهای خرید، برش) همین را برمی‌گرداند
    /// و نسخهٔ کهنه ۴۰۹ می‌گیرد.
    /// </summary>
    uint Version = 0,
    /// <summary>سطرهای خرید به ترتیب نمایش؛ فقط برای Business (برای بقیه null).</summary>
    IReadOnlyList<PurchaseLineDto>? PurchaseLines = null,
    // ── فاز ۲، افزوده بر I.3 ───────────────────────────────────────────────
    /// <summary>مساحت یک تخته در اندازهٔ اول (ROUND(طول اول × عرض اول، ۲))؛ مبنای سطر خرید «هر متر».</summary>
    decimal IntakeAreaM2 = 0,
    /// <summary>زمان ثبت اندازهٔ نهایی؛ null وقتی اندازهٔ نهایی نیست.</summary>
    DateTimeOffset? FinalMeasuredAt = null,
    /// <summary>شمارهٔ «ورودی» این فرش (شمارندهٔ batch)؛ null وقتی ورودی ندارد.</summary>
    int? BatchNumber = null,
    /// <summary>نام فروشندهٔ همان ورودی همان‌طور که نوشته شد؛ null وقتی ورودی یا فروشنده ندارد.</summary>
    string? SellerName = null);

public record RugWorkflowStepDto(
    Guid Id,
    Guid ProcessStepTypeId,
    string StepCode,
    string StepNameFa,
    string StepNameEn,
    string Icon,
    int OrderIndex,
    bool IsOptional,
    WorkflowStepStatus Status,
    Guid? ServiceProviderId,
    string? ServiceProviderName,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    /// <summary>هزینهٔ مؤثر مرحله؛ مثل بقیهٔ مبلغ‌های مرحله برای غیر Business null</summary>
    decimal? EffectiveCost,
    decimal? CalculatedCost,
    string? AppliedPricingModel,
    decimal? AppliedUnitRate,
    string? PricingConfigJson,
    string? FieldValuesJson,
    string? Notes,
    decimal? Adjustment = null,
    /// <summary>کاربری که این مرحله را تکمیل کرد — «چه کسی این کار را انجام داد»</summary>
    string? CompletedByName = null,
    /// <summary>
    /// مبلغ دستیِ ذخیره‌شده (اگر هست). بر هر روش و نرخی مقدم است؛ ویرایشگر هزینه با آن حالت «دستی» را
    /// از پیش پر می‌کند تا پیش‌نمایش همان هزینه‌ای را نشان دهد که واقعاً ذخیره شده.
    /// </summary>
    decimal? ManualCostOverride = null,
    /// <summary>
    /// عکس اندازهٔ قیمت‌گذاری مرحله (B.5): طول و عرض یک تخته و تعدادی که هزینهٔ مرحله رویشان حساب شده و می‌شود؛ null برای
    /// مرحلهٔ بی‌عکس. ویرایشگر هزینه پیش‌نمایش را روی همین می‌خواهد، نه اندازهٔ امروزِ فرش (اندازه نیست، پول نیست).
    /// </summary>
    decimal? PricedLength = null,
    decimal? PricedWidth = null,
    int? PricedQuantity = null);

/// <summary>
/// خلاصهٔ بهای تمام‌شدهٔ فرش (فقط Business). از فاز ۲ از ردیف مشتق‌شدهٔ <c>RugCostTotal</c> خوانده می‌شود، پس فهرست،
/// جزئیات، داشبورد و گزارش یک عدد می‌بینند (D.4). نام‌های قدیمی ماندند تا جزیره‌ها نشکنند:
/// <see cref="TotalProcessCost"/> = هزینهٔ مراحل (<c>StepCost</c>)، <see cref="PurchaseCost"/> = خرید با سهم «خرید یکجا»
/// (<c>RugCostTotal.PurchaseCost</c>)، <see cref="TotalInvestment"/> = بهای تمام‌شده (<c>Total</c>).
/// </summary>
public record RugCostSummaryDto(
    decimal TotalProcessCost,
    decimal PurchaseCost,
    decimal TotalInvestment,
    decimal? TargetSalePrice,
    /// <summary>سود تخمینی بر اساس «قیمت هدف» — تا وقتی فرش فروخته نشده</summary>
    decimal? EstimatedMargin,
    /// <summary>مبلغ خالص فروش واقعی؛ null یعنی هنوز فروخته نشده</summary>
    decimal? ActualSaleAmount = null,
    /// <summary>سود واقعی = فروش خالص − (<see cref="CostAtSale"/> ?? بهای تمام‌شده)؛ null یعنی هنوز فروخته نشده (D.6)</summary>
    decimal? ActualProfit = null,
    // ── فاز ۲ (I.3): به همین ترتیب؛ فقط به انتها اضافه کنید ───────────────
    /// <summary>هزینهٔ مراحل (= <see cref="TotalProcessCost"/>؛ با نام ستون <c>RugCostTotal</c>).</summary>
    decimal StepCost = 0,
    /// <summary>سهم هزینه‌های گروه «هزینه» (شستشو، کرایه، گمرک…)؛ تا فاز هزینه‌ها صفر.</summary>
    decimal ExpenseCost = 0,
    /// <summary>بها برای هر متر مربع = ROUND(کل ÷ مساحت کل ردیف، MoneyScale)؛ null وقتی مساحت صفر است.</summary>
    decimal? CostPerM2 = null,
    /// <summary>بها برای هر تخته = ROUND(کل ÷ تعداد، MoneyScale).</summary>
    decimal CostPerPiece = 0,
    /// <summary>سهم این فرش از درآمد صادرات محموله (ارز پایه)؛ null وقتی درآمدی ثبت نشده.</summary>
    decimal? ExportRevenueShare = null,
    /// <summary>
    /// ردیف <c>RugCostTotal</c> برای این فرش وجود دارد. false یعنی پاس هنوز این فرش را حساب نکرده (مثلاً پیش از
    /// بازسازی استارت) و عددها صفرِ پیش‌فرض‌اند، نه «بی‌هزینه».
    /// </summary>
    bool Computed = false,
    /// <summary>
    /// عکس بهای تمام‌شده در لحظهٔ فروش (D.6؛ M2 فروش‌های قدیمی را پر کرد)؛ null یعنی فروخته نشده. اگر بعداً با
    /// <see cref="TotalInvestment"/> فرق کند، چیپ «هزینه پس از فروش تغییر کرده» نشان داده می‌شود.
    /// </summary>
    decimal? CostAtSale = null);

/// <summary>
/// ثبت فرش جدید — یا WorkflowTemplateId یا CustomSteps.
///
/// <para><b>درخواست فاز ۲</b> (<see cref="Values"/> پر است): فیلدها فقط از <see cref="Values"/> خوانده می‌شوند
/// (<c>RugFieldWriter</c>، کلیدهای FieldCatalog مثل <c>rug.serial</c>، <c>rug.qty</c>، <c>rug.intakeLength</c>) و عضوهای
/// قدیمی توصیفی و اندازه و پول نادیده گرفته می‌شوند؛ چون <see cref="WidthMeters"/>/<see cref="LengthMeters"/> nullable
/// نیستند و نفرستادنشان به صفر bind می‌شود. مسیر و قالب (<see cref="WorkflowTemplateId"/>،
/// <see cref="SkippedOptionalStepIds"/>، <see cref="CustomSteps"/>) در هر دو شکل خوانده می‌شوند.</para>
/// <para><b>درخواست قدیمی</b> (<see cref="Values"/> = null): همان رفتار پیش از فاز ۲؛ اندازه‌ها اندازهٔ اول می‌شوند و
/// <see cref="PurchaseCost"/> (فقط Business) یک سطر خرید «کل» «قیمت خرید» می‌سازد.</para>
/// </summary>
public record CreateRugRequest(
    string? Title,
    string? Origin,
    string? Pattern,
    string? Material,
    int? KnotDensity,
    decimal WidthMeters,
    decimal LengthMeters,
    decimal? PurchaseCost,
    decimal? TargetSalePrice,
    string? ImageUrl,
    string? Notes,
    Guid? WorkflowTemplateId,
    IReadOnlyList<Guid>? SkippedOptionalStepIds,
    IReadOnlyList<CustomRugStepRequest>? CustomSteps,
    string? MetadataJson = null,
    // ── فاز ۲ ──────────────────────────────────────────────────────────────
    /// <summary>
    /// مقدار فیلدها با کلید FieldCatalog (← <c>RugFieldWriter</c>). مقدار JSON null یعنی «خالی کن»؛ کلیدِ نیامده یعنی
    /// «دست نزن». کلید پولی از غیر Business → ۴۰۳ برای کل درخواست (A.5).
    /// </summary>
    IReadOnlyDictionary<string, JsonElement?>? Values = null);

public record CustomRugStepRequest(
    Guid ProcessStepTypeId,
    bool IsOptional,
    Guid? ServiceProviderId);

/// <summary>
/// ویرایش یک فرش (<c>PUT /api/rugs/{id}</c>).
///
/// <para><b>درخواست فاز ۲</b> (<see cref="Values"/> پر است؛ <c>RugForm.vue</c> از فاز ۲): فقط <see cref="Values"/> و
/// <see cref="Version"/> خوانده می‌شوند و همهٔ عضوهای قدیمی نادیده گرفته می‌شوند. دلیل: <see cref="WidthMeters"/> و
/// <see cref="LengthMeters"/> nullable نیستند و فرمی که آن‌ها را نمی‌فرستد صفر bind می‌کند؛ رشته‌های null هم در شکل
/// قدیمی یعنی «خالی کن».</para>
///
/// <para><b>درخواست قدیمی</b> (<see cref="Values"/> = null): فرم قدیمی هر بار <see cref="PurchaseCost"/> و جفت مؤثر را
/// دوباره می‌فرستد؛ این قاعده‌ها (I.3) نمی‌گذارند آن رفت‌وبرگشت داده را خراب کند:</para>
/// <list type="bullet">
/// <item><see cref="PurchaseCost"/> (فقط Business؛ مقدار غیر null از غیر Business → ۴۰۳، null نادیده — E5): فرش بی‌سطر
/// خرید → یک سطر «کل» «قیمت خرید» ساخته می‌شود؛ دقیقاً یک سطر و از نوع «کل» → نرخ همان سطر عوض می‌شود؛ وگرنه اگر با
/// جمع مبلغ سطرها برابر است → نادیده؛ وگرنه ۴۰۹ «قیمت خرید این فرش چندسطری است؛ از بخش «خرید» ویرایش کنید».</item>
/// <item><see cref="WidthMeters"/>/<see cref="LengthMeters"/>: تا اندازهٔ نهایی نیست، اندازهٔ اول را می‌نویسند؛ با
/// اندازهٔ نهایی، برابر جفت مؤثر → نادیده، وگرنه ۴۰۹ (از <c>/final-size</c> استفاده کنید). هرگز اندازهٔ اول را با
/// اندازهٔ نهایی رونویسی نمی‌کنند.</item>
/// </list>
/// </summary>
public record UpdateRugRequest(
    string? Title,
    string? Origin,
    string? Pattern,
    string? Material,
    int? KnotDensity,
    decimal WidthMeters,
    decimal LengthMeters,
    decimal? PurchaseCost,
    decimal? TargetSalePrice,
    RugStatus? Status,
    string? ImageUrl,
    string? Notes,
    string? MetadataJson = null,
    // ── فاز ۲ (I.3) ────────────────────────────────────────────────────────
    /// <summary>
    /// نسخهٔ فرش (<see cref="RugDto.Version"/>) که کاربر دیده است. PUT از فاز ۲ آن را لازم دارد (I.2)؛ نسخهٔ کهنه → ۴۰۹.
    /// DTOی که از GET آمده همین را دارد، پس رفت‌وبرگشت مسیر قدیمی هم آن را می‌فرستد.
    /// </summary>
    uint? Version = null,
    /// <summary>
    /// مقدار فیلدها با کلید FieldCatalog (← <c>RugFieldWriter</c>). مقدار JSON null یعنی «خالی کن»؛ کلیدِ نیامده یعنی
    /// «دست نزن». کلید پولی از غیر Business → ۴۰۳ برای کل درخواست (A.5).
    /// </summary>
    IReadOnlyDictionary<string, JsonElement?>? Values = null);

/// <summary>
/// تکمیل/قیمت‌گذاری یک مرحله. فیلد null یعنی «دست نزن / سرور تصمیم بگیرد».
/// </summary>
public record AdvanceRugStepRequest(
    Guid? ServiceProviderId,
    decimal? ManualCostOverride,
    StepPricingModel? PricingModel,
    decimal? UnitRate,
    string? PricingConfigJson,
    string? FieldValuesJson,
    string? Notes,
    bool MarkCompleted = true,
    decimal? Adjustment = null,
    /// <summary>
    /// true = مبلغ دستیِ ذخیره‌شده پاک شود و مرحله به روش/نرخِ انتخاب‌شده برگردد. ویرایشگر هزینه فقط وقتی
    /// می‌فرستد که کاربر حالت دستی را خاموش کرده باشد؛ تزریق نرخ توافقی و تکمیل گروهی هرگز آن را نمی‌گذارند.
    /// </summary>
    bool? ClearManualOverride = null,
    /// <summary>
    /// true = انجام‌دهندهٔ مرحله برداشته شود (انتخاب «—»). null در ServiceProviderId به‌تنهایی یعنی «همان قبلی».
    /// با برداشتن انجام‌دهنده نرخ توافقی هم تزریق نمی‌شود.
    /// </summary>
    bool? ClearServiceProvider = null);

public record UpdateRugWorkflowRequest(IReadOnlyList<CustomRugStepRequest> PendingSteps);

public record ApplyTemplateRequest(Guid TemplateId, IReadOnlyList<Guid>? SkippedOptionalStepIds);

public record BulkRugIdsRequest(IReadOnlyList<Guid> RugIds);

/// <summary>
/// ویرایش گروهی مشخصات چند فرش.
///
/// هر فیلد null یعنی «دست نزن» — این تفاوت با «خالی کن» عمدی است، وگرنه
/// کاربری که فقط می‌خواهد اصالت را عوض کند ناخواسته بقیهٔ فیلدها را پاک می‌کرد.
/// </summary>
public record BulkUpdateFieldsRequest(
    IReadOnlyList<Guid> RugIds,
    string? Origin,
    string? Pattern,
    string? Material,
    int? KnotDensity,
    decimal? TargetSalePrice,
    RugStatus? Status,
    /// <summary>Guid.Empty یعنی «از گروه خارج کن»</summary>
    Guid? BatchId);

public record BulkAdvanceRequest(
    IReadOnlyList<Guid> RugIds,
    AdvanceRugStepRequest Step);

public record BulkUpdateWorkflowRequest(
    IReadOnlyList<Guid> RugIds,
    IReadOnlyList<CustomRugStepRequest> PendingSteps);

public record BulkOperationResultDto(
    int SuccessCount,
    int FailedCount,
    IReadOnlyList<BulkItemErrorDto> Errors);

public record BulkItemErrorDto(Guid RugId, string Message);
