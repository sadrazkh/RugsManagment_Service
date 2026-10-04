using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// موجودیت اصلی: یک فرش — یا یک «دسته» از فرش‌های هم‌شکل (تعداد بیش از یک، مثل «3911 - 3960» × ۵۰) — در یک
/// کارگاه، با شماره، اندازهٔ اول و نهایی، و مسیر فرایند نمونه‌برداری‌شده.
///
/// <para><b>اندازه‌ها (spec B.4).</b> سه جفت ضلع دارد:</para>
/// <list type="bullet">
/// <item><see cref="IntakeLength"/>/<see cref="IntakeWidth"/>: اندازهٔ لحظهٔ رسیدن (همیشه پر)؛ قیمت متری خرید روی این است.</item>
/// <item><see cref="FinalLength"/>/<see cref="FinalWidth"/>: اندازهٔ نهایی پیش از صادرات (هر دو با هم، یا هیچ‌کدام).</item>
/// <item><see cref="LengthMeters"/>/<see cref="WidthMeters"/>: جفت «مؤثر» = نهایی ?? اول. SQL و گزارش‌های قدیمی همین را
/// می‌خوانند، پس نامش عوض نشد؛ این جفت همیشه با دو جفت دیگر هم‌خوان می‌ماند (خودِ متدهای این کلاس، و بررسی
/// self-verify در M2 و <c>LedgerVerifier</c>).</item>
/// </list>
/// اندازه‌ها را فقط با <see cref="SetIntakeSize"/>، <see cref="SetFinalSize"/> و <see cref="ClearFinalSize"/> بنویسید.
///
/// <para><b>مساحت‌ها</b> (<see cref="AreaM2"/>، <see cref="TotalAreaM2"/>، <see cref="IntakeAreaM2"/>) در دیتابیس ستون
/// تولیدشده (generated stored) هستند تا هر SQLی همان عدد را ببیند؛ در C# همان فرمول (<see cref="AreaMath"/>) روی مقدار فعلیِ
/// اضلاع حساب می‌شود، پس پیش از ذخیره هم درست‌اند. مقدار خوانده‌شده از دیتابیس در فیلدهای پشتیبان می‌نشیند و EF فقط
/// همان را می‌بیند؛ این مقادیر هرگز در تفاوت‌سنجی پاس سازگاری ظاهر نمی‌شوند.</para>
///
/// <para><b>شماره و تعداد</b> فقط با <see cref="SetSerialAndQuantity"/> (یا <see cref="SetSerial"/> / <see cref="SetQuantity"/>)
/// نوشته می‌شوند تا قاعدهٔ «بازه هرگز کوتاه‌تر از تعداد نیست» پیش از دیتابیس سنجیده شود.</para>
/// </summary>
public class Rug : BaseEntity, ITenantScoped
{
    /// <summary>کد پیام وقتی تعداد کمتر از یک است.</summary>
    public const string QuantityErrorCode = "E_RUG_QUANTITY";

    /// <summary>متن پیام تعداد نامعتبر.</summary>
    public const string QuantityErrorMessage = "تعداد تخته باید دست‌کم ۱ باشد.";

    /// <summary>کد پیام وقتی اندازهٔ مؤثرِ فرشی که اندازهٔ نهایی دارد از مسیر قدیمی عوض می‌شود (۴۰۹).</summary>
    public const string FinalSizeSetErrorCode = "E_RUG_HAS_FINAL_SIZE";

    /// <summary>متن همان پیام.</summary>
    public const string FinalSizeSetErrorMessage =
        "این فرش اندازهٔ نهایی دارد؛ اندازه را از بخش «اندازهٔ نهایی» عوض کنید.";

    public Guid TenantId { get; set; }

    /// <summary>کد انبار خودکار مثل RUG-202606-0001</summary>
    public string Sku { get; set; } = string.Empty;

    public string? Title { get; set; }
    public string? Origin { get; set; }
    public string? Pattern { get; set; }
    public string? Material { get; set; }
    public int? KnotDensity { get; set; }

    // ── شمارهٔ بازرگان (B.4) ─────────────────────────────────────────────

    /// <summary>شکل نمایشی شماره («3778»، «3911 - 3960» یا «A-12»)؛ null یعنی بی‌شماره. یکتا بین فرش‌های فعال کارگاه.</summary>
    public string? SerialNo { get; private set; }

    /// <summary>اولین شمارهٔ عددی؛ null برای شمارهٔ متنی یا بی‌شماره.</summary>
    public int? SerialFrom { get; private set; }

    /// <summary>آخرین شمارهٔ عددی (برای شمارهٔ تک برابر <see cref="SerialFrom"/>).</summary>
    public int? SerialTo { get; private set; }

    /// <summary>کلید مرتب‌سازی (= <see cref="SerialFrom"/>)؛ null برای شمارهٔ متنی، که در مرتب‌سازی آخر می‌آید.</summary>
    public long? SerialSort { get; private set; }

    /// <summary>تعداد تخته‌های این ردیف (دسته)؛ دست‌کم ۱.</summary>
    public int Quantity { get; private set; } = 1;

    // ── اندازه‌ها ────────────────────────────────────────────────────────

    private decimal _lengthMeters;
    private decimal _widthMeters;

    /// <summary>عرض مؤثر (= نهایی ?? اول)، متر.</summary>
    /// <remarks>
    /// setter عمومی فقط برای کد قدیمی مانده و قاعدهٔ I.3 را اجرا می‌کند: تا اندازهٔ نهایی نیست، نوشتنش اندازهٔ اول را
    /// هم می‌نویسد (پس فرشی که مسیر قدیمی می‌سازد هم‌خوان می‌ماند)؛ وقتی اندازهٔ نهایی هست، همان مقدارِ فعلی
    /// بی‌اثر است و مقدار دیگر ۴۰۹ (<see cref="DomainConflictException"/>). EF از فیلد پشتیبان می‌خواند و می‌نویسد،
    /// نه از این setter.
    /// </remarks>
    public decimal WidthMeters
    {
        get => _widthMeters;
        set => WriteLegacyEffectiveSide(value, isLength: false);
    }

    /// <summary>طول مؤثر (= نهایی ?? اول)، متر. قاعدهٔ setter همان <see cref="WidthMeters"/>.</summary>
    public decimal LengthMeters
    {
        get => _lengthMeters;
        set => WriteLegacyEffectiveSide(value, isLength: true);
    }

    /// <summary>طول هنگام رسیدن فرش (numeric(9,2)).</summary>
    public decimal IntakeLength { get; private set; }

    /// <summary>عرض هنگام رسیدن فرش (numeric(9,2)).</summary>
    public decimal IntakeWidth { get; private set; }

    /// <summary>طول نهایی؛ با <see cref="FinalWidth"/> یا هر دو پر یا هر دو خالی.</summary>
    public decimal? FinalLength { get; private set; }

    /// <summary>عرض نهایی.</summary>
    public decimal? FinalWidth { get; private set; }

    /// <summary>زمان ثبت اندازهٔ نهایی.</summary>
    public DateTimeOffset? FinalMeasuredAt { get; private set; }

    /// <summary>کاربری که اندازهٔ نهایی را ثبت کرد (بدون کلید خارجی، مثل <see cref="DeletedByUserId"/>).</summary>
    public Guid? FinalMeasuredBy { get; private set; }

    // مقدار ستون‌های تولیدشده، همان‌طور که دیتابیس حساب کرده؛ فقط EF آن‌ها را می‌نویسد (PropertyAccessMode.Field).
#pragma warning disable CS0169, CS0649 // EF با reflection پرشان می‌کند
    private decimal _storedAreaM2;
    private decimal _storedTotalAreaM2;
    private decimal _storedIntakeAreaM2;
#pragma warning restore CS0169, CS0649

    /// <summary>مساحت یک تخته در اندازهٔ مؤثر: ROUND(L×W, 2). ستون تولیدشده <c>AreaM2</c>.</summary>
    public decimal AreaM2 => AreaMath.PieceArea(LengthMeters, WidthMeters);

    /// <summary>مساحت کل ردیف: مساحت تخته × تعداد (۰٫۱۶ × ۵۰ = ۸٫۰۰). ستون تولیدشده <c>TotalAreaM2</c>.</summary>
    public decimal TotalAreaM2 => AreaMath.LineArea(AreaM2, Quantity);

    /// <summary>مساحت یک تخته در اندازهٔ اول: ROUND(L×W, 2). ستون تولیدشده <c>IntakeAreaM2</c>.</summary>
    public decimal IntakeAreaM2 => AreaMath.PieceArea(IntakeLength, IntakeWidth);

    /// <summary>
    /// قیمت خرید قدیمی (پیش از M2). M2 هر مقدار غیرخالی را به یک سطر خرید «کل» («قیمت خرید») برد؛ از آن پس
    /// هیچ‌کس در این ستون نمی‌نویسد و M7 آن را حذف می‌کند. قیمت خرید = جمع <see cref="PurchaseLines"/>.
    /// <para>نگاشت EF با قرارداد است (هیچ کدی به این ستون اشاره نمی‌کند)، پس برخلاف B.4 به <c>#pragma</c> در
    /// <c>EntityConfigurations.cs</c> نیازی نیست؛ CI با <c>-warnaserror</c> هر استفادهٔ تازه را خطا می‌کند.</para>
    /// </summary>
    [Obsolete("از M2 قیمت خرید = جمع سطرهای خرید (RugPurchaseLine)؛ این ستون فقط برای M2/Down مانده و M7 حذفش می‌کند.")]
    public decimal? PurchaseCost { get; set; }

    public decimal? TargetSalePrice { get; set; }

    public RugStatus Status { get; set; } = RugStatus.Draft;
    public string? ImageUrl { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// مقادیر فیلدهای سفارشی این کارگاه به‌صورت JSON (ستون jsonb).
    /// کلیدها از CustomFieldDefinition.Key می‌آیند — انعطاف NoSQL‌گونه بدون تغییر اسکیمـا.
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>اگر از قالب ساخته شده؛ null یعنی مسیر کاملاً سفارشی</summary>
    public Guid? WorkflowTemplateId { get; set; }

    /// <summary>«ورودی» این فرش (یک فروشنده در یک روز) — برای عملیات گروهی و هزینه‌های ورودی</summary>
    public Guid? BatchId { get; set; }

    /// <summary>شاخص مرحلهٔ جاری در مسیر (برای UI)</summary>
    public int CurrentStepIndex { get; set; }

    /// <summary>
    /// شناسهٔ ردیفِ جدول ورود (grid) که این فرش را ساخت؛ تلاش دوبارهٔ همان ردیف (قطع اینترنت) همان فرش را
    /// برمی‌گرداند و فرش دوم نمی‌سازد. یکتا در کارگاه وقتی پر است.
    /// </summary>
    public Guid? ClientRowId { get; set; }

    /// <summary>
    /// زمان انتقال به سطل زباله؛ null یعنی فعال.
    /// حذف نرم است تا هزینه‌های ثبت‌شدهٔ مراحل و تاریخچه از بین نرود و اشتباه اپراتور برگشت‌پذیر باشد.
    /// فیلتر سراسری EF این رکوردها را از همهٔ کوئری‌ها بیرون می‌گذارد.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>چه کسی حذف کرده — برای پاسخ‌گویی در لاگ فعالیت</summary>
    public Guid? DeletedByUserId { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public RugBatch? Batch { get; set; }
    public WorkflowTemplate? WorkflowTemplate { get; set; }

    /// <summary>کپی زندهٔ مراحل این فرش — با پیش بردن مرحله وضعیت‌ها عوض می‌شود</summary>
    public ICollection<RugWorkflowStep> WorkflowSteps { get; set; } = [];

    /// <summary>گالری عکس‌های فرش به ترتیب SortOrder</summary>
    public ICollection<RugImage> Images { get; set; } = [];

    /// <summary>فروش این فرش؛ null یعنی هنوز فروخته نشده</summary>
    public RugSale? Sale { get; set; }

    /// <summary>سطرهای قیمت خرید (قیمت خرید، دلالی، کرایه تا انبار، …).</summary>
    public ICollection<RugPurchaseLine> PurchaseLines { get; set; } = [];

    /// <summary>
    /// جمع هزینهٔ مشتق‌شده (فقط پاس سازگاری می‌نویسد). در کوئری‌ها فقط از راه projection بدون ردیابی خوانده شود،
    /// هرگز با Include روی موجودیت ردیابی‌شده.
    /// </summary>
    public RugCostTotal? CostTotal { get; set; }

    /// <summary>نام قدیمیِ مساحت تخته؛ حالا همان <see cref="AreaM2"/> گردشده (I.3).</summary>
    public decimal AreaSquareMeters => AreaM2;

    /// <summary>اندازهٔ نهایی ثبت شده است.</summary>
    public bool HasFinalSize => FinalLength is not null && FinalWidth is not null;

    /// <summary>شمارهٔ فعلی به‌صورت <see cref="SerialNumber"/>؛ null یعنی بی‌شماره.</summary>
    public SerialNumber? Serial => SerialNumber.FromStored(SerialNo, SerialFrom, SerialTo);

    // ═════════════════════════════════════════════════════════════════════
    // شماره و تعداد
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// شماره و تعداد را با هم می‌نویسد (ترتیب دو تغییر جدا مهم نیست: «3778» × ۵۰ ← «3911 - 3960» × ۵۰ یک قدم است).
    /// تعداد کمتر از ۱ یا بازهٔ کوتاه‌تر از تعداد → <see cref="DomainValidationException"/>. همپوشانی با فرش‌های دیگر
    /// را این‌جا نمی‌شود دید؛ <c>SerialOverlapStage</c> زیر قفل کارگاه می‌سنجد.
    /// </summary>
    public void SetSerialAndQuantity(SerialNumber? serial, int quantity)
    {
        if (quantity < 1)
            throw new DomainValidationException(QuantityErrorMessage, "rug.quantity");
        serial?.EnsureCovers(quantity);

        SerialNo = serial?.Display;
        SerialFrom = serial?.From;
        SerialTo = serial?.To;
        SerialSort = serial?.SortKey;
        Quantity = quantity;
    }

    /// <summary>فقط شماره (تعداد همان می‌ماند).</summary>
    public void SetSerial(SerialNumber? serial) => SetSerialAndQuantity(serial, Quantity);

    /// <summary>فقط تعداد (شماره همان می‌ماند و باید هنوز کافی باشد).</summary>
    public void SetQuantity(int quantity) => SetSerialAndQuantity(Serial, quantity);

    // ═════════════════════════════════════════════════════════════════════
    // اندازه‌ها
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// اندازهٔ اول؛ اگر اندازهٔ نهایی نیست، جفت مؤثر هم همین می‌شود. هر ضلع از ۵ سانت تا ۳۰ متر با حداکثر دو رقم
    /// اعشار (<see cref="Dimension"/>)، وگرنه ۴۰۰.
    /// </summary>
    public void SetIntakeSize(decimal length, decimal width)
    {
        var l = Dimension.From(length, "rug.intakeLength");
        var w = Dimension.From(width, "rug.intakeWidth");
        IntakeLength = l;
        IntakeWidth = w;
        SyncEffectiveSize();
    }

    /// <summary>اندازهٔ نهایی (هر دو ضلع با هم)؛ از این پس جفت مؤثر همین است.</summary>
    public void SetFinalSize(decimal length, decimal width, DateTimeOffset measuredAt, Guid? measuredBy)
    {
        var l = Dimension.From(length, "rug.finalLength");
        var w = Dimension.From(width, "rug.finalWidth");
        FinalLength = l;
        FinalWidth = w;
        FinalMeasuredAt = measuredAt;
        FinalMeasuredBy = measuredBy;
        SyncEffectiveSize();
    }

    /// <summary>«همان اندازه»: اندازهٔ نهایی برابر اندازهٔ اول ثبت می‌شود (صریح، نه با خالی گذاشتن).</summary>
    public void SetFinalSizeSameAsIntake(DateTimeOffset measuredAt, Guid? measuredBy)
        => SetFinalSize(IntakeLength, IntakeWidth, measuredAt, measuredBy);

    /// <summary>پاک کردن اندازهٔ نهایی؛ جفت مؤثر به اندازهٔ اول برمی‌گردد.</summary>
    public void ClearFinalSize()
    {
        FinalLength = null;
        FinalWidth = null;
        FinalMeasuredAt = null;
        FinalMeasuredBy = null;
        SyncEffectiveSize();
    }

    private void SyncEffectiveSize()
    {
        _lengthMeters = FinalLength ?? IntakeLength;
        _widthMeters = FinalWidth ?? IntakeWidth;
    }

    /// <summary>
    /// قاعدهٔ I.3 برای نوشتن جفت مؤثر از مسیر قدیمی: بی اندازهٔ نهایی → همان ضلعِ اندازهٔ اول هم نوشته می‌شود؛
    /// با اندازهٔ نهایی → همان مقدار بی‌اثر است و مقدار دیگر ۴۰۹. مقدار به دو رقم گرد می‌شود، مثل ستون numeric(9,2).
    /// </summary>
    private void WriteLegacyEffectiveSide(decimal value, bool isLength)
    {
        var rounded = Num.Round(value, AreaMath.DimensionDecimals);
        if (FinalLength is not null || FinalWidth is not null)
        {
            if (rounded != (isLength ? _lengthMeters : _widthMeters))
                throw new DomainConflictException(FinalSizeSetErrorMessage, isLength ? "rug.length" : "rug.width");
            return;
        }

        if (isLength)
        {
            _lengthMeters = rounded;
            IntakeLength = rounded;
        }
        else
        {
            _widthMeters = rounded;
            IntakeWidth = rounded;
        }
    }
}
