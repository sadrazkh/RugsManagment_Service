namespace RugsManagment.Application.Common;

/// <summary>
/// پیام‌های دفتر «هزینه‌ها»، تقسیم و حساب محموله (فاز هزینه‌ها؛ spec D.8، H.2، H.3، H.3b) — آینه در
/// <c>CA/lib/messages/costs.ts</c>. هر «نگله» با نشانهٔ <c>{bale}</c> / <c>{baleOf}</c> است (E10)، واحد پول با
/// <c>{currency}</c> (کارگاه ریالی «ریال» می‌بیند، نه «تومان»)، و هیچ عدد نمونه‌ای در متن ثابت نیست.
/// </summary>
public static partial class Messages
{
    // ── هشدارهای تقسیم (D.8) ─────────────────────────────────────────────

    /// <summary>نوع هزینه به نوع مرحله‌ای وصل است که این فرش‌ها با هزینه دارند؛ <c>{category}</c> نام نوع.</summary>
    public static readonly MessageTemplate CostStepDouble =
        new("W_STEP_DOUBLE", "برای {n} فرش «{category}» در کارهای کارگاه هم حساب شده؛ دوبار حساب نشود؟");

    /// <summary>فرش هم سطر خرید غیرصفر دارد و هم سهم «خرید یکجا».</summary>
    public static readonly MessageTemplate CostPurchaseDouble =
        new("W_PURCHASE_DOUBLE", "برای {n} فرش هم «قیمت خرید» ثبت شده و هم «خرید یکجا»؛ مطمئنید دوبار حساب نشده؟");

    /// <summary>مبلغ ثابتِ فرشی که دیگر جزو فرش‌های این هزینه نیست؛ <c>{serial}</c> شماره(ها).</summary>
    public static readonly MessageTemplate CostPinDropped =
        new("W_PIN_DROPPED", "مبلغ ثابتِ فرش {serial} حساب نشد، چون دیگر جزو این فرش‌ها نیست.");

    public static readonly MessageTemplate CostZeroWeights =
        new("W_ZERO_WEIGHTS", "چیزی برای نسبت گرفتن نبود؛ مساوی تقسیم شد.");

    public static readonly MessageTemplate CostNoDeclaredValue =
        new("W_NO_DECLARED_VALUE", "{n} فرش ارزش گمرکی ندارند و سهمی نگرفتند.");

    public static readonly MessageTemplate CostEmptyTarget =
        new("W_EMPTY_TARGET", "فرشی برای تقسیم نیست؛ هزینه «تقسیم‌نشده» ماند.");

    /// <summary>نگله، محموله یا ورودیِ این هزینه حذف شد (کنار پیام دکمهٔ «انتخاب فرش‌ها» می‌آید).</summary>
    public static readonly MessageTemplate CostTargetDeleted =
        new("W_TARGET_DELETED", "جایی که این هزینه به آن وصل بود ({bale}، محموله یا ورودی) حذف شد؛ دوباره مشخص کنید برای کدام فرش‌هاست.");

    public static readonly MessageTemplate CostLate =
        new("W_LATE_COST", "این محموله قطعی شده؛ هزینهٔ تازه فقط سود را تغییر می‌دهد، نه اظهار را.");

    /// <summary>رسید منجمد با اندازه‌های امروز مبلغ دیگری می‌شد؛ <c>{amount}</c> فرق (کنار پیام دکمهٔ «به‌روزرسانی مبلغ»).</summary>
    public static readonly MessageTemplate CostRateDrift =
        new("W_RATE_DRIFT", "مبلغ این رسید با اندازه‌های فعلی {amount} {currency} فرق دارد.");

    public static readonly MessageTemplate CostPinsResidual =
        new("W_PINS_RESIDUAL", "{amount} {currency} از این هزینه به هیچ فرشی نرسید، چون همهٔ فرش‌ها مبلغ ثابت دارند.");

    public static readonly MessageTemplate CostNegativeWeight =
        new("W_NEGATIVE_WEIGHT", "قیمت خرید {n} فرش منفی بود و سهمی نگرفتند.");

    // ── خطاهای ثبت (D.8، ۴۰۰) ────────────────────────────────────────────

    /// <summary>جمع مبلغ‌های ثابت از کل بیشتر است یا علامتش با کل فرق دارد.</summary>
    public static readonly MessageTemplate CostPinsExceed =
        new("E_PINS_EXCEED", "جمع مبلغ‌های ثابت ({pins}) از کل هزینه ({total}) بیشتر است.");

    /// <summary>مبلغ ثابت روی هزینهٔ زندهٔ «هر متر» / «هر تخته» (E9: فقط «کل» یا رسید).</summary>
    public static readonly MessageTemplate CostPinsBasis =
        new("E_PINS_BASIS", "مبلغ ثابت فقط برای هزینهٔ «کل» است، یا هزینه‌ای که شمارهٔ رسید یا طرف حساب دارد.");

    public static readonly MessageTemplate CostCircular =
        new("E_CIRCULAR", "خرید را نمی‌شود به نسبت قیمت خرید تقسیم کرد.");

    public static readonly MessageTemplate CostFxRequired =
        new("E_FX_REQUIRED", "برای مبلغ دلاری نرخ دلار را بنویسید.");

    public static readonly MessageTemplate CostEmptySelection =
        new("E_SCOPE_EMPTY_SELECTION", "هیچ فرشی انتخاب نشده؛ شماره‌ها را بنویسید یا از لیست بزنید.");

    /// <summary>«درصد» فقط برای سطر خرید فرش است.</summary>
    public static readonly MessageTemplate CostPercentBasis =
        new("E_COST_PERCENT_BASIS", "هزینه «درصد» ندارد؛ «هر متر»، «هر تخته» یا «کل» را بزنید.");

    /// <summary>دامنهٔ «این محموله» بی محموله.</summary>
    public static readonly MessageTemplate CostShipmentRequired =
        new("E_COST_SHIPMENT_REQUIRED", "محموله را مشخص کنید.");

    /// <summary>دامنهٔ «این ورودی» بی ورودی.</summary>
    public static readonly MessageTemplate CostBatchRequired =
        new("E_COST_BATCH_REQUIRED", "ورودی را مشخص کنید.");

    /// <summary>دامنهٔ «این نگله» بی نگله.</summary>
    public static readonly MessageTemplate CostBaleRequired =
        new("E_COST_BALE_REQUIRED", "{bale} را مشخص کنید.");

    /// <summary>نوع هزینهٔ غیرفعال برای ثبت تازه.</summary>
    public static readonly MessageTemplate CostCategoryInactive =
        new("E_COST_CATEGORY_INACTIVE", "این نوع هزینه غیرفعال است؛ نوع دیگری بزنید یا آن را در تنظیمات فعال کنید.");

    /// <summary>ویرایش هزینهٔ حذف‌شده.</summary>
    public static readonly MessageTemplate CostIsDeleted =
        new("E_COST_DELETED", "این هزینه حذف شده؛ اول آن را برگردانید.");

    /// <summary>مبلغ با رقم اعشار بیش از مجاز کارگاه؛ <c>{n}</c> تعداد رقم مجاز.</summary>
    public static readonly MessageTemplate CostDecimals =
        new("E_COST_DECIMALS", "مبلغ {currency} بیش از {n} رقم اعشار ندارد.");

    /// <summary>مبلغ یا نرخ بزرگ‌تر از گنجایش ستون.</summary>
    public static readonly MessageTemplate CostTooLarge =
        new("E_COST_TOO_LARGE", "این مبلغ از حد بیشتر است.");

    /// <summary>تغییر گروه نوعی که به کار رفته (CostCategoryGuard، D.5 #25؛ ۴۰۹).</summary>
    public static readonly MessageTemplate CostCategoryGroupLocked =
        new("E_COST_CATEGORY_GROUP_LOCKED", "دستهٔ هزینهٔ «{category}» عوض نمی‌شود، چون با آن هزینه یا قیمت خرید ثبت شده.");

    /// <summary>حذف نوعی که به کار رفته (۴۰۹؛ سرویس به‌جایش غیرفعال می‌کند).</summary>
    public static readonly MessageTemplate CostCategoryInUse =
        new("E_COST_CATEGORY_IN_USE", "«{category}» به کار رفته و حذف نمی‌شود؛ غیرفعالش کنید.");

    /// <summary>نام نوع تکراری در همان گروه.</summary>
    public static readonly MessageTemplate CostCategoryNameTaken =
        new("E_COST_CATEGORY_NAME_TAKEN", "نوع «{category}» از قبل هست.");

    /// <summary>درآمد منفی محموله.</summary>
    public static readonly MessageTemplate RevenueNegative =
        new("E_REVENUE_NEGATIVE", "فروش محموله منفی نمی‌شود.");

    /// <summary>ارز درآمد یا هزینه پشتیبانی نمی‌شود (دلار کارگاه نیست، ارز ناشناخته).</summary>
    public static readonly MessageTemplate CostCurrencyInvalid =
        new("E_COST_CURRENCY", "ارز این مبلغ پذیرفته نیست؛ تومان، ریال یا دلار را بزنید.");

    // ── تراشه‌ها و جمله‌ها (H.2، H.3b) ───────────────────────────────────

    /// <summary>تراشهٔ «تقسیم‌نشده» (هیچ فرشی).</summary>
    public static readonly MessageTemplate CostUnallocatedChip =
        new("I_COST_UNALLOCATED", "تقسیم‌نشده");

    /// <summary>تراشهٔ ثبتِ ناقص؛ <c>{amount}</c> باقی‌مانده.</summary>
    public static readonly MessageTemplate CostPartialChip =
        new("I_COST_PARTIAL", "{amount} {currency} هنوز تقسیم نشده.");

    /// <summary>جملهٔ «کل»: «۱۰٬۰۰۰٬۰۰۰ تومان کرایه بین ۲۴۲ تخته به نسبت متراژ تقسیم می‌شود».</summary>
    public static readonly MessageTemplate CostSentenceTotal =
        new("I_COST_SENTENCE_TOTAL", "{amount} {currency} {category} بین {pieces} تخته {method} تقسیم می‌شود");

    /// <summary>جملهٔ «هر متر»: «۸۵٬۰۰۰ تومان برای هر متر × ۲۰٫۸۱ متر = ۱٬۷۶۸٬۸۵۰ تومان».</summary>
    public static readonly MessageTemplate CostSentencePerM2 =
        new("I_COST_SENTENCE_PER_M2", "{rate} {currency} برای هر متر × {area} متر = {amount} {currency}");

    /// <summary>جملهٔ «هر تخته»: «۵۰٬۰۰۰ تومان برای هر تخته × ۵۲ تخته = ۲٬۶۰۰٬۰۰۰ تومان».</summary>
    public static readonly MessageTemplate CostSentencePerPiece =
        new("I_COST_SENTENCE_PER_PIECE", "{rate} {currency} برای هر تخته × {pieces} تخته = {amount} {currency}");

    /// <summary>جملهٔ رسید منجمد: «رسید ۱٬۷۶۸٬۸۵۰ تومان شستشو بین ۵۲ تخته به نسبت متراژ (اندازهٔ اول) تقسیم می‌شود».</summary>
    public static readonly MessageTemplate CostSentenceReceipt =
        new("I_COST_SENTENCE_RECEIPT", "رسید {amount} {currency} {category} بین {pieces} تخته {method} تقسیم می‌شود");

    /// <summary>toast بعد از ثبت (H.2): «ثبت شد · ۱۰٬۰۰۰٬۰۰۰ تومان بین ۵۲ تخته تقسیم شد».</summary>
    public static readonly MessageTemplate CostSavedToast =
        new("I_COST_SAVED", "ثبت شد · {amount} {currency} بین {pieces} تخته تقسیم شد");

    /// <summary>toast حذف (کنارش «برگردان»).</summary>
    public static readonly MessageTemplate CostDeletedToast =
        new("I_COST_DELETED", "هزینهٔ شمارهٔ {number} حذف شد");

    public static readonly MessageTemplate CostRestoredToast =
        new("I_COST_RESTORED", "هزینهٔ شمارهٔ {number} برگشت");

    // روش‌ها، برای جمله و انتخابگر «[تغییر]»
    public static readonly MessageTemplate CostMethodByArea = new("I_COST_METHOD_BY_AREA", "به نسبت متراژ");
    public static readonly MessageTemplate CostMethodByCount = new("I_COST_METHOD_BY_COUNT", "به تعداد تخته");
    public static readonly MessageTemplate CostMethodEqual = new("I_COST_METHOD_EQUAL", "مساوی برای هر ردیف");
    public static readonly MessageTemplate CostMethodByDeclared = new("I_COST_METHOD_BY_DECLARED", "به نسبت ارزش گمرکی");
    public static readonly MessageTemplate CostMethodByPurchase = new("I_COST_METHOD_BY_PURCHASE", "به نسبت قیمت خرید");
    public static readonly MessageTemplate CostMethodManual = new("I_COST_METHOD_MANUAL", "با سهم‌هایی که خودتان نوشتید");

    /// <summary>پسوند پایهٔ اندازهٔ اول: «به نسبت متراژ (اندازهٔ اول)».</summary>
    public static readonly MessageTemplate CostAreaBasisIntake = new("I_COST_AREA_INTAKE", "(اندازهٔ اول)");

    // «برای»: متن دامنه در فهرست
    public static readonly MessageTemplate CostTargetShipment =
        new("I_COST_TARGET_SHIPMENT", "محمولهٔ {number} ({pieces} تخته)");

    public static readonly MessageTemplate CostTargetBales =
        new("I_COST_TARGET_BALES", "{baleOf} {numbers} ({pieces} تخته)");

    public static readonly MessageTemplate CostTargetBatch =
        new("I_COST_TARGET_BATCH", "ورودی {number} ({pieces} تخته)");

    public static readonly MessageTemplate CostTargetRugs =
        new("I_COST_TARGET_RUGS", "{n} فرش ({pieces} تخته)");

    public static readonly MessageTemplate CostTargetOneRug =
        new("I_COST_TARGET_ONE_RUG", "فرش {serial}");

    /// <summary>عکس «خرید یکجای این ورودی» (D.2): «۱۲ فرش ورودی ۳».</summary>
    public static readonly MessageTemplate CostScopeNoteBatch =
        new("I_COST_SCOPE_BATCH", "{n} فرش ورودی {number}");

    /// <summary>«همهٔ انبار» هنگام ذخیره: «۲۴۲ فرش انبار در ۱۴۰۵/۰۶/۱۵».</summary>
    public static readonly MessageTemplate CostScopeNoteStock =
        new("I_COST_SCOPE_STOCK", "{n} فرش انبار در {date}");

    /// <summary>فهرست شماره‌ها: «۱۲ فرش از شماره‌های نوشته‌شده».</summary>
    public static readonly MessageTemplate CostScopeNoteSerials =
        new("I_COST_SCOPE_SERIALS", "{n} فرش از شماره‌های نوشته‌شده");

    /// <summary>فیلتر فهرست: «۱۲ فرش از فهرست فرش‌ها در ۱۴۰۵/۰۶/۱۵».</summary>
    public static readonly MessageTemplate CostScopeNoteFilter =
        new("I_COST_SCOPE_FILTER", "{n} فرش از فهرست فرش‌ها در {date}");

    /// <summary>بی شماره‌ای که پیدا نشد: «۳۷۷۹ پیدا نشد».</summary>
    public static readonly MessageTemplate CostSerialNotFound =
        new("I_COST_SERIAL_NOT_FOUND", "{serial} پیدا نشد");
}
