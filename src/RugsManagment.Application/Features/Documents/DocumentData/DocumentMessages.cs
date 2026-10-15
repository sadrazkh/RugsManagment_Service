namespace RugsManagment.Application.Common;

/// <summary>
/// پیام‌های موتور سند (spec E.2، E.4، E.5، E.7): خطاهای قالب و عبارت، و هشدارهای ساختن سند.
///
/// چرا این‌جا و نه در <c>Messages.fa.Documents.cs</c>: موتور سند (P6-engine) پیش از سیم‌کشی (P6-wiring) ساخته می‌شود و
/// اعتبارسنج و سازنده همین کدها را برمی‌گردانند؛ آن فایل کدهای بخش وب و خروجی را اضافه می‌کند و این کدها را تکرار
/// نمی‌کند (آزمون «کد تکراری» می‌شکند).
/// </summary>
public static partial class Messages
{
    // ── قالب (E.2، LayoutValidator) ─────────────────────────────────────

    public static readonly MessageTemplate LayoutJsonInvalid =
        new("E_LAYOUT_JSON", "متن قالب خوانده نشد؛ جایی از آن درست نوشته نشده.");

    public static readonly MessageTemplate LayoutVersion =
        new("E_LAYOUT_VERSION", "این نسخهٔ قالب شناخته نیست.");

    public static readonly MessageTemplate LayoutValue =
        new("E_LAYOUT_VALUE", "مقدار «{value}» این‌جا درست نیست.");

    public static readonly MessageTemplate LayoutRequired =
        new("E_LAYOUT_REQUIRED", "«{label}» نوشته نشده.");

    public static readonly MessageTemplate LayoutSheets =
        new("E_LAYOUT_SHEETS", "قالب باید درست یک برگهٔ اصلی و حداکثر یک برگهٔ خلاصه داشته باشد.");

    public static readonly MessageTemplate LayoutSheetName =
        new("E_LAYOUT_SHEET_NAME", "نام برگهٔ «{value}» در اکسل مجاز نیست یا تکراری است.");

    public static readonly MessageTemplate LayoutTooManyColumns =
        new("E_LAYOUT_TOO_MANY_COLUMNS", "قالب بیش از {max} ستون دارد.");

    public static readonly MessageTemplate LayoutDuplicateKey =
        new("E_LAYOUT_DUPLICATE_KEY", "کلید ستون «{key}» تکراری است.");

    public static readonly MessageTemplate LayoutDuplicateFooter =
        new("E_LAYOUT_DUPLICATE_FOOTER", "شناسهٔ «{key}» در خانه‌های پانویس تکراری است.");

    public static readonly MessageTemplate LayoutDuplicateStyle =
        new("E_LAYOUT_DUPLICATE_STYLE", "سبک «{key}» دو بار تعریف شده.");

    public static readonly MessageTemplate LayoutUnknownField =
        new("E_LAYOUT_UNKNOWN_FIELD", "فیلد «{key}» در فهرست فیلدها نیست.");

    public static readonly MessageTemplate LayoutUnknownColumn =
        new("E_LAYOUT_UNKNOWN_COLUMN", "ستونی با کلید «{key}» نیست.");

    public static readonly MessageTemplate LayoutUnknownStyle =
        new("E_LAYOUT_UNKNOWN_STYLE", "سبک «{key}» تعریف نشده.");

    public static readonly MessageTemplate LayoutUnknownFooter =
        new("E_LAYOUT_UNKNOWN_FOOTER", "خانهٔ پانویسی با شناسهٔ «{key}» نیست.");

    public static readonly MessageTemplate LayoutLedgerField =
        new("E_LAYOUT_LEDGER_FIELD", "«{key}» جمع‌پذیر نیست و پانویس را نمی‌شود با آن سنجید.");

    public static readonly MessageTemplate LayoutCycle =
        new("E_LAYOUT_CYCLE", "فرمول ستون «{key}» دور می‌زند و به خودش برمی‌گردد.");

    public static readonly MessageTemplate LayoutLetterColumn =
        new("E_LAYOUT_LETTER_COL", "«{key}» حرف ستون اکسل است؛ کلید ستون را بنویسید تا با جابه‌جا شدن ستون‌ها درست بماند.");

    public static readonly MessageTemplate LayoutSpan =
        new("E_LAYOUT_SPAN", "خانهٔ «{key}» از ستون‌های جدول بیرون می‌زند.");

    public static readonly MessageTemplate LayoutOverlap =
        new("E_LAYOUT_OVERLAP", "خانهٔ «{key}» روی خانهٔ دیگری افتاده؛ خانه‌های ادغام‌شده نباید روی هم بیفتند.");

    public static readonly MessageTemplate LayoutHiddenAnchor =
        new("E_LAYOUT_HIDDEN_ANCHOR", "خانهٔ «{key}» روی ستونی است که گاهی پنهان می‌شود.");

    public static readonly MessageTemplate LayoutQtyRequired =
        new("E_LAYOUT_QTY_REQUIRED", "در قالب «هر تخته یک ردیف» ستون «تعداد» لازم است؛ بدون آن دسته‌ها درست شمرده نمی‌شوند.");

    public static readonly MessageTemplate LayoutTotals =
        new("E_LAYOUT_TOTALS", "«{key}» در جمع خلاصه آمده ولی ستونی از خلاصه نیست.");

    public static readonly MessageTemplate LayoutStyleValue =
        new("E_LAYOUT_STYLE_VALUE", "مقدار «{value}» در سبک «{key}» درست نیست.");

    public static readonly MessageTemplate LayoutColumnSource =
        new("E_LAYOUT_COLUMN_SOURCE", "ستون «{key}» باید یا فیلد داشته باشد یا فرمول؛ نه هر دو و نه هیچ‌کدام.");

    public static readonly MessageTemplate LayoutColumnType =
        new("E_LAYOUT_COLUMN_TYPE", "نتیجهٔ «{key}» باید عدد یا متن باشد.");

    public static readonly MessageTemplate LayoutFooterContent =
        new("E_LAYOUT_FOOTER_CONTENT", "خانهٔ پانویس «{key}» باید درست یکی از متن، فرمول یا جمع را داشته باشد.");

    public static readonly MessageTemplate LayoutWidth =
        new("E_LAYOUT_WIDTH", "پهنای ستون «{key}» باید بیشتر از صفر و کمتر از ۲۵۶ باشد.");

    // ── عبارت (E.4) ─────────────────────────────────────────────────────

    public static readonly MessageTemplate ExprSyntax =
        new("E_EXPR_SYNTAX", "فرمول درست نوشته نشده (نزدیک نویسهٔ {n}).");

    public static readonly MessageTemplate ExprEmpty =
        new("E_EXPR_EMPTY", "فرمول خالی است.");

    public static readonly MessageTemplate ExprTooLong =
        new("E_EXPR_TOO_LONG", "فرمول بیش از {max} نویسه است.");

    public static readonly MessageTemplate ExprTooDeep =
        new("E_EXPR_TOO_DEEP", "فرمول بیش از اندازه تودرتو است.");

    public static readonly MessageTemplate ExprUnknownFunction =
        new("E_EXPR_UNKNOWN_FUNCTION", "تابع «{key}» شناخته نیست.");

    public static readonly MessageTemplate ExprArguments =
        new("E_EXPR_ARGS", "تابع «{key}» این تعداد ورودی نمی‌گیرد.");

    public static readonly MessageTemplate ExprNumberExpected =
        new("E_EXPR_NUMBER_EXPECTED", "این‌جا عدد لازم است (نزدیک نویسهٔ {n}).");

    public static readonly MessageTemplate ExprBoolExpected =
        new("E_EXPR_BOOL_EXPECTED", "این‌جا شرطی لازم است که درست یا نادرست باشد (نزدیک نویسهٔ {n}).");

    public static readonly MessageTemplate ExprDateExpected =
        new("E_EXPR_DATE_EXPECTED", "این تابع تاریخ می‌خواهد (نزدیک نویسهٔ {n}).");

    public static readonly MessageTemplate ExprTextCompare =
        new("E_EXPR_TEXT_COMPARE", "متن را نمی‌شود مقایسه کرد؛ مقایسه فقط برای عدد است.");

    public static readonly MessageTemplate ExprMixed =
        new("E_EXPR_MIXED", "دو نتیجهٔ این تابع باید هم‌نوع باشند (هر دو عدد یا هر دو متن).");

    public static readonly MessageTemplate ExprConcatType =
        new("E_EXPR_CONCAT_TYPE", "تاریخ یا شرط را نمی‌شود مستقیم به متن چسباند؛ تاریخ را با تابع تاریخ شمسی بنویسید.");

    public static readonly MessageTemplate ExprAggregateContext =
        new("E_EXPR_AGG_CONTEXT", "جمع و شمارش فقط در پانویس می‌آید.");

    public static readonly MessageTemplate ExprAggregateArgument =
        new("E_EXPR_AGG_ARG", "ورودی «{key}» باید یک ستون باشد.");

    public static readonly MessageTemplate ExprColumnContext =
        new("E_EXPR_COLUMN_CONTEXT", "ستون «{key}» این‌جا در دسترس نیست؛ در پانویس ستون فقط درون جمع می‌آید.");

    public static readonly MessageTemplate ExprFieldScope =
        new("E_EXPR_FIELD_SCOPE", "فیلد «{key}» این‌جا معنا ندارد (مثلاً فیلد فرش در عنوان).");

    // ── ساختن سند (E.5، E.7) ────────────────────────────────────────────

    public static readonly MessageTemplate DocumentEmptyBale =
        new("W_EMPTY_BALE", "{baleOf} {n} خالی بود و در خروجی نیامد.");

    public static readonly MessageTemplate DocumentBaleTooLong =
        new("W_BALE_TOO_LONG", "{baleOf} {n} از یک صفحه بلندتر است و میان دو صفحه شکست.");

    public static readonly MessageTemplate DocumentCheckMismatch =
        new("W_CHECK_MISMATCH", "«{label}» در {baleOf} {n} با حساب محموله نمی‌خواند: {value} به‌جای {total}.");
}
