namespace RugsManagment.Application.Common;

/// <summary>
/// پیام‌های فرش از فاز ۲ (مدل فرش نسخهٔ ۲): شماره و تعداد، اندازهٔ اول و نهایی، سطرهای خرید، نوشتن فیلدها، برش دسته
/// و پیشنهاد مقدار — همان صدای H.3، آینه در <c>CA/lib/messages/rugs.ts</c>.
///
/// چرا چند کد این‌جا متنِ ثابت‌های دامنه را تکرار می‌کند (<c>SerialNumber.*ErrorMessage</c>،
/// <c>Rug.QuantityErrorMessage</c>، <c>Rug.FinalSizeSetErrorMessage</c>): Domain به Application دسترسی ندارد و پیامش را
/// خودش می‌سازد، ولی جزیره همان کد را از <c>lib/messages</c> می‌خواند (مثلاً وقتی جدول پیش از ارسال شماره را
/// می‌سنجد). آزمون واحد <c>RugMessagesTests</c> بررسی می‌کند که هر جفت دقیقاً یکی باشد؛ پس اگر کسی متن دامنه را عوض کند
/// همان‌جا می‌شکند، نه جلوی بازرگان.
/// </summary>
public static partial class Messages
{
    // ── شماره و تعداد (همان متن‌های Domain/Common/SerialNumber و Domain/Entities/Rug) ──────────

    /// <summary>شماره‌ای که شکل عدد یا دسته دارد ولی درست نوشته نشده («3911-»، «3911 3960»).</summary>
    public static readonly MessageTemplate SerialFormat =
        new("E_SERIAL_FORMAT", "شماره را مثل ۳۷۷۸ و دسته را مثل ۳۹۱۱ - ۳۹۶۰ بنویسید.");

    /// <summary>دستهٔ برعکس («3960-3911»).</summary>
    public static readonly MessageTemplate SerialReversed =
        new("E_SERIAL_REVERSED", "در دسته، شمارهٔ اول باید کوچک‌تر باشد؛ مثل ۳۹۱۱ - ۳۹۶۰ بنویسید.");

    /// <summary>دسته‌ای با بیش از ده هزار شماره.</summary>
    public static readonly MessageTemplate SerialSpan =
        new("E_SERIAL_SPAN", "یک دسته حداکثر ۱۰٬۰۰۰ شماره دارد.");

    /// <summary>شمارهٔ متنیِ بلندتر از ستون (۴۰ حرف).</summary>
    public static readonly MessageTemplate SerialTooLong =
        new("E_SERIAL_TOO_LONG", "شماره حداکثر ۴۰ حرف است.");

    /// <summary>بازه کوتاه‌تر از تعداد تخته‌ها؛ <c>{serial}</c> شماره، <c>{count}</c> تعداد شماره‌های بازه، <c>{qty}</c> تعداد تخته.</summary>
    public static readonly MessageTemplate SerialQuantity =
        new("E_SERIAL_QTY",
            "شمارهٔ «{serial}» فقط {count} شماره دارد ولی تعداد {qty} تخته است؛ بازه نباید از تعداد کوتاه‌تر باشد (مثل ۳۹۱۱ - ۳۹۶۰).");

    /// <summary>تعداد کمتر از یک.</summary>
    public static readonly MessageTemplate RugQuantity =
        new("E_RUG_QUANTITY", "تعداد تخته باید دست‌کم ۱ باشد.");

    /// <summary>مسیر قدیمی طول/عرض فرشی را عوض می‌کند که اندازهٔ نهایی دارد (۴۰۹).</summary>
    public static readonly MessageTemplate RugHasFinalSize =
        new("E_RUG_HAS_FINAL_SIZE", "این فرش اندازهٔ نهایی دارد؛ اندازه را از بخش «اندازهٔ نهایی» عوض کنید.");

    /// <summary>
    /// شماره با شمارهٔ فرش فعال دیگری همپوشانی دارد (۴۰۹ از <c>SerialOverlapStage</c>، با <c>nextFree</c>)؛ همان الگوی
    /// <c>SerialOverlapException.OverlapMessage</c>. <c>{serial}</c> شمارهٔ درخواست‌شده، <c>{other}</c> شمارهٔ فرش دیگر.
    /// </summary>
    public static readonly MessageTemplate SerialOverlap =
        new("E_SERIAL_OVERLAP", "شمارهٔ «{serial}» با شمارهٔ فرش دیگری («{other}») هم‌پوشانی دارد.");

    /// <summary>همان، وقتی فرش دیگر معلوم نیست (فقط متن یکسان از ایندکس یکتا)؛ الگوی <c>SerialOverlapException.TakenMessage</c>.</summary>
    public static readonly MessageTemplate SerialTaken =
        new("E_SERIAL_TAKEN", "شمارهٔ «{serial}» مال فرش دیگری است.");

    /// <summary>جست‌وجوی شماره (اسکنر، «شماره را بزنید») چیزی پیدا نکرد (۴۰۴)؛ <c>{serial}</c> همان که نوشته شد.</summary>
    public static readonly MessageTemplate RugSerialNotFound =
        new("E_RUG_SERIAL_NOT_FOUND", "فرشی با شمارهٔ «{serial}» پیدا نشد.");

    // ── نسخه و نوشتن فیلدها ─────────────────────────────────────────────────────────────────

    /// <summary>درخواست فاز ۲ بدون نسخهٔ فرش (۴۰۰)؛ بدون نسخه دو ذخیرهٔ هم‌زمان همدیگر را بی‌صدا پاک می‌کردند.</summary>
    public static readonly MessageTemplate RugVersionRequired =
        new("E_RUG_VERSION_REQUIRED", "نسخهٔ فرش همراه درخواست نیامده؛ صفحه را تازه کنید و دوباره ذخیره کنید.");

    /// <summary>کلید فیلدی که نویسندهٔ فیلدها نمی‌شناسد؛ <c>{key}</c> همان کلید.</summary>
    public static readonly MessageTemplate FieldUnknown =
        new("E_FIELD_UNKNOWN", "خانهٔ «{key}» در این کارگاه تعریف نشده است.");

    /// <summary>خانه‌ای که حساب می‌شود (مثل متراژ) و نوشتنی نیست.</summary>
    public static readonly MessageTemplate FieldReadOnly =
        new("E_FIELD_READONLY", "«{label}» خودش حساب می‌شود و نوشتنی نیست.");

    /// <summary>گزینه‌ای که در فهرست گزینه‌های یک فیلد سفارشیِ «انتخابی» نیست.</summary>
    public static readonly MessageTemplate FieldOption =
        new("E_FIELD_OPTION", "«{value}» از گزینه‌های «{label}» نیست.");

    /// <summary>طول و عرض باید با هم بیایند (فقط یکی نوشته شد).</summary>
    public static readonly MessageTemplate SizePairRequired =
        new("E_SIZE_PAIR", "طول و عرض را با هم بنویسید.");

    // ── اندازهٔ نهایی ────────────────────────────────────────────────────────────────────────

    /// <summary>درخواست اندازهٔ نهایی که هیچ‌کدام از سه شکل درست را ندارد.</summary>
    public static readonly MessageTemplate FinalSizeRequest =
        new("E_FINAL_SIZE_REQUEST", "یا طول و عرض نهایی را بنویسید، یا «همان اندازه» یا «پاک کردن» را بزنید.");

    // ── سطرهای خرید ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// فرم قدیمیِ فرش یک «قیمت خرید» تک‌عددی فرستاد که با جمع سطرهای چندگانه نمی‌خواند (I.3، ۴۰۹): از آن عدد نمی‌شود
    /// فهمید کدام سطر باید عوض شود، پس هیچ سطری بی‌صدا بازنویسی نمی‌شود.
    /// </summary>
    public static readonly MessageTemplate PurchaseMultiLine =
        new("E_PURCHASE_MULTILINE", "قیمت خرید این فرش چندسطری است؛ از بخش «خرید» ویرایش کنید.");

    /// <summary>سطر خریدی که شناسه‌اش مال این فرش نیست (ویرایشگر کهنه).</summary>
    public static readonly MessageTemplate PurchaseLineUnknown =
        new("E_PURCHASE_LINE_UNKNOWN", "این سطر خرید دیگر در این فرش نیست؛ صفحه را تازه کنید.");

    /// <summary>نوع هزینه‌ای که در گروه «خرید» این کارگاه نیست یا غیرفعال شده.</summary>
    public static readonly MessageTemplate PurchaseCategoryInvalid =
        new("E_PURCHASE_CATEGORY", "این نوع خرید پیدا نشد یا غیرفعال شده است.");

    /// <summary>دو سطر از یک نوع خرید در خانهٔ جدول؛ <c>{label}</c> نام نوع.</summary>
    public static readonly MessageTemplate PurchaseCellMultiLine =
        new("E_PURCHASE_CELL_MULTILINE", "«{label}» این فرش چند سطر دارد؛ از بخش «خرید» ویرایش کنید.");

    /// <summary>درصدِ بیرون از ۰ تا ۱۰۰.</summary>
    public static readonly MessageTemplate PurchasePercentRange =
        new("E_PURCHASE_PERCENT", "درصد باید از ۰ تا ۱۰۰ باشد.");

    /// <summary>نرخ با بیش از چهار رقم اعشار (ستون numeric(18,4)).</summary>
    public static readonly MessageTemplate PurchaseRateDecimals =
        new("E_PURCHASE_RATE_DECIMALS", "نرخ حداکثر چهار رقم بعد از ممیز دارد.");

    /// <summary>بیش از حد سطر برای یک فرش.</summary>
    public static readonly MessageTemplate PurchaseTooManyLines =
        new("E_PURCHASE_TOO_MANY", "یک فرش حداکثر {n} سطر خرید دارد.");

    // ── برش دسته (D.9) ───────────────────────────────────────────────────────────────────────

    /// <summary>فرشی که دسته نیست (تعداد یک).</summary>
    public static readonly MessageTemplate SplitNotALot =
        new("E_SPLIT_NOT_LOT", "فقط دسته‌ای با دست‌کم ۲ تخته را می‌شود جدا کرد.");

    /// <summary>تعداد جداشده بیرون از ۱ تا (تعداد − ۱)؛ <c>{max}</c> بیشترین مقدار.</summary>
    public static readonly MessageTemplate SplitCount =
        new("E_SPLIT_COUNT", "تعداد تخته‌ای که جدا می‌شود باید از ۱ تا {max} باشد.");

    /// <summary>دسته‌ای که فروش دارد (۴۰۹؛ M.2 #27: اول جدا کنید، بعد بفروشید).</summary>
    public static readonly MessageTemplate SplitSold =
        new("E_SPLIT_SOLD", "این دسته فروش ثبت‌شده دارد؛ اول فروش را لغو کنید، جدا کنید و بعد بخش فروخته‌شده را بفروشید.");

    /// <summary>بازه با تعداد یکی نیست (یا شماره متنی است)، پس شمارهٔ هر دو بخش باید نوشته شود.</summary>
    public static readonly MessageTemplate SplitSerialsRequired =
        new("E_SPLIT_SERIALS", "شمارهٔ هر دو بخش را بنویسید؛ این دسته را نمی‌شود خودکار شماره‌گذاری کرد.");

    /// <summary>شمارهٔ بخشی که می‌ماند یا جدا می‌شود خالی است.</summary>
    public static readonly MessageTemplate SplitSerialMissing =
        new("E_SPLIT_SERIAL_MISSING", "شمارهٔ این بخش را بنویسید.");

    // ── قالب کارهای کارگاه (E17) ───────────────────────────────────────────────────────────

    /// <summary>
    /// قالبی که مرحله‌ای از کارگاه دیگر دارد (از پیش از رفع نشت میان کارگاه‌ها). فرش با آن ساخته نمی‌شود، چون
    /// نام و نرخ و فرم مرحلهٔ کارگاه دیگر به این فرش می‌چسبید.
    /// </summary>
    public static readonly MessageTemplate TemplateForeignStep =
        new("E_TEMPLATE_FOREIGN_STEP",
            "این قالب مرحله‌ای دارد که در این کارگاه تعریف نشده؛ قالب را از «قالب‌های کارهای کارگاه» دوباره ذخیره کنید.");

    // ── پیشنهاد مقدار ────────────────────────────────────────────────────────────────────────

    /// <summary>کلید ستونی که برایش پیشنهاد مقدار نداریم (<c>/api/rugs/values</c>، ۴۰۰).</summary>
    public static readonly MessageTemplate ValuesFieldInvalid =
        new("E_VALUES_FIELD", "برای این خانه پیشنهادی نداریم.");

    // ── سلامت حساب‌ها ───────────────────────────────────────────────────────────────────────

    /// <summary>بررسی حساب‌ها هیچ ناهمخوانی پیدا نکرد.</summary>
    public static readonly MessageTemplate LedgerHealthy =
        new("I_LEDGER_OK", "همهٔ حساب‌ها درست است؛ {n} فرش بررسی شد.");

    /// <summary>بازسازی حساب‌ها تمام شد.</summary>
    public static readonly MessageTemplate LedgerRebuilt =
        new("I_LEDGER_REBUILT", "حساب {n} فرش از نو نوشته شد.");
}
