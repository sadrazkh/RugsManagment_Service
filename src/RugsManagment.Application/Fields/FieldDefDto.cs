namespace RugsManagment.Application.Fields;

/// <summary>
/// یک فیلد در کاتالوگ فیلدها (spec E.3، <c>GET /api/fields?purpose=grid|document&amp;kind=</c>) — آینهٔ TS:
/// <c>FieldDef</c> در <c>CA/lib/types/rugs.ts</c>.
///
/// یک کلید یک معنا دارد در همه‌جا: ستون جدول ورود، خانهٔ فرم فرش (<c>RugFieldWriter</c>)، ستون قالب اکسل و چاپ،
/// پیشنهاد مقدار. پس هر خانه‌ای که کاربر می‌بیند یا می‌نویسد از همین فهرست می‌آید، نه از یک فهرست ثابت در هر صفحه.
/// </summary>
/// <param name="Key">کلید پایدار (<c>rug.serial</c>، <c>rug.custom.bg_color</c>، <c>purchase.price</c>، …).</param>
/// <param name="Label">برچسب فارسی برای بازرگان.</param>
/// <param name="Scope">دامنه: <see cref="FieldScope"/> (rug، line، bale، shipment، tenant، terms، purchase).</param>
/// <param name="DataType">نوع داده: <see cref="FieldDataType"/>.</param>
/// <param name="DefaultFormat">قالب پیش‌فرض نمایش (عدد ثابت، مثل «0.00» یا «#,0»)؛ خالی برای متن.</param>
/// <param name="Aggregatable">جمع‌پذیر است (تعداد، متراژ، مبلغ) و در پانویس جمع می‌خورد.</param>
/// <param name="Money">مبلغ تومانی است و فقط برای Business می‌آید (A.5). دلار گمرکی پول نیست.</param>
/// <param name="GridEditable">در جدول نوشتنی است (از راه <c>RugFieldWriter</c>).</param>
public record FieldDefDto(
    string Key,
    string Label,
    string Scope,
    string DataType,
    string DefaultFormat,
    bool Aggregatable,
    bool Money,
    bool GridEditable);

/// <summary>هدف درخواست کاتالوگ (<c>purpose</c>).</summary>
public static class FieldPurpose
{
    /// <summary>ستون‌های جدول ورود و انبار (خانه‌های خرید هم).</summary>
    public const string Grid = "grid";

    /// <summary>ستون‌های قالب سند (اکسل و چاپ): فقط فیلدهای سفارشی «خروجی‌پذیر».</summary>
    public const string Document = "document";

    /// <summary>هدف شناخته‌شده است یا نه (کنترلر مقدار ناشناخته را ۴۰۰ می‌دهد).</summary>
    public static bool IsKnown(string? purpose) => purpose is Grid or Document;
}

/// <summary>دامنهٔ فیلد (E.3).</summary>
public static class FieldScope
{
    public const string Rug = "rug";
    public const string Line = "line";
    public const string Bale = "bale";
    public const string Shipment = "shipment";
    public const string Tenant = "tenant";
    public const string Terms = "terms";
    public const string Purchase = "purchase";
}

/// <summary>نوع دادهٔ فیلد؛ همان نام‌های نوع ستون جدول (G.2) تا جدول بی‌نگاشت از کاتالوگ ستون بسازد.</summary>
public static class FieldDataType
{
    /// <summary>شماره («3778» یا «3911 - 3960»)</summary>
    public const string Serial = "serial";

    /// <summary>عدد صحیح (تعداد، رجشمار)</summary>
    public const string Int = "int";

    /// <summary>عدد اعشاری آزاد (فیلد سفارشی عددی)</summary>
    public const string Number = "number";

    /// <summary>متن آزاد</summary>
    public const string Text = "text";

    /// <summary>متن با پیشنهاد مقدارهای قبلی (<c>/api/rugs/values</c>)</summary>
    public const string Combo = "combo";

    /// <summary>انتخاب از گزینه‌های ثابت</summary>
    public const string Select = "select";

    /// <summary>بله/نه</summary>
    public const string Bool = "bool";

    /// <summary>تاریخ (ISO، نمایش شمسی)</summary>
    public const string Date = "date";

    /// <summary>ضلع به متر، دو رقم اعشار</summary>
    public const string Dim = "dim";

    /// <summary>متراژ (خواندنی)</summary>
    public const string Area = "area";

    /// <summary>مبلغ تومان/ریال</summary>
    public const string Money = "money";

    /// <summary>مبلغ با چیپ واحد («هر متر | هر تخته | کل»)</summary>
    public const string MoneyUnit = "money+unit";

    /// <summary>درصد</summary>
    public const string Percent = "percent";
}
