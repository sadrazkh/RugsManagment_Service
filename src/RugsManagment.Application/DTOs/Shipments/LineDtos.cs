using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.DTOs.Shipments;

/// <summary>نام عمل‌های <c>POST /api/shipments/{id}/lines/batch</c> (spec I.2).</summary>
public static class LineOpKinds
{
    /// <summary>افزودن فرش به نگله (با شناسه یا شماره)؛ شمارهٔ نگله‌ای که نیست همان نگله را می‌سازد.</summary>
    public const string Add = "add";

    /// <summary>بردن خط به نگلهٔ دیگری از همین محموله.</summary>
    public const string Move = "move";

    /// <summary>خارج کردن از نگله (فرش به انبار برمی‌گردد).</summary>
    public const string Remove = "remove";

    /// <summary>اندازهٔ نهایی (یا «همان اندازه»، یا پاک کردن).</summary>
    public const string Measure = "measure";

    /// <summary>نرخ دستیِ دلار هر متر برای یک خط (فقط بازرگان).</summary>
    public const string Rate = "rate";

    /// <summary>ترتیب خط‌های یک نگله.</summary>
    public const string Order = "order";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Add, Move, Remove, Measure, Rate, Order
    };
}

/// <summary>
/// یک عمل روی خط‌ها. هر عمل فقط عضوهای خودش را می‌خواند (I.2):
/// <list type="bullet">
/// <item><c>add</c>: <see cref="ClientOpId"/>، <see cref="BaleId"/> یا <see cref="BaleNumber"/>، <see cref="RugId"/> یا
/// <see cref="Serial"/>، اختیاری <see cref="Seq"/> (برای «برگردان» در همان جا) و <see cref="Move"/> (تأیید انتقال فرشی که
/// در نگلهٔ دیگری است).</item>
/// <item><c>move</c>: <see cref="LineId"/>، <see cref="LineVersion"/>، نگلهٔ مقصد، اختیاری <see cref="Seq"/>.</item>
/// <item><c>remove</c>: <see cref="LineId"/>، <see cref="LineVersion"/>.</item>
/// <item><c>measure</c>: <see cref="RugId"/>، <see cref="RugVersion"/>، و یکی از: طول و عرض نهایی، <see cref="SameAsIntake"/>،
/// <see cref="Clear"/>.</item>
/// <item><c>rate</c>: <see cref="LineId"/>، <see cref="LineVersion"/>، <see cref="UsdPerM2Override"/> (null یعنی برگشت به جدول).</item>
/// <item><c>order</c>: <see cref="BaleId"/> و <see cref="LineIds"/> به ترتیب تازه.</item>
/// </list>
/// </summary>
public record LineOp(
    string Op,
    string? ClientOpId = null,
    Guid? BaleId = null,
    int? BaleNumber = null,
    Guid? RugId = null,
    string? Serial = null,
    Guid? LineId = null,
    uint? LineVersion = null,
    int? Seq = null,
    uint? RugVersion = null,
    decimal? FinalLength = null,
    decimal? FinalWidth = null,
    bool? SameAsIntake = null,
    bool? Clear = null,
    decimal? UsdPerM2Override = null,
    IReadOnlyList<Guid>? LineIds = null,
    bool? Move = null);

/// <summary>چند عمل روی خط‌های یک محموله؛ هر عمل جدا ذخیره می‌شود و نتیجهٔ خودش را دارد.</summary>
public record LinesBatchRequest(IReadOnlyList<LineOp> Ops);

/// <summary>خطای یک عمل (کد ثابت + پیام فارسی + خانهٔ مربوط اگر هست).</summary>
public record LineOpErrorDto(string Code, string Message, string? Field = null);

/// <summary>نوع تطبیق شماره (همان <c>/api/rugs/by-serial</c>).</summary>
public static class LineMatchTypes
{
    public const string Exact = "exact";
    public const string InRange = "inRange";
}

/// <summary>
/// پرسشی که عمل «افزودن» پیش از انجام می‌پرسد (هیچ چیز ذخیره نشده):
/// <list type="bullet">
/// <item><c>Q_LINE_MOVE</c>: فرش در نگلهٔ دیگری از همین محموله یا محمولهٔ باز دیگری است — با <c>move: true</c> دوباره بفرستید.</item>
/// <item><c>Q_LINE_SERIAL_IN_RANGE</c>: شماره داخل یک دسته است — «[کل دسته را بگذار]» همان عمل را با <c>rugId</c> دسته
/// می‌فرستد و «[جدا کن]» پنجرهٔ برش را باز می‌کند.</item>
/// </list>
/// </summary>
public record LineOpPromptDto(
    string Code,
    string Message,
    Guid RugId,
    string? Serial,
    string MatchType,
    Guid? ShipmentId,
    int? ShipmentNumber,
    Guid? BaleId,
    int? BaleNumber,
    int Quantity = 1);

/// <summary>
/// نتیجهٔ یک عمل. موفق: <see cref="Row"/> ردیف تازه (برای «خارج کردن» همان ردیف پیش از خروج، تا «برگردان» نگله و Seq را
/// بداند؛ برای «ترتیب» null). ناموفق: <see cref="Errors"/>، یا <see cref="Prompt"/> وقتی سرور پیش از انجام می‌پرسد.
/// </summary>
public record LineOpResultDto(
    string? ClientOpId,
    bool Ok,
    IReadOnlyList<LineOpErrorDto> Errors,
    BaleRowDto? Row = null,
    LineOpPromptDto? Prompt = null,
    string? Op = null);

/// <summary>پاسخ دسته‌ای: نتیجهٔ هر عمل به همان ترتیب، خلاصهٔ تازهٔ همهٔ نگله‌ها و جمع محموله.</summary>
public record LinesBatchResultDto(
    IReadOnlyList<LineOpResultDto> Results,
    IReadOnlyList<BaleSummaryDto> Bales,
    ShipmentTotalsDto Totals);

/// <summary>فرش پیداشده کجاست (برای «این فرش در محمولهٔ ۷ / نگلهٔ ۳ است؛ منتقل شود؟»).</summary>
public record FindRugInBaleDto(
    Guid ShipmentId,
    int ShipmentNo,
    Guid BaleId,
    int BaleNo,
    Guid LineId,
    bool SameShipment,
    ShipmentStatus Status);

/// <summary>
/// <c>GET /api/shipments/{id}/find-rug?serial=</c>: فرش با تطبیق دقیق یا «داخل دسته»، و اگر خطی دارد کجا. فرش پیدا نشده
/// → null (۴۰۴).
/// </summary>
public record FindRugResultDto(RugListItemDto Rug, FindRugInBaleDto? InBale, string MatchType);
