namespace RugsManagment.Application.Common;

/// <summary>
/// پیام‌های جدول ورود و ستون‌های جدول (فاز ۳): ذخیرهٔ دسته‌ای، تعارض، حذف و برگرداندن فرش، ورودی (فروشندهٔ یک روز) و
/// چیدمان ستون‌ها — همان صدای H.3، آینه در <c>CA/lib/messages/grid.ts</c>.
///
/// نام فیلدها و کدها با پیشوند <c>Grid</c>/<c>Intake</c> است تا با فایل‌های partial فازهای دیگر (که ممکن است پیام
/// «ورودی پیدا نشد» خودشان را داشته باشند) نه در نام فیلد برخورد کند و نه در کد.
/// </summary>
public static partial class Messages
{
    // ── جدول و ذخیره ─────────────────────────────────────────────────────

    /// <summary>شناسهٔ جدولی که برنامه نمی‌شناسد (۴۰۴).</summary>
    public static readonly MessageTemplate GridUnknown =
        new("E_GRID_UNKNOWN", "این جدول پیدا نشد.");

    /// <summary>بیش از سقف ردیف در یک ذخیره؛ <c>{n}</c> سقف.</summary>
    public static readonly MessageTemplate GridTooManyOps =
        new("E_GRID_TOO_MANY_OPS", "در یک بار حداکثر {n} ردیف ذخیره می‌شود؛ بقیه را بار بعد بفرستید.");

    /// <summary>شناسهٔ ردیف خالی یا دوبار در یک درخواست.</summary>
    public static readonly MessageTemplate GridClientRowInvalid =
        new("E_GRID_CLIENT_ROW", "شناسهٔ ردیف خالی یا تکراری است؛ صفحه را تازه کنید.");

    /// <summary>ردیفی که فرشش دیگر نیست (حذف شده، یا هرگز نبوده).</summary>
    public static readonly MessageTemplate GridRowGone =
        new("E_GRID_ROW_GONE", "این فرش دیگر نیست؛ شاید کس دیگری حذفش کرده.");

    /// <summary>همان خانه را کس دیگری هم عوض کرد (H.3 «Conflict»؛ دکمه‌ها را جزیره می‌گذارد).</summary>
    public static readonly MessageTemplate GridConflict =
        new("E_GRID_CONFLICT", "این ردیف را همین الان کس دیگری هم عوض کرد.");

    /// <summary>ستون فقط‌خواندنی یا ستونی که این جدول ندارد؛ <c>{label}</c> برچسب یا کلید.</summary>
    public static readonly MessageTemplate GridColumnNotWritable =
        new("E_GRID_COLUMN", "خانهٔ «{label}» در این جدول نوشتنی نیست.");

    /// <summary>تاریخ «رسیدن» نادرست (۴۰۰).</summary>
    public static readonly MessageTemplate GridDateInvalid =
        new("E_GRID_DATE", "تاریخ رسیدن درست نیست؛ مثل ۱۴۰۵/۰۶/۱۵ بنویسید.");

    // ── حذف و برگرداندن فرش (G.3، G.6، E6) ───────────────────────────────

    /// <summary>برگرداندنِ فرشی که انباردار حق برگرداندنش را ندارد (۴۰۳).</summary>
    public static readonly MessageTemplate GridRestoreForbidden =
        new("E_RUG_RESTORE_FORBIDDEN", "این فرش را فقط بازرگان می‌تواند برگرداند.");

    /// <summary>حذف فرش فروخته‌شده.</summary>
    public static readonly MessageTemplate GridDeleteSold =
        new("E_RUG_DELETE_SOLD", "این فرش فروخته شده؛ اول فروش را لغو کنید.");

    /// <summary>فرشی که در سطل زباله نیست (برگرداندن).</summary>
    public static readonly MessageTemplate GridRestoreNotDeleted =
        new("E_RUG_NOT_DELETED", "این فرش حذف نشده که برگردد.");

    // ── ورودی (فروشندهٔ یک روز) ──────────────────────────────────────────

    /// <summary>ورودی‌ای که پیدا نشد یا مال کارگاه دیگر است (۴۰۴).</summary>
    public static readonly MessageTemplate IntakeBatchNotFound =
        new("E_INTAKE_BATCH_NOT_FOUND", "این ورودی پیدا نشد.");

    /// <summary>نام فروشندهٔ بیش از ستون؛ <c>{n}</c> سقف حرف.</summary>
    public static readonly MessageTemplate IntakeSellerTooLong =
        new("E_INTAKE_SELLER", "نام فروشنده حداکثر {n} حرف است.");

    // ── چیدمان ستون‌ها (تنظیمات) ─────────────────────────────────────────

    /// <summary>کلید ستونی که این جدول ندارد؛ <c>{key}</c> همان کلید.</summary>
    public static readonly MessageTemplate GridLayoutUnknownColumn =
        new("E_GRID_LAYOUT_COLUMN", "ستون «{key}» در این جدول نیست.");

    /// <summary>ستونی که نمی‌شود پنهانش کرد (ردیف تازه بی آن ذخیره نمی‌شود)؛ <c>{label}</c> برچسب.</summary>
    public static readonly MessageTemplate GridLayoutRequired =
        new("E_GRID_LAYOUT_REQUIRED", "ستون «{label}» را نمی‌شود پنهان کرد؛ ردیف تازه بی آن ذخیره نمی‌شود.");

    /// <summary>ستون دوبار آمده؛ <c>{label}</c> برچسب.</summary>
    public static readonly MessageTemplate GridLayoutDuplicate =
        new("E_GRID_LAYOUT_DUPLICATE", "ستون «{label}» دو بار آمده.");

    /// <summary>عنوان ستون بلندتر از سقف؛ <c>{n}</c> سقف حرف.</summary>
    public static readonly MessageTemplate GridLayoutLabel =
        new("E_GRID_LAYOUT_LABEL", "عنوان ستون حداکثر {n} حرف است.");

    /// <summary>پهنای ستون بیرون از بازه؛ <c>{min}</c> و <c>{max}</c>.</summary>
    public static readonly MessageTemplate GridLayoutWidth =
        new("E_GRID_LAYOUT_WIDTH", "پهنای ستون باید از {min} تا {max} باشد.");

    /// <summary>جدول بی هیچ ستون.</summary>
    public static readonly MessageTemplate GridLayoutEmpty =
        new("E_GRID_LAYOUT_EMPTY", "دست‌کم یک ستون را نگه دارید.");

    /// <summary>ذخیرهٔ چیدمانِ کهنه (۴۰۹).</summary>
    public static readonly MessageTemplate GridLayoutStale =
        new("E_GRID_LAYOUT_STALE", "این چیدمان را همین الان کس دیگری هم عوض کرد؛ صفحه را تازه کنید و دوباره بچینید.");

    /// <summary>تاریخچه: چیدمان ستون‌های یک جدول عوض شد؛ <c>{grid}</c> نام جدول.</summary>
    public static readonly MessageTemplate GridLayoutChanged =
        new("I_GRID_LAYOUT_CHANGED", "ستون‌های {grid} عوض شد.");

    /// <summary>تاریخچه: چیدمان ستون‌ها به پیش‌فرض برگشت؛ <c>{grid}</c> نام جدول.</summary>
    public static readonly MessageTemplate GridLayoutReset =
        new("I_GRID_LAYOUT_RESET", "ستون‌های {grid} به پیش‌فرض برگشت.");
}
