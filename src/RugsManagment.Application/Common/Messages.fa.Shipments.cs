namespace RugsManagment.Application.Common;

/// <summary>
/// پیام‌های محموله، نگله، ارزش گمرکی و قفل «قطعی» (فاز محموله؛ spec C، H.3، H.3b) — آینه در
/// <c>CA/lib/messages/shipments.ts</c>. هر «نگله» با نشانه‌های <c>{bale}</c> / <c>{baleOf}</c> نوشته می‌شود تا کارگاهی که
/// «عدل» می‌گوید «عدل ۳» ببیند (H.3، E10)؛ عدد و مساحت هم نشانه‌اند، هیچ مثال ثابتی در متن نیست.
/// کدهای پیش‌پرواز قالب (<c>W_NO_USD_RATE</c>، <c>W_TEMPLATE_FIELD_EMPTY</c>) مال فاز اسناد است.
/// </summary>
public static partial class Messages
{
    // ── آمادگی (C.6) ─────────────────────────────────────────────────────

    /// <summary>محموله هیچ خطی ندارد (بازدارنده).</summary>
    public static readonly MessageTemplate ReadinessEmptyShipment =
        new("E_EMPTY_SHIPMENT", "هنوز فرشی در این محموله نیست.");

    /// <summary>یک نگلهٔ خالی (بازدارنده)؛ <c>{n}</c> شمارهٔ نگله.</summary>
    public static readonly MessageTemplate ReadinessEmptyBale =
        new("E_EMPTY_BALE", "{baleOf} {n} خالی است؛ یا فرش در آن بگذارید یا حذفش کنید.");

    /// <summary>فرش‌هایی که اندازهٔ نهایی ندارند (بازدارنده وقتی ترجیح کارگاه می‌خواهد).</summary>
    public static readonly MessageTemplate ReadinessNoFinalSize =
        new("E_NO_FINAL_SIZE", "اندازهٔ نهایی {n} فرش ثبت نشده.");

    /// <summary>فرش‌های یک مقدار فیلد کلید که نرخی ندارند (بازدارنده)؛ <c>{key}</c> همان مقدار («کاشان»).</summary>
    public static readonly MessageTemplate ReadinessNoDeclaredValue =
        new("E_NO_DECLARED_VALUE", "ارزش گمرکی {n} فرش «{key}» معلوم نیست.");

    /// <summary>فرش‌هایی که خودِ فیلد کلیدشان خالی است و نرخ پیش‌فرض هم نیست (بازدارنده)؛ <c>{label}</c> نام فیلد.</summary>
    public static readonly MessageTemplate ReadinessNoDeclaredKey =
        new("E_NO_DECLARED_KEY", "ارزش گمرکی {n} فرش معلوم نیست، چون «{label}» آن‌ها خالی است.");

    /// <summary>نگله‌ای بالاتر از هدف + تلورانس (هشدار)؛ <c>{area}</c> متراژ نگله، <c>{target}</c> متراژ هدف.</summary>
    public static readonly MessageTemplate ReadinessBaleOverTarget =
        new("W_BALE_OVER_TARGET", "{baleOf} {n} شد {area} متر؛ از {target} متر هدف بیشتر است.");

    /// <summary>فرش‌های بی‌شماره (هشدار).</summary>
    public static readonly MessageTemplate ReadinessNoSerial =
        new("W_NO_SERIAL", "{n} فرش شماره ندارد.");

    // ── قفل «قطعی» (C.8) ─────────────────────────────────────────────────

    /// <summary>تغییر فرشی که در محمولهٔ قطعی است — متن بازرگان؛ <c>{number}</c> شمارهٔ محموله.</summary>
    public static readonly MessageTemplate ShipmentLocked =
        new("E_SHIPMENT_LOCKED", "این فرش در محمولهٔ قطعی‌شدهٔ {number} است. برای تغییر، اول «باز کردن دوباره» را بزنید.");

    /// <summary>تغییر خودِ محموله، نگله‌ها یا ردیف‌هایش وقتی قطعی است — متن بازرگان.</summary>
    public static readonly MessageTemplate ShipmentLockedHeader =
        new("E_SHIPMENT_LOCKED_HEADER", "محمولهٔ {number} قطعی شده است. برای تغییر، اول «باز کردن دوباره» را بزنید.");

    /// <summary>همان قفل برای انباردار، که «باز کردن دوباره» را ندارد.</summary>
    public static readonly MessageTemplate ShipmentLockedOperator =
        new("E_SHIPMENT_LOCKED_OPERATOR", "این محموله قطعی شده؛ برای تغییر به بازرگان بگویید.");

    // ── عضویت در نگله (C.3، C.10) ────────────────────────────────────────

    /// <summary>حذف فرشی که در نگله است؛ <c>{n}</c> شمارهٔ نگله.</summary>
    public static readonly MessageTemplate TrashBaled =
        new("E_TRASH_BALED", "اول این فرش را از {baleOf} {n} خارج کنید.");

    /// <summary>فروش فرشی که در نگله است.</summary>
    public static readonly MessageTemplate SaleBaled =
        new("E_SALE_BALED", "این فرش در {baleOf} {n} است؛ اول آن را از {bale} خارج کنید.");

    /// <summary>فرش همین حالا در نگلهٔ دیگری است (خط دوم برای یک فرش).</summary>
    public static readonly MessageTemplate LineRugInOtherBale =
        new("E_LINE_RUG_IN_OTHER", "این فرش در محمولهٔ {shipment} / {baleOf} {n} است.");

    /// <summary>فرش در سطل زباله است.</summary>
    public static readonly MessageTemplate LineRugTrashed =
        new("E_LINE_RUG_TRASHED", "این فرش در سطل زباله است؛ اول آن را بازگردانید.");

    /// <summary>فرش فروخته شده.</summary>
    public static readonly MessageTemplate LineRugSold =
        new("E_LINE_RUG_SOLD", "این فرش فروخته شده و در {bale} نمی‌رود.");

    /// <summary>نگلهٔ مقصد مال همین محموله نیست.</summary>
    public static readonly MessageTemplate LineBaleMismatch =
        new("E_LINE_BALE_MISMATCH", "این {bale} مال این محموله نیست.");

    /// <summary>ردیفی که عمل به آن اشاره می‌کند در این محموله نیست.</summary>
    public static readonly MessageTemplate LineNotInShipment =
        new("E_LINE_NOT_IN_SHIPMENT", "این ردیف در این محموله نیست؛ صفحه را تازه کنید.");

    /// <summary>نام عمل ناشناخته.</summary>
    public static readonly MessageTemplate LineOpUnknown =
        new("E_LINE_OP_UNKNOWN", "این کار شناخته نشد.");

    /// <summary>«افزودن» بی شناسه و بی شماره.</summary>
    public static readonly MessageTemplate LineRugRequired =
        new("E_LINE_RUG_REQUIRED", "شمارهٔ فرش را بنویسید.");

    /// <summary>عمل بی نگلهٔ مقصد.</summary>
    public static readonly MessageTemplate LineBaleRequired =
        new("E_LINE_BALE_REQUIRED", "{bale} را مشخص کنید.");

    /// <summary>نرخ دستیِ ردیف از کسی که بازرگان نیست.</summary>
    public static readonly MessageTemplate LineRateForbidden =
        new("E_LINE_RATE_FORBIDDEN", "نرخ دلار هر ردیف را فقط بازرگان عوض می‌کند.");

    /// <summary>پرسش پیش از انتقال از محمولهٔ باز دیگر؛ <c>{shipment}</c> شمارهٔ محموله، <c>{n}</c> شمارهٔ نگله.</summary>
    public static readonly MessageTemplate LineMoveQuestion =
        new("Q_LINE_MOVE", "این فرش در محمولهٔ {shipment} / {baleOf} {n} است؛ منتقل شود؟");

    /// <summary>پرسش پیش از انتقال از نگلهٔ دیگرِ همین محموله.</summary>
    public static readonly MessageTemplate LineMoveSameQuestion =
        new("Q_LINE_MOVE_SAME", "این فرش در {baleOf} {n} همین محموله است؛ منتقل شود؟");

    /// <summary>عددی که داخل یک دسته است؛ <c>{from}</c> و <c>{to}</c> دو سر بازه.</summary>
    public static readonly MessageTemplate LineSerialInRange =
        new("Q_LINE_SERIAL_IN_RANGE", "این شماره داخل دستهٔ {from} تا {to} است.");

    // ── نگله‌ها (C.2) ────────────────────────────────────────────────────

    /// <summary>حذف نگله‌ای که فرش دارد.</summary>
    public static readonly MessageTemplate BaleNotEmpty =
        new("E_BALE_NOT_EMPTY", "{baleOf} {n} خالی نیست؛ اول فرش‌هایش را خارج کنید.");

    /// <summary>شمارهٔ نگله تکراری.</summary>
    public static readonly MessageTemplate BaleNumberTaken =
        new("E_BALE_NUMBER_TAKEN", "{baleOf} {n} از پیش هست.");

    /// <summary>شمارهٔ نگله بیرون از بازه؛ <c>{max}</c> بزرگ‌ترین شمارهٔ مجاز.</summary>
    public static readonly MessageTemplate BaleNumberInvalid =
        new("E_BALE_NUMBER", "شمارهٔ {bale} باید از ۱ تا {max} باشد.");

    /// <summary>وزن منفی یا بیش از اندازه.</summary>
    public static readonly MessageTemplate BaleWeightInvalid =
        new("E_BALE_WEIGHT", "وزن {bale} را به کیلو و بزرگ‌تر از صفر بنویسید.");

    // ── محموله (C.1، C.7، C.9) ───────────────────────────────────────────

    /// <summary>عنوان پیش‌فرض محمولهٔ تازه؛ <c>{number}</c> شمارهٔ محموله.</summary>
    public static readonly MessageTemplate ShipmentDefaultTitle =
        new("I_SHIPMENT_TITLE", "اظهار {number}");

    /// <summary>حذف محموله‌ای که فرش دارد.</summary>
    public static readonly MessageTemplate ShipmentNotEmpty =
        new("E_SHIPMENT_NOT_EMPTY", "این محموله فرش دارد؛ فقط محمولهٔ بی‌فرش حذف می‌شود.");

    /// <summary>حذف محمولهٔ قطعی‌شده.</summary>
    public static readonly MessageTemplate ShipmentDeleteFinalized =
        new("E_SHIPMENT_DELETE_FINALIZED", "محمولهٔ قطعی‌شده حذف نمی‌شود؛ اول «باز کردن دوباره» را بزنید.");

    /// <summary>«باز کردن دوباره» روی محموله‌ای که باز است.</summary>
    public static readonly MessageTemplate ShipmentNotFinalized =
        new("E_SHIPMENT_NOT_FINALIZED", "این محموله قطعی نیست.");

    /// <summary>«قطعی کن» روی محموله‌ای که قطعی است.</summary>
    public static readonly MessageTemplate ShipmentAlreadyFinalized =
        new("E_SHIPMENT_ALREADY_FINALIZED", "این محموله پیش از این قطعی شده.");

    /// <summary>«قطعی کن» با کمبود بازدارنده؛ <c>{issues}</c> فهرست کوتاه تراشه‌ها.</summary>
    public static readonly MessageTemplate ShipmentNotReady =
        new("E_SHIPMENT_NOT_READY", "اول این‌ها را درست کنید: {issues}");

    // ── ارزش گمرکی (C.5) ─────────────────────────────────────────────────

    /// <summary>بازرگان کلیدی را اضافه می‌کند که در جدول هست؛ <c>{key}</c> همان کلید.</summary>
    public static readonly MessageTemplate DeclaredKeyExists =
        new("E_DECLARED_KEY_EXISTS", "ارزش گمرکی «{key}» از پیش در جدول هست؛ برای عوض کردنش به تنظیمات بروید.");

    /// <summary>نرخ منفی.</summary>
    public static readonly MessageTemplate DeclaredNegative =
        new("E_DECLARED_NEGATIVE", "ارزش گمرکی منفی نمی‌شود.");

    /// <summary>کلید خالی.</summary>
    public static readonly MessageTemplate DeclaredKeyRequired =
        new("E_DECLARED_KEY_REQUIRED", "نام ردیف را بنویسید؛ مثلاً همان محل بافت.");

    /// <summary>یک کلید دو بار در جایگزینی کامل؛ <c>{key}</c> همان کلید.</summary>
    public static readonly MessageTemplate DeclaredKeyDuplicate =
        new("E_DECLARED_KEY_DUPLICATE", "«{key}» دو بار در جدول آمده.");

    /// <summary>کلید بلندتر از ستون؛ <c>{n}</c> بیشترین طول.</summary>
    public static readonly MessageTemplate DeclaredKeyTooLong =
        new("E_DECLARED_KEY_TOO_LONG", "نام ردیف حداکثر {n} حرف است.");

    /// <summary>مهر کهنه (۴۰۹).</summary>
    public static readonly MessageTemplate DeclaredStale =
        new("E_DECLARED_STALE", "جدول ارزش گمرکی را همین الان کس دیگری عوض کرد؛ صفحه را تازه کنید و دوباره بنویسید.");

    /// <summary>غیرفعال یا حذف کردن فیلد سفارشی‌ای که کلید ارزش گمرکی است.</summary>
    public static readonly MessageTemplate DeclaredKeyFieldInUse =
        new("E_DECLARED_KEY_FIELD_IN_USE", "«{label}» کلید ارزش گمرکی است؛ اول در ترجیحات کلید دیگری انتخاب کنید.");

    /// <summary>بعد از افزودن کلید؛ <c>{n}</c> ردیف‌های دوباره‌حساب‌شده.</summary>
    public static readonly MessageTemplate DeclaredAdded =
        new("I_DECLARED_ADDED", "ارزش گمرکی «{key}» ثبت شد؛ {n} ردیف دوباره حساب شد.");

    // ── تراشهٔ منبع نرخ (H.3b «Chips») ────────────────────────────────────

    public static readonly MessageTemplate RateSourceOverride = new("I_RATE_OVERRIDE", "دستی");

    public static readonly MessageTemplate RateSourceTable = new("I_RATE_TABLE", "از جدول");

    public static readonly MessageTemplate RateSourceFallback = new("I_RATE_FALLBACK", "نرخ پیش‌فرض");

    public static readonly MessageTemplate RateSourceMissing = new("I_RATE_MISSING", "ندارد");
}
