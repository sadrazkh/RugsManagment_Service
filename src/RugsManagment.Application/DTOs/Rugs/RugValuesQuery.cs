namespace RugsManagment.Application.DTOs.Rugs;

/// <summary>
/// ستونی که <c>GET /api/rugs/values?field=&amp;q=</c> برایش پیشنهاد می‌دهد (خانه‌های combo جدول و فرم: نقشه، محل بافت،
/// مواد، رنگ‌ها، فروشنده). فقط افزودنی با عدد صریح.
/// </summary>
public enum RugValueColumn
{
    /// <summary>عنوان فرش (<c>rug.title</c>)</summary>
    Title = 0,

    /// <summary>محل بافت (<c>rug.origin</c>)</summary>
    Origin = 1,

    /// <summary>نقشه (<c>rug.pattern</c>)</summary>
    Pattern = 2,

    /// <summary>مواد مصرفی (<c>rug.material</c>)</summary>
    Material = 3,

    /// <summary>یک فیلد سفارشی (<c>rug.custom.{key}</c>)؛ کلید در <see cref="RugValuesQuery.CustomKey"/>.</summary>
    Custom = 4,

    /// <summary>نام فروشندهٔ ورودی‌ها (<c>batch.seller</c>)</summary>
    Seller = 5
}

/// <summary>
/// درخواست پیشنهادهای یک خانهٔ combo: مقدارهای متمایزی که همین کارگاه قبلاً نوشته است.
///
/// قرارداد پیاده‌سازی (<c>IRugRepository.ListDistinctValuesAsync</c>): فقط فرش‌های فعال (سطل زباله نه) و ورودی‌های
/// همین کارگاه؛ مقدار خالی کنار گذاشته می‌شود؛ <see cref="Search"/> مثل جستجوی فهرست یکسان‌سازی می‌شود (ي/ك عربی) و
/// «شامل» با ILIKE است؛ ترتیب: پرتکرارتر اول، بعد خودِ مقدار؛ حداکثر <see cref="Take"/> مقدار. فیلد سفارشی از
/// <c>MetadataJson -&gt;&gt; key</c> با کلید به‌صورت پارامتر خوانده می‌شود، هرگز چسبانده در متن SQL.
/// </summary>
public sealed record RugValuesQuery
{
    public const int DefaultTake = 20;
    public const int MaxTake = 50;

    /// <summary>بیشترین طول کلید فیلد سفارشی.</summary>
    public const int MaxCustomKeyLength = 100;

    /// <summary>پیشوند کلید فیلدهای سفارشی در FieldCatalog.</summary>
    public const string CustomFieldPrefix = "rug.custom.";

    public required RugValueColumn Column { get; init; }

    /// <summary>کلید فیلد سفارشی (<c>CustomFieldDefinition.Key</c>)؛ فقط برای <see cref="RugValueColumn.Custom"/>.</summary>
    public string? CustomKey { get; init; }

    /// <summary>متنی که کاربر تا این‌جا نوشته؛ null یعنی پرتکرارترین‌ها.</summary>
    public string? Search { get; init; }

    public int Take { get; init; } = DefaultTake;

    /// <summary>ورودی را امن می‌کند: تعداد در بازهٔ ۱..<see cref="MaxTake"/>، جستجوی خالی به null.</summary>
    public RugValuesQuery Sanitized() => this with
    {
        Take = Take < 1 ? DefaultTake : Math.Min(Take, MaxTake),
        Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim(),
        CustomKey = Column == RugValueColumn.Custom ? CustomKey?.Trim() : null
    };

    /// <summary>
    /// کلید فیلد FieldCatalog (<c>rug.origin</c>، <c>rug.custom.bg_color</c>، <c>batch.seller</c>…) را به درخواست تبدیل
    /// می‌کند. کلید ناشناخته، یا کلید سفارشی با نویسهٔ غیر حرف/رقم/«_» → false (کنترلر ۴۰۰ می‌دهد). این‌که آن فیلد
    /// سفارشی واقعاً در کارگاه تعریف شده یا نه را این‌جا نمی‌سنجیم؛ کلید ناموجود فقط فهرست خالی می‌دهد.
    /// </summary>
    public static bool TryFromFieldKey(string? fieldKey, string? search, out RugValuesQuery query)
    {
        query = null!;
        var key = fieldKey?.Trim();
        if (string.IsNullOrEmpty(key))
            return false;

        RugValueColumn? column = key switch
        {
            "rug.title" => RugValueColumn.Title,
            "rug.origin" => RugValueColumn.Origin,
            "rug.pattern" => RugValueColumn.Pattern,
            "rug.material" => RugValueColumn.Material,
            "batch.seller" => RugValueColumn.Seller,
            _ => null
        };

        if (column is { } known)
        {
            query = new RugValuesQuery { Column = known, Search = search }.Sanitized();
            return true;
        }

        if (!key.StartsWith(CustomFieldPrefix, StringComparison.Ordinal))
            return false;

        var customKey = key[CustomFieldPrefix.Length..];
        if (!IsValidCustomKey(customKey))
            return false;

        query = new RugValuesQuery { Column = RugValueColumn.Custom, CustomKey = customKey, Search = search }.Sanitized();
        return true;
    }

    /// <summary>
    /// همان شکلی که <c>CustomFieldService</c> برای کلید می‌سازد: حرف (فارسی هم)، رقم و «_»، غیرخالی، نه فقط «_».
    /// </summary>
    public static bool IsValidCustomKey(string? key)
        => !string.IsNullOrEmpty(key)
           && key.Length <= MaxCustomKeyLength
           && key.All(c => char.IsLetterOrDigit(c) || c == '_')
           && key.Any(c => c != '_');
}
