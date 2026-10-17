using System.Text.Json;

namespace RugsManagment.Application.DTOs.Grid;

/// <summary>
/// یک ستون جدول (spec I.3، G.2) — آینهٔ TS: <c>GridColumn</c> در <c>CA/lib/types/grid.ts</c>.
/// </summary>
/// <param name="Key">کلید FieldCatalog (<c>rug.serial</c>، <c>batch.seller</c>، <c>purchase.price</c>، …).</param>
/// <param name="Label">برچسب فارسی سرستون.</param>
/// <param name="Type">نوع خانه: serial | text | combo | select | int | dim | money | money+unit | percent | readonly.</param>
/// <param name="Width">پهنا به <c>ch</c>، تا با اندازهٔ نوشته (TextScale) بزرگ شود.</param>
/// <param name="Sticky">مقدار به ردیف تازهٔ بعدی می‌رسد («چسبان»).</param>
/// <param name="ReadOnly">نوشتنی نیست؛ مقدارش در <see cref="GridRowDto.Derived"/> است.</param>
/// <param name="Required">ردیف تازه بی آن ذخیره نمی‌شود.</param>
/// <param name="Options">گزینه‌های ثابت (select)؛ null برای بقیه.</param>
/// <param name="Money">مبلغ تومانی است؛ برای غیر Business اصلاً نمی‌آید (A.5).</param>
/// <param name="TabStop">false: Tab از روی خانه می‌پرد؛ خانهٔ فقط‌خواندنی هرگز ایستگاه Tab نیست.</param>
public record GridColumnDto(
    string Key,
    string Label,
    string Type,
    decimal Width,
    bool Sticky,
    bool ReadOnly,
    bool Required,
    IReadOnlyList<string>? Options,
    bool Money,
    bool TabStop = true);

/// <summary>
/// یک ردیف جدول (spec I.3). <see cref="Values"/> مقدار همهٔ خانه‌های نوشتنیِ جدول است (حتی ستون‌های پنهان، تا ادغام
/// سه‌طرفه همه را ببیند)؛ <see cref="Derived"/> مقدارهای حساب‌شده و خانه‌های فقط‌خواندنی، با همان کلید ستون
/// (<c>rug.areaM2</c>، <c>rug.purchaseTotal</c>، …) و کلیدهای قراردادی <c>row.canDelete</c>، <c>batch.id</c>، ….
/// </summary>
public record GridRowDto(
    Guid RugId,
    uint Version,
    string? Sku,
    IReadOnlyDictionary<string, JsonElement?> Values,
    IReadOnlyDictionary<string, JsonElement?> Derived,
    Guid? LineId = null,
    uint? LineVersion = null /* فقط BalesGridSource پر می‌کند */);

/// <summary>داده‌های یک جدول (<c>GET /api/grid/{gridId}</c>).</summary>
/// <param name="NextSerial">شمارهٔ پیشنهادی بعدی (G.4)؛ null در حالت «دستی».</param>
public record GridDataDto(
    IReadOnlyList<GridColumnDto> Columns,
    IReadOnlyList<GridRowDto> Rows,
    GridTotalsDto Totals,
    int? NextSerial);

/// <summary>
/// جمع‌های پانویس. شمارش همیشه Σ تعداد به «تخته» است (A.2). <see cref="Purchase"/> برای غیر Business null.
/// </summary>
/// <param name="Rows">تعداد ردیف‌ها (در جدول انبار: همهٔ ردیف‌های فیلترشده، نه فقط این صفحه).</param>
/// <param name="Groups">
/// جمع هر ورودی (فروشنده) در جدول ورود — پانویس «حاج رضا: ۱۲ تخته · ۳۴٫۵ متر» و دکمهٔ «هزینه یا خرید یکجای این ورودی»
/// برای همان ورودی (E7)؛ null برای جدول‌های دیگر.
/// </param>
public record GridTotalsDto(
    int Rows,
    int Pieces,
    decimal AreaM2,
    decimal? Purchase,
    IReadOnlyList<GridGroupTotalsDto>? Groups = null);

/// <summary>جمع یک ورودی (فروشندهٔ یک روز) در پانویس جدول ورود.</summary>
public record GridGroupTotalsDto(
    Guid? BatchId,
    int? BatchNumber,
    string? SellerName,
    int Rows,
    int Pieces,
    decimal AreaM2,
    decimal? Purchase);

/// <summary>درخواست ذخیرهٔ دسته‌ای (<c>POST /api/grid/rugs/batch</c>، G.6).</summary>
/// <param name="GridId">جدولی که ردیف‌ها از آن آمده‌اند (<c>intake</c>، <c>rugs</c>، …).</param>
/// <param name="Batch">روز و فروشندهٔ پیش‌فرض ردیف‌های تازه (جدول ورود).</param>
/// <param name="ShipmentId">محموله (جدول نگله‌ها، فاز ۴)؛ این‌جا استفاده نمی‌شود.</param>
public record GridBatchRequest(
    string GridId,
    GridBatchContext? Batch,
    Guid? ShipmentId,
    IReadOnlyList<GridOp> Ops);

/// <summary>
/// ورودی ردیف‌ها: یک ورودی مشخص (<see cref="Id"/>)، یا روز (<see cref="ReceivedAt"/>، روز ایران؛ پیش‌فرض امروز) و
/// فروشندهٔ پیش‌فرض ردیف تازه‌ای که خانهٔ فروشنده ندارد.
/// </summary>
public record GridBatchContext(Guid? Id, string? SellerName, DateOnly? ReceivedAt);

/// <summary>
/// یک عمل روی یک ردیف. <see cref="RugId"/> خالی یعنی ردیف تازه (یکتا با <see cref="ClientRowId"/>)؛
/// <see cref="Version"/> نسخه‌ای که کلاینت دیده بود؛ <see cref="BaseValues"/> مقدار پیشینِ همان کلیدها (پایهٔ ادغام
/// سه‌طرفه).
/// </summary>
public record GridOp(
    Guid ClientRowId,
    Guid? RugId,
    uint? Version,
    IReadOnlyDictionary<string, JsonElement?> Values,
    IReadOnlyDictionary<string, JsonElement?>? BaseValues,
    bool Delete,
    uint? LineVersion = null);

/// <summary>وضعیت یک ردیف بعد از ذخیره. فقط افزودنی با عدد صریح.</summary>
public enum GridRowStatus
{
    /// <summary>ذخیره شد (یا چیزی برای ذخیره نبود)</summary>
    Saved = 0,

    /// <summary>خانه‌ای نادرست است، یا اجازه نیست؛ <see cref="GridOpResult.Errors"/> را ببینید</summary>
    Invalid = 1,

    /// <summary>همان خانه را کس دیگری هم عوض کرد؛ <see cref="GridOpResult.Current"/> مقدار سرور است</summary>
    Conflict = 2,

    /// <summary>فرش حذف شد، یا دیگر نیست</summary>
    Deleted = 3
}

/// <summary>نتیجهٔ یک عمل.</summary>
/// <param name="Derived">مقدارهای حساب‌شدهٔ تازه (متراژ، جمع خرید، …).</param>
/// <param name="Errors">خطای هر خانه (کلید فیلد و پیام فارسی).</param>
/// <param name="Current">مقدار فعلیِ همهٔ خانه‌های ردیف در سرور (بعد از ذخیره، یا در تعارض).</param>
/// <param name="NextFree">
/// اولین شمارهٔ آزاد وقتی شماره با فرش دیگری همپوشانی داشت («شمارهٔ ۴۰۰۵ آزاد است؛ همان را بگذارم؟»، G.4).
/// </param>
public record GridOpResult(
    Guid ClientRowId,
    Guid? RugId,
    uint? Version,
    string? Sku,
    GridRowStatus Status,
    IReadOnlyDictionary<string, JsonElement?>? Derived,
    IReadOnlyList<FieldErrorDto>? Errors,
    IReadOnlyDictionary<string, JsonElement?>? Current,
    uint? LineVersion = null,
    int? NextFree = null);

/// <summary>خطای یک خانه.</summary>
public record FieldErrorDto(string Field, string Message);

/// <summary>پاسخ ذخیرهٔ دسته‌ای: نتیجهٔ هر ردیف، جمع‌های تازهٔ روز (جدول ورود) و شمارهٔ پیشنهادی بعدی.</summary>
public record GridBatchResultDto(
    IReadOnlyList<GridOpResult> Results,
    GridTotalsDto? Totals,
    int? NextSerial);

/// <summary>
/// چیدمان ستون‌های یک جدول (<c>GET/PUT /api/settings/grid-layout/{gridId}</c>). در PUT ترتیب، برچسب، پهنا، چسبان و
/// ایستگاه Tab از بدنه خوانده می‌شود و ستونی که نیامده پنهان است؛ نسخهٔ کهنه → ۴۰۹.
/// </summary>
/// <param name="Version">xmin ردیف چیدمان کارگاه؛ ۰ یعنی هنوز پیش‌فرض برنامه.</param>
/// <param name="Available">همهٔ ستون‌هایی که این جدول می‌تواند نشان دهد (برای برگرداندن ستون پنهان)؛ در PUT نادیده.</param>
public record GridLayoutDto(
    string GridId,
    IReadOnlyList<GridColumnDto> Columns,
    uint Version,
    IReadOnlyList<GridColumnDto>? Available = null);
