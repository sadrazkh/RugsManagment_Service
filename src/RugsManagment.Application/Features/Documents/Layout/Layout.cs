using System.Text.Json;
using System.Text.Json.Serialization;

namespace RugsManagment.Application.Features.Documents.Layout;

/// <summary>
/// چیدمان یک قالب سند (spec E.2، <c>schemaVersion: 1</c>) — همان JSON که در <c>DocumentTemplate.LayoutJson</c> ذخیره و
/// در طراح (P7) ویرایش می‌شود. قالب پیش‌فرض کامل در <c>Defaults/shipment-declaration.v1.json</c> است (E.9).
///
/// عضوهای نانوشته پیش‌فرض می‌گیرند (<c>format: "general"</c>، <c>visibleWhen: "always"</c>، <c>style: "cell"</c>،
/// <c>headerStyle: "header"</c>)؛ عضو ناشناخته رد می‌شود تا غلط تایپی («fromcol») بی‌صدا نادیده نماند.
/// </summary>
public sealed record DocumentLayout
{
    public int SchemaVersion { get; init; }

    /// <summary><see cref="LayoutKinds"/>.</summary>
    public string Kind { get; init; } = "";

    /// <summary><see cref="LotRowModes"/>: هر تخته یک ردیف با مقدارهای «برای یک تخته» (perPiece) یا جمع خط (perLine).</summary>
    public string LotRow { get; init; } = LotRowModes.PerPiece;

    public PageLayout Page { get; init; } = new();

    public IReadOnlyList<SheetLayout> Sheets { get; init; } = [];

    public LayoutStyles Styles { get; init; } = new();

    /// <summary>برگهٔ اصلی (نقش <c>main</c>)؛ قالب معتبر دقیقاً یکی دارد.</summary>
    [JsonIgnore]
    public SheetLayout? Main => Sheets.FirstOrDefault(s => s.Role == SheetRoles.Main);

    /// <summary>برگهٔ خلاصه (نقش <c>summary</c>)، اگر باشد.</summary>
    [JsonIgnore]
    public SheetLayout? Summary => Sheets.FirstOrDefault(s => s.Role == SheetRoles.Summary);
}

/// <summary>تنظیم صفحه و نما (E.2 <c>page</c>). اندازه‌ها به اینچ‌اند، همان واحد <c>pageMargins</c> اکسل.</summary>
public sealed record PageLayout
{
    public string Paper { get; init; } = "A4";
    public string Orientation { get; init; } = "portrait";
    public bool FitToWidth { get; init; } = true;
    public int Scale { get; init; } = 100;
    public MarginsLayout Margins { get; init; } = new();
    public int Zoom { get; init; } = 100;
    public bool Rtl { get; init; } = true;
    public string Font { get; init; } = "B Nazanin";
    public decimal FontSize { get; init; } = 11m;
    public decimal RowHeight { get; init; } = 16.8m;
    public int PrintTitleRows { get; init; } = 2;
    public bool KeepGroupTogether { get; init; } = true;
    public string? DraftHeader { get; init; }
    public string SummaryPlacement { get; init; } = "sheet";
}

/// <summary>حاشیه‌های صفحه به اینچ.</summary>
public sealed record MarginsLayout
{
    public decimal Left { get; init; } = 0.2m;
    public decimal Right { get; init; } = 0.2m;
    public decimal Top { get; init; } = 0.5m;
    public decimal Bottom { get; init; } = 0.5m;
    public decimal Header { get; init; } = 0.3m;
    public decimal Footer { get; init; } = 0.3m;
}

/// <summary>
/// یک برگه. برگهٔ اصلی ستون‌های کلیددار، گروه (نگله) و پانویس دارد؛ برگهٔ خلاصه (<c>role: summary</c>) ستون‌هایی که به
/// پانویس‌ها وصل‌اند (<c>from: "footer:&lt;id&gt;"</c>) و ردیف «=» و جمع.
/// </summary>
public sealed record SheetLayout
{
    public string Name { get; init; } = "";

    /// <summary><see cref="SheetRoles"/>.</summary>
    public string Role { get; init; } = SheetRoles.Main;

    /// <summary>سبکِ ستون‌ها (<c>&lt;col style&gt;</c>): خانه‌های خالی برگه هم همین قلم را دارند.</summary>
    public string? ColumnStyle { get; init; }

    public TitleLayout? Title { get; init; }

    public IReadOnlyList<ColumnLayout> Columns { get; init; } = [];

    public GroupLayout? Group { get; init; }

    public GrandTotalLayout? GrandTotal { get; init; }

    public IReadOnlyList<CheckAgainstLayout> CheckAgainst { get; init; } = [];

    // ── فقط برگهٔ خلاصه ──
    /// <summary>حرف ستون اولِ جدول خلاصه (برگهٔ خلاصه جابه‌جایی ستون ندارد، پس حرف ثابت است).</summary>
    public string? StartCol { get; init; }

    public string? HeaderStyle { get; init; }

    public string? RowStyle { get; init; }

    /// <summary>ردیف «=» زیر ردیف‌های خلاصه (همان نمونهٔ بازرگان).</summary>
    public bool EqualsRow { get; init; }

    public SummaryTotalsLayout? Totals { get; init; }
}

/// <summary>
/// ردیف عنوان: ادغام از <see cref="FromCol"/> تا <see cref="ToCol"/>. در برگهٔ اصلی این دو کلید ستون‌اند (یا
/// <c>last</c>) تا با آمدن ستون «تعداد» عنوان هم پهن‌تر شود (A1:L1 → A1:M1)؛ در برگهٔ خلاصه حرف ثابت‌اند (C تا H).
/// </summary>
public sealed record TitleLayout
{
    public string FromCol { get; init; } = "";
    public string ToCol { get; init; } = "";
    public string Expr { get; init; } = "";
    public string? Style { get; init; }
}

/// <summary>یک ستون (E.2 «Column»): یا فیلد کاتالوگ یا عبارت؛ پهنا به واحد نویسهٔ اکسل، عیناً نوشته می‌شود.</summary>
public sealed record ColumnLayout
{
    public string? Key { get; init; }
    public string Label { get; init; } = "";
    public decimal? Width { get; init; }
    public string? Field { get; init; }
    public string? Expr { get; init; }
    public string? Format { get; init; }

    /// <summary><see cref="Visibility"/>.</summary>
    public string? VisibleWhen { get; init; }

    public string? Style { get; init; }
    public string? HeaderStyle { get; init; }

    /// <summary>فقط برگهٔ خلاصه: <c>footer:&lt;id&gt;</c>.</summary>
    public string? From { get; init; }

    [JsonIgnore]
    public string EffectiveFormat => string.IsNullOrEmpty(Format) ? LayoutDefaults.Format : Format;

    [JsonIgnore]
    public string EffectiveVisibleWhen => string.IsNullOrEmpty(VisibleWhen) ? Visibility.Always : VisibleWhen;

    [JsonIgnore]
    public string EffectiveStyle => string.IsNullOrEmpty(Style) ? LayoutDefaults.CellStyle : Style;

    [JsonIgnore]
    public string EffectiveHeaderStyle => string.IsNullOrEmpty(HeaderStyle) ? LayoutDefaults.HeaderStyle : HeaderStyle;
}

/// <summary>گروه‌بندی ردیف‌ها (فعلاً فقط <c>by: "bale"</c>) با پانویس هر گروه.</summary>
public sealed record GroupLayout
{
    public string By { get; init; } = "bale";

    /// <summary>ستون کلید (شمارهٔ نگله) در ردیف‌های گروه عمودی ادغام شود (فقط وقتی گروه ≥ ۲ ردیف دارد).</summary>
    public bool MergeKey { get; init; } = true;

    public string KeyColumn { get; init; } = "";

    /// <summary>نگلهٔ خالی کنار گذاشته شود (<c>W_EMPTY_BALE</c>)؛ وگرنه بازه‌های وارونه مثل <c>SUM(J8:J7)</c> درست می‌شد.</summary>
    public bool SkipEmpty { get; init; } = true;

    public string? FooterStyle { get; init; }

    public IReadOnlyList<FooterCellLayout> Footer { get; init; } = [];
}

/// <summary>
/// یک خانهٔ پانویس (E.2 «Footer cell»). <see cref="Col"/> همیشه کلید ستون است، هرگز حرف، تا با آمدن ستون «تعداد»
/// خانه‌ها خودشان جابه‌جا شوند (I:J → J:K). <see cref="Span"/> تعداد ستونِ دیده‌شده‌ای است که خانه می‌پوشاند.
/// </summary>
public sealed record FooterCellLayout
{
    public string Id { get; init; } = "";
    public string Col { get; init; } = "";
    public int Span { get; init; } = 1;
    public string? Text { get; init; }
    public string? Expr { get; init; }

    /// <summary><see cref="AggKinds"/>.</summary>
    public string? Agg { get; init; }

    public string? Of { get; init; }
    public string? Style { get; init; }
}

/// <summary>ردیف جمع کل پایین جدول (برای فهرست فرش؛ اظهار ندارد).</summary>
public sealed record GrandTotalLayout
{
    public string? Label { get; init; }
    public IReadOnlyList<FooterCellLayout> Cells { get; init; } = [];
    public string? Style { get; init; }
}

/// <summary>سنجش یک خانهٔ پانویس با جمع دفتر هر گروه (<c>W_CHECK_MISMATCH</c>).</summary>
public sealed record CheckAgainstLayout
{
    public string FooterId { get; init; } = "";
    public string LedgerField { get; init; } = "";
}

/// <summary>ردیف جمع برگهٔ خلاصه: کدام ستون‌ها جمع می‌خورند و کدام خالیِ کادردار می‌مانند.</summary>
public sealed record SummaryTotalsLayout
{
    public IReadOnlyList<string> Sum { get; init; } = [];
    public IReadOnlyList<string> BlankBordered { get; init; } = [];
}

/// <summary>یک سبک نام‌دار (E.2 <c>styles</c>).</summary>
public sealed record StyleLayout
{
    public string Font { get; init; } = "B Nazanin";
    public decimal Size { get; init; } = 11m;

    /// <summary>رنگ قلم به شکل AARRGGBB (<c>FF767171</c>).</summary>
    public string? ColorRgb { get; init; }

    /// <summary>رنگ زمینهٔ یکدست به شکل AARRGGBB (<c>FFFFFF00</c>).</summary>
    public string? FillRgb { get; init; }

    /// <summary><see cref="BorderStyles"/>.</summary>
    public string Border { get; init; } = BorderStyles.None;

    /// <summary>left | center | right | general.</summary>
    public string HAlign { get; init; } = "center";

    /// <summary>top | center | bottom.</summary>
    public string VAlign { get; init; } = "center";

    public bool? ShrinkToFit { get; init; }
}

/// <summary>
/// فهرست سبک‌ها به ترتیب نوشته‌شده، با تکراری‌ها (برای قاعدهٔ «نام سبک یکتا»؛ دیکشنری معمولی تکراری را بی‌صدا
/// می‌بلعید). در JSON یک شیء است: <c>{ "title": { … }, … }</c>.
/// </summary>
[JsonConverter(typeof(LayoutStylesConverter))]
public sealed class LayoutStyles
{
    private readonly List<KeyValuePair<string, StyleLayout>> _entries;

    public LayoutStyles() => _entries = [];

    public LayoutStyles(IEnumerable<KeyValuePair<string, StyleLayout>> entries) => _entries = entries.ToList();

    /// <summary>همهٔ سبک‌ها به ترتیب، با تکراری‌ها.</summary>
    public IReadOnlyList<KeyValuePair<string, StyleLayout>> Entries => _entries;

    public int Count => _entries.Count;

    /// <summary>اولین سبک با این نام.</summary>
    public StyleLayout? Find(string? name)
        => name is null ? null : _entries.FirstOrDefault(e => string.Equals(e.Key, name, StringComparison.Ordinal)).Value;

    public bool Contains(string? name) => Find(name) is not null;
}

internal sealed class LayoutStylesConverter : JsonConverter<LayoutStyles>
{
    public override LayoutStyles Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("styles must be an object.");

        var entries = new List<KeyValuePair<string, StyleLayout>>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return new LayoutStyles(entries);
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("styles must be an object.");
            var name = reader.GetString() ?? "";
            reader.Read();
            var style = JsonSerializer.Deserialize<StyleLayout>(ref reader, options)
                        ?? throw new JsonException($"style {name} is null.");
            entries.Add(new KeyValuePair<string, StyleLayout>(name, style));
        }

        throw new JsonException("styles is not closed.");
    }

    public override void Write(Utf8JsonWriter writer, LayoutStyles value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (name, style) in value.Entries)
        {
            writer.WritePropertyName(name);
            JsonSerializer.Serialize(writer, style, options);
        }

        writer.WriteEndObject();
    }
}

/// <summary>نوع سند (<c>DocumentKind</c>، B.14) به همان نام‌ها.</summary>
public static class LayoutKinds
{
    public const string ShipmentDeclaration = "ShipmentDeclaration";
    public const string ShipmentCost = "ShipmentCost";
    public const string RugList = "RugList";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { ShipmentDeclaration, ShipmentCost, RugList };
}

/// <summary>شکل ردیف دسته.</summary>
public static class LotRowModes
{
    /// <summary>هر خط یک ردیف با مقدارهای «یک تخته»؛ پانویس با تعداد وزن می‌خورد (SUMPRODUCT).</summary>
    public const string PerPiece = "perPiece";

    /// <summary>هر خط یک ردیف با جمع خط (عبارت خودش در تعداد ضرب می‌کند)؛ پانویس جمع ساده است.</summary>
    public const string PerLine = "perLine";
}

/// <summary>نقش برگه.</summary>
public static class SheetRoles
{
    public const string Main = "main";
    public const string Summary = "summary";
}

/// <summary>دیده شدن ستون.</summary>
public static class Visibility
{
    public const string Always = "always";

    /// <summary>فقط وقتی سند دست‌کم یک دسته (تعداد ≠ ۱) دارد.</summary>
    public const string HasLots = "hasLots";

    public const string Never = "never";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Always, HasLots, Never };
}

/// <summary>کادر سبک.</summary>
public static class BorderStyles
{
    public const string None = "none";

    /// <summary>کادر نازک دور هر خانه.</summary>
    public const string Thin = "thin";

    /// <summary>کادر نازک فقط دور بیرونِ خانهٔ ادغام‌شده.</summary>
    public const string Outer = "outer";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { None, Thin, Outer };
}

/// <summary>جمع پانویس.</summary>
public static class AggKinds
{
    /// <summary>تعداد تخته: <c>SUM(qty)</c> وقتی ستون تعداد دیده می‌شود، وگرنه <c>ROWS()</c>.</summary>
    public const string Count = "count";

    /// <summary>جمع ستون؛ در perPiece با ستون تعداد دیده‌شده وزن‌دار (<c>SUMPRODUCT</c>).</summary>
    public const string Sum = "sum";
}

/// <summary>پیش‌فرض‌های عضوهای نانوشته (E.9).</summary>
public static class LayoutDefaults
{
    public const string Format = "general";
    public const string CellStyle = "cell";
    public const string HeaderStyle = "header";

    /// <summary>بیشترین تعداد ستون یک قالب.</summary>
    public const int MaxColumns = 30;

    /// <summary><c>toCol: "last"</c> یعنی آخرین ستون دیده‌شده.</summary>
    public const string LastColumn = "last";

    /// <summary>پیشوند پیوند ستون خلاصه به پانویس.</summary>
    public const string FooterLinkPrefix = "footer:";
}

/// <summary>یک خطای قالب با کد، مسیر JSON و متن فارسی (spec E.2: «each with a Persian message and a JSON path»).</summary>
/// <param name="Code">کد ثابت (<c>E_LAYOUT_*</c> یا <c>E_EXPR_*</c>).</param>
/// <param name="Path">مسیر JSON: <c>$.sheets[0].columns[10].expr</c>.</param>
/// <param name="Message">متن فارسی پرشده.</param>
public sealed record LayoutIssue(string Code, string Path, string Message);

/// <summary>خواندن و نوشتن JSON قالب، و قالب‌های پیش‌فرض جاسازی‌شده.</summary>
public static class LayoutJson
{
    /// <summary>گزینه‌های مشترک: camelCase، عضو ناشناخته خطا، توضیح مجاز.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>قالب از متن JSON، یا خطای خواندن با مسیر.</summary>
    public static (DocumentLayout? Layout, LayoutIssue? Error) Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return (null, LayoutMessages.Issue(LayoutCodes.Json, "$"));
        try
        {
            var layout = JsonSerializer.Deserialize<DocumentLayout>(json, Options);
            return layout is null ? (null, LayoutMessages.Issue(LayoutCodes.Json, "$")) : (layout, null);
        }
        catch (JsonException e)
        {
            return (null, LayoutMessages.Issue(LayoutCodes.Json, e.Path ?? "$"));
        }
    }

    public static string Serialize(DocumentLayout layout) => JsonSerializer.Serialize(layout, Options);

    /// <summary>
    /// قالب‌های پیش‌فرض جاسازی‌شده (<c>Features/Documents/Defaults/*.v*.json</c>): کلید (<c>shipment-declaration</c>)،
    /// نسخه و متن. <c>DocumentTemplateSeedContributor</c> همه را می‌کارد، پس P7 فقط فایل اضافه می‌کند.
    /// </summary>
    public static IReadOnlyList<DefaultTemplate> Defaults()
    {
        var assembly = typeof(LayoutJson).Assembly;
        var result = new List<DefaultTemplate>();
        foreach (var name in assembly.GetManifestResourceNames().Order(StringComparer.Ordinal))
        {
            const string marker = ".Features.Documents.Defaults.";
            var at = name.IndexOf(marker, StringComparison.Ordinal);
            if (at < 0 || !name.EndsWith(".json", StringComparison.Ordinal))
                continue;
            var file = name[(at + marker.Length)..^".json".Length]; // shipment-declaration.v1
            var dot = file.LastIndexOf(".v", StringComparison.Ordinal);
            if (dot < 0 || !int.TryParse(file[(dot + 2)..], out var version))
                continue;
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            result.Add(new DefaultTemplate(file[..dot], version, reader.ReadToEnd()));
        }

        return result;
    }

    /// <summary>یک قالب پیش‌فرض با کلید و نسخه (بالاترین نسخه وقتی <paramref name="version"/> نیامده).</summary>
    public static DefaultTemplate Default(string seedKey, int? version = null)
        => Defaults().Where(d => d.SeedKey == seedKey && (version is null || d.Version == version))
               .OrderByDescending(d => d.Version)
               .FirstOrDefault()
           ?? throw new KeyNotFoundException($"Default document template {seedKey} v{version} is not embedded.");
}

/// <summary>یک قالب پیش‌فرض جاسازی‌شده.</summary>
/// <param name="SeedKey">کلید پایدار (<c>shipment-declaration</c>، <c>rug-list</c>).</param>
/// <param name="Version">نسخه از نام فایل (<c>.v1</c>).</param>
/// <param name="Json">متن JSON عیناً.</param>
public sealed record DefaultTemplate(string SeedKey, int Version, string Json)
{
    public DocumentLayout Parse()
        => LayoutJson.Parse(Json).Layout ?? throw new InvalidOperationException($"Embedded template {SeedKey}.v{Version} is not valid JSON.");
}

/// <summary>ستون‌های دیده‌شدهٔ برگهٔ اصلی برای یک حالت (با دسته یا بی دسته)، با حرف اکسل هر کدام.</summary>
public static class LayoutColumns
{
    /// <summary>ستون دیده می‌شود؟</summary>
    public static bool IsVisible(ColumnLayout column, bool hasLots) => column.EffectiveVisibleWhen switch
    {
        Visibility.Always => true,
        Visibility.HasLots => hasLots,
        _ => false
    };

    /// <summary>ستون‌های دیده‌شده به ترتیب.</summary>
    public static IReadOnlyList<ColumnLayout> Visible(SheetLayout sheet, bool hasLots)
        => sheet.Columns.Where(c => IsVisible(c, hasLots)).ToList();

    /// <summary>حرف ستون اکسل از شمارهٔ ۱-پایه (۱ → A، ۲۸ → AB).</summary>
    public static string Letter(int index)
    {
        if (index < 1)
            throw new ArgumentOutOfRangeException(nameof(index));
        var s = "";
        while (index > 0)
        {
            var r = (index - 1) % 26;
            s = (char)('A' + r) + s;
            index = (index - 1) / 26;
        }

        return s;
    }

    /// <summary>شمارهٔ ۱-پایهٔ ستون از حرف (A → ۱)؛ ۰ اگر حرف نیست.</summary>
    public static int Index(string? letters)
    {
        if (string.IsNullOrEmpty(letters) || letters.Length > 3 || !letters.All(c => c is >= 'A' and <= 'Z'))
            return 0;
        var n = 0;
        foreach (var c in letters)
            n = n * 26 + (c - 'A' + 1);
        return n <= 16384 ? n : 0;
    }
}
