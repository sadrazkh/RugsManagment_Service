using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Grid;
using RugsManagment.Application.Fields;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Intake.Grid;

/// <summary>
/// قاعدهٔ حذف و برگرداندن فرش از جدول (G.3، G.6، E6): Business هر فرشی را که فروخته نشده؛ بقیه (انباردار) فقط فرشی که
/// خودش در ۲۴ ساعت گذشته ساخته و هیچ مبلغی رویش نیست (سطر خرید، سهم هزینه، فروش، هزینهٔ کارهای کارگاه). فرش داخل نگله را
/// پاس سازگاری (قفل محموله) جدا رد می‌کند.
/// </summary>
public static class RugDeletePolicy
{
    /// <summary>پنجرهٔ «فرشی که خودش تازه ساخته».</summary>
    public static readonly TimeSpan FloorWindow = TimeSpan.FromHours(24);

    public static bool MayDelete(RugDeleteFacts facts, bool business, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.Sold) return false;
        if (business) return true;
        return facts.CreatedByUser && !facts.HasMoney && now - facts.CreatedAt <= FloorWindow;
    }
}

/// <summary>
/// ساختن ردیف جدول از یک فرش (<see cref="GridRugSnapshot"/>): مقدار هر خانهٔ نوشتنی به همان شکلی که جزیره می‌خواند و
/// پس می‌فرستد، و مقدارهای حساب‌شده. GET، پاسخ ذخیره و «مقدار فعلی سرور» در ادغام سه‌طرفه همه از همین‌جا می‌آیند، پس
/// یک خانه هرگز در دو جا دو شکل ندارد (L.2: Derived همان ستون‌های ذخیره‌شده است).
///
/// <para>شکل خانه‌ها: شماره متن نمایشی («3911 - 3960»)، اندازه‌ها عدد، فیلد سفارشی متن ذخیره‌شده، و خانهٔ خرید
/// (فقط Business) یکی از <c>null</c>، <c>{rate, basis, amount}</c> (یک سطر از آن نوع) یا
/// <c>{rate: null, basis, amount, lines}</c> (دو سطر یا بیشتر؛ فقط‌خواندنی با چیپ «چند سطر»، G.2).</para>
/// </summary>
public sealed class RugGridRowBuilder(GridViewer viewer, RugGridCatalog catalog, IReadOnlyCollection<string> valueKeys)
{
    /// <summary>کلیدهای حساب‌شده که از کلاینت پذیرفته نمی‌شوند (در ورودی کنار می‌روند).</summary>
    public static readonly IReadOnlySet<string> DerivedKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        RugFieldKeys.AreaM2, RugFieldKeys.TotalAreaM2, RugFieldKeys.IntakeAreaM2, RugFieldKeys.PurchaseTotal, RugFieldKeys.Sku,
        GridKeys.CanDelete, GridKeys.BatchId, GridKeys.BatchNumber, GridKeys.SerialFrom, GridKeys.SerialTo, GridKeys.CreatedAt
    };

    public GridViewer Viewer => viewer;

    public RugGridCatalog Catalog => catalog;

    /// <summary>ردیف کامل.</summary>
    public GridRowDto Row(GridRugSnapshot rug, bool canDelete)
        => new(rug.Id, rug.Version, rug.Sku, Values(rug), Derived(rug, canDelete));

    /// <summary>مقدار خانه‌های نوشتنی (همهٔ کلیدهای جدول، حتی ستون‌های پنهان). پول برای غیر Business هرگز.</summary>
    public Dictionary<string, JsonElement?> Values(GridRugSnapshot rug)
    {
        ArgumentNullException.ThrowIfNull(rug);
        var values = new Dictionary<string, JsonElement?>(StringComparer.Ordinal);
        JsonObject? metadata = null;

        foreach (var key in valueKeys)
        {
            switch (key)
            {
                case RugFieldKeys.Serial: values[key] = Json(rug.SerialNo); break;
                case GridKeys.Seller: values[key] = Json(rug.SellerName); break;
                case RugFieldKeys.Quantity: values[key] = Json(rug.Quantity); break;
                case RugFieldKeys.Title: values[key] = Json(rug.Title); break;
                case RugFieldKeys.Origin: values[key] = Json(rug.Origin); break;
                case RugFieldKeys.Pattern: values[key] = Json(rug.Pattern); break;
                case RugFieldKeys.Material: values[key] = Json(rug.Material); break;
                case RugFieldKeys.Notes: values[key] = Json(rug.Notes); break;
                case RugFieldKeys.KnotDensity: values[key] = Json(rug.KnotDensity); break;
                case RugFieldKeys.IntakeLength: values[key] = Json(Norm(rug.IntakeLength)); break;
                case RugFieldKeys.IntakeWidth: values[key] = Json(Norm(rug.IntakeWidth)); break;
                case RugFieldKeys.FinalLength: values[key] = Json(Norm(rug.FinalLength)); break;
                case RugFieldKeys.FinalWidth: values[key] = Json(Norm(rug.FinalWidth)); break;
                default:
                    if (key.StartsWith(RugFieldKeys.CustomPrefix, StringComparison.Ordinal))
                    {
                        metadata ??= ParseMetadata(rug.MetadataJson);
                        var customKey = key[RugFieldKeys.CustomPrefix.Length..];
                        values[key] = Json(metadata.TryGetPropertyValue(customKey, out var node) ? NodeText(node) : null);
                    }
                    else if (key.StartsWith(RugFieldKeys.PurchasePrefix, StringComparison.Ordinal))
                    {
                        if (viewer.CanSeeMoney && catalog.PurchaseCategoryOf(key) is { } category)
                            values[key] = PurchaseCell(rug.PurchaseLines.Where(l => l.CategoryId == category.Id).ToList());
                    }
                    break;
            }
        }

        return values;
    }

    /// <summary>
    /// مقدارهای حساب‌شده: متراژها، جمع خرید (Business)، دو سر شماره (برای «شمارهٔ بعدی» محلی جدول)، ورودی، و اینکه
    /// «حذف این فرش» به این بیننده نشان داده شود.
    /// </summary>
    public Dictionary<string, JsonElement?> Derived(GridRugSnapshot rug, bool canDelete)
    {
        ArgumentNullException.ThrowIfNull(rug);
        var derived = new Dictionary<string, JsonElement?>(StringComparer.Ordinal)
        {
            [RugFieldKeys.AreaM2] = Json(Norm(rug.AreaM2)),
            [RugFieldKeys.TotalAreaM2] = Json(Norm(rug.TotalAreaM2)),
            [RugFieldKeys.IntakeAreaM2] = Json(Norm(rug.IntakeAreaM2)),
            [GridKeys.SerialFrom] = Json(rug.SerialFrom),
            [GridKeys.SerialTo] = Json(rug.SerialTo),
            [GridKeys.BatchId] = Json(rug.BatchId),
            [GridKeys.BatchNumber] = Json(rug.BatchNumber),
            [GridKeys.CreatedAt] = Json(rug.CreatedAt),
            [GridKeys.CanDelete] = Json(canDelete)
        };

        if (viewer.CanSeeMoney)
            derived[RugFieldKeys.PurchaseTotal] = Json(PurchaseTotal(rug));

        return derived;
    }

    /// <summary>جمع مبلغ سطرهای خرید خودِ فرش (نه سهم خرید یکجا)؛ null وقتی سطری ندارد.</summary>
    public static decimal? PurchaseTotal(GridRugSnapshot rug)
        => rug.PurchaseLines.Count == 0 ? null : Norm(rug.PurchaseLines.Sum(l => l.Amount));

    /// <summary>
    /// جمع‌های پانویس: Σ تعداد به تخته، Σ متراژ کل، Σ خرید (Business)؛ با <paramref name="groupByBatch"/> یک جمع برای هر
    /// ورودی (فروشنده) به ترتیب اولین ردیفش.
    /// </summary>
    public GridTotalsDto Totals(IReadOnlyList<GridRugSnapshot> rows, bool groupByBatch, int? totalRows = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        decimal? Purchase(IEnumerable<GridRugSnapshot> set)
            => viewer.CanSeeMoney ? Norm(set.Sum(r => r.PurchaseLines.Sum(l => l.Amount))) : null;

        IReadOnlyList<GridGroupTotalsDto>? groups = null;
        if (groupByBatch)
        {
            groups = rows
                .GroupBy(r => r.BatchId)
                .Select(g => new GridGroupTotalsDto(
                    g.Key, g.First().BatchNumber, g.First().SellerName, g.Count(), g.Sum(r => r.Quantity),
                    Norm(g.Sum(r => r.TotalAreaM2)), Purchase(g)))
                .ToList();
        }

        return new GridTotalsDto(
            totalRows ?? rows.Count, rows.Sum(r => r.Quantity), Norm(rows.Sum(r => r.TotalAreaM2)), Purchase(rows), groups);
    }

    // ═════════════════════════════════════════════════════════════════════
    // خانهٔ خرید
    // ═════════════════════════════════════════════════════════════════════

    private static JsonElement? PurchaseCell(IReadOnlyList<GridPurchaseLine> lines) => lines.Count switch
    {
        0 => null,
        1 => JsonSerializer.SerializeToElement(new PurchaseCellValue(Norm(lines[0].Rate), (int)lines[0].Basis, Norm(lines[0].Amount), null)),
        _ => JsonSerializer.SerializeToElement(
            new PurchaseCellValue(null, (int)lines[0].Basis, Norm(lines.Sum(l => l.Amount)), lines.Count))
    };

    /// <summary>
    /// شکل JSON خانهٔ خرید؛ نام‌ها صریح‌اند چون <c>SerializeToElement</c> پیش‌فرض نام عضو را camelCase نمی‌کند، و
    /// <c>lines</c> فقط برای خانهٔ چندسطری می‌آید.
    /// </summary>
    private sealed record PurchaseCellValue(
        [property: JsonPropertyName("rate")] decimal? Rate,
        [property: JsonPropertyName("basis")] int Basis,
        [property: JsonPropertyName("amount")] decimal? Amount,
        [property: JsonPropertyName("lines"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Lines);

    // ═════════════════════════════════════════════════════════════════════
    // ابزار
    // ═════════════════════════════════════════════════════════════════════

    private static JsonElement? Json<T>(T? value) => value is null ? null : JsonSerializer.SerializeToElement(value);

    /// <summary>صفرهای بی‌اثر اعشار را برمی‌دارد (۹۰۰۰۰۰۰٫۰۰۰۰ ← ۹۰۰۰۰۰۰، ۸٫۰۰ ← ۸) تا JSON عدد تمیز داشته باشد.</summary>
    public static decimal Norm(decimal value) => value / 1.0000000000000000000000000000m;

    /// <inheritdoc cref="Norm(decimal)"/>
    public static decimal? Norm(decimal? value) => value is { } v ? Norm(v) : null;

    private static JsonObject ParseMetadata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonNode.Parse(json) as JsonObject ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? NodeText(JsonNode? node) => node switch
    {
        null => null,
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        _ => node.ToJsonString()
    };
}

/// <summary>
/// مقایسهٔ «همان مقدار» برای ادغام سه‌طرفه (G.6): دو مقدار یک خانه با شکل‌های نوشتاری متفاوت ولی معنای یکسان برابرند —
/// «3911-3960» و «3911 - 3960»، ۲٫۰۸ و "2.08"، «حاج‌رضا» و «حاج رضا» (کلید فروشنده)، و خانهٔ خرید با نرخ برابر (مبنا
/// اگر هر دو طرف دارند باید برابر باشد). خالی، رشتهٔ خالی و null یکی‌اند.
/// </summary>
public static class GridValueComparer
{
    private static readonly HashSet<string> NumericKeys = new(StringComparer.Ordinal)
    {
        RugFieldKeys.Quantity, RugFieldKeys.KnotDensity, RugFieldKeys.IntakeLength, RugFieldKeys.IntakeWidth,
        RugFieldKeys.FinalLength, RugFieldKeys.FinalWidth
    };

    public static bool Same(string key, JsonElement? a, JsonElement? b)
    {
        var blankA = IsBlank(a);
        var blankB = IsBlank(b);
        if (blankA || blankB) return blankA && blankB;

        if (key.StartsWith(RugFieldKeys.PurchasePrefix, StringComparison.Ordinal))
            return SamePurchase(a!.Value, b!.Value);

        if (NumericKeys.Contains(key))
        {
            var dim = key != RugFieldKeys.Quantity && key != RugFieldKeys.KnotDensity;
            return TryNumber(a, dim, out var x) && TryNumber(b, dim, out var y) ? x == y : Text(a) == Text(b);
        }

        var textA = Text(a);
        var textB = Text(b);
        if (key == RugFieldKeys.Serial
            && SerialNumber.TryParse(textA, out var serialA, out _) && SerialNumber.TryParse(textB, out var serialB, out _))
            return string.Equals(serialA?.Display, serialB?.Display, StringComparison.Ordinal);

        if (key == GridKeys.Seller)
            return RugBatchService.SellerKeyOf(textA) == RugBatchService.SellerKeyOf(textB);

        return string.Equals(PersianText.Normalize(textA), PersianText.Normalize(textB), StringComparison.Ordinal);
    }

    /// <summary>null، undefined، رشتهٔ خالی/فاصله.</summary>
    public static bool IsBlank(JsonElement? value)
        => value is null
           || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
           || (value.Value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.Value.GetString()));

    /// <summary>
    /// خانهٔ خرید به (نرخ، مبنا، چندسطری): عدد ← نرخ بی مبنا؛ شیء ← <c>rate</c>، <c>basis</c> (عدد یا نام) و <c>lines</c>.
    /// </summary>
    public static (decimal? Rate, CostBasis? Basis, bool Multi) PurchaseParts(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
            return (TryNumber(value, dimension: false, out var n) ? n : null, null, false);

        decimal? rate = value.TryGetProperty("rate", out var r) && TryNumber(r, dimension: false, out var parsed) ? parsed : null;
        CostBasis? basis = null;
        if (value.TryGetProperty("basis", out var b))
        {
            if (b.ValueKind == JsonValueKind.Number && b.TryGetInt32(out var number) && Enum.IsDefined((CostBasis)number))
                basis = (CostBasis)number;
            else if (b.ValueKind == JsonValueKind.String && Enum.TryParse<CostBasis>(b.GetString(), true, out var named)
                     && Enum.IsDefined(named))
                basis = named;
        }

        var multi = value.TryGetProperty("lines", out var lines) && lines.ValueKind == JsonValueKind.Number
                    && lines.TryGetInt32(out var count) && count >= 2;
        return (rate, basis, multi);
    }

    private static bool SamePurchase(JsonElement a, JsonElement b)
    {
        var x = PurchaseParts(a);
        var y = PurchaseParts(b);
        if (x.Multi || y.Multi) return x.Multi && y.Multi;
        if (x.Rate != y.Rate) return false;
        return x.Basis is null || y.Basis is null || x.Basis == y.Basis;
    }

    private static string? Text(JsonElement? value)
    {
        if (IsBlank(value)) return null;
        return value!.Value.ValueKind switch
        {
            JsonValueKind.String => value.Value.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.Value.GetRawText(),
            _ => value.Value.GetRawText()
        };
    }

    /// <summary>عدد از JSON: عدد، یا متن با ارقام فارسی/عربی؛ «٫» و «/» ممیز؛ «,» در اندازه ممیز و در بقیه جداکنندهٔ هزارگان.</summary>
    private static bool TryNumber(JsonElement? value, bool dimension, out decimal number)
    {
        number = 0;
        if (value is not { } element) return false;
        if (element.ValueKind == JsonValueKind.Number) return element.TryGetDecimal(out number);
        if (element.ValueKind != JsonValueKind.String) return false;

        var builder = new StringBuilder();
        foreach (var c in PersianText.Normalize(element.GetString()))
        {
            switch (c)
            {
                case ' ' or '٬':
                    continue;
                case ',':
                    if (dimension) builder.Append('.');
                    continue;
                case '٫' or '/':
                    builder.Append('.');
                    continue;
                default:
                    builder.Append(c);
                    continue;
            }
        }

        return decimal.TryParse(builder.ToString(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out number);
    }
}
