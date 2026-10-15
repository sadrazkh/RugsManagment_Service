using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Shipments;

/// <summary>ورودی عکس «قطعی»: سربرگ، ترجیحات (واژه‌ها و صادرکننده)، نگله‌ها، ردیف‌ها و فیلدهای سفارشی خروجی‌پذیر.</summary>
public sealed record SnapshotInput(
    ShipmentHeaderData Header,
    TenantPreferences Preferences,
    string KeyField,
    IReadOnlyList<BaleData> Bales,
    IReadOnlyList<ShipmentRowData> Rows,
    IReadOnlyList<CustomFieldInfo> CustomFields);

/// <summary>
/// عکس متعارف یک محمولهٔ قطعی (spec C.7) و هش آن.
///
/// <para><b>چرا «متعارف»:</b> هش باید برای همان داده همیشه همان باشد — هم بعد از ذخیره در ستون متن (پس هش دوبارهٔ مقدار
/// ذخیره‌شده همان است)، هم در «قطعی» دوباره بی تغییر. پس: کلیدهای هر شیء به ترتیب ordinal، عددها invariant با تعداد رقم
/// ثابت (ابعاد، متراژ و دلار ۲ رقم، نرخ دلار ۶ رقم)، متن NFC، آرایه‌ها به ترتیب ثابت (نگله با شماره، ردیف با Seq و بعد
/// شناسهٔ فرش)، و هیچ چیزی که با «قطعی» دوباره عوض می‌شود (شمار قطعی، زمان، کاربر، یادداشت). هیچ مبلغ تومانی در آن نیست.</para>
/// </summary>
public static class SnapshotBuilder
{
    /// <summary>نسخهٔ شکل عکس؛ تغییر شکل یعنی نسخهٔ تازه، نه بازنویسی بی‌صدا.</summary>
    public const int SchemaVersion = 1;

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = false
    };

    /// <summary>متن JSON متعارف عکس.</summary>
    public static string Build(SnapshotInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var header = input.Header;
        var preferences = input.Preferences;
        var terms = BaleTerms.For(preferences.BaleTerm);
        var customKeys = input.CustomFields
            .Where(f => f.Exportable)
            .OrderBy(f => f.Key, StringComparer.Ordinal)
            .ToList();
        var rowsByBale = input.Rows.ToLookup(r => r.BaleId);

        var root = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schemaVersion"] = SchemaVersion,
            ["keyField"] = input.KeyField,
            ["shipment"] = new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["number"] = header.Number,
                ["title"] = header.Title,
                ["documentDate"] = header.DocumentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["destination"] = header.Destination,
                ["buyerName"] = header.BuyerName,
                ["declarationNo"] = header.DeclarationNo,
                ["customsOffice"] = header.CustomsOffice,
                ["usdRate"] = Fixed.Of(header.UsdRate, 6)
            },
            ["exporter"] = new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = preferences.ExporterName,
                ["code"] = preferences.ExporterCode,
                ["address"] = preferences.ExporterAddress,
                ["phone"] = preferences.ExporterPhone,
                ["customsOffice"] = preferences.CustomsOffice
            },
            ["terms"] = new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["bale"] = terms.Bale,
                ["bales"] = terms.Bales,
                ["baleOf"] = terms.BaleOf,
                ["baleFooter"] = terms.BaleFooter
            },
            ["customFields"] = customKeys
                .Select(f => (object?)new SortedDictionary<string, object?>(StringComparer.Ordinal) { ["key"] = f.Key, ["label"] = f.Label })
                .ToList(),
            ["bales"] = input.Bales
                .OrderBy(b => b.Number)
                .Select(b => (object?)Bale(b, rowsByBale[b.Id], customKeys))
                .ToList(),
            ["totals"] = new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["bales"] = input.Bales.Count,
                ["lines"] = input.Rows.Count,
                ["pieces"] = input.Rows.Sum(r => r.Quantity),
                ["areaM2"] = Fixed.Of(input.Rows.Sum(LineArea), 2),
                ["usdTotal"] = Fixed.Of(input.Rows.Sum(r => r.HasValue ? r.LineUsd ?? 0m : 0m), 2)
            }
        };

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
            Write(writer, root);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>SHA-256 بایت‌های UTF-8 متن (hex کوچک، ۶۴ نویسه).</summary>
    public static string Hash(string snapshotJson)
    {
        ArgumentNullException.ThrowIfNull(snapshotJson);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(snapshotJson)));
    }

    private static SortedDictionary<string, object?> Bale(
        BaleData bale, IEnumerable<ShipmentRowData> rows, IReadOnlyList<CustomFieldInfo> customKeys)
    {
        var ordered = rows.OrderBy(r => r.Seq).ThenBy(r => r.RugId.ToString("D"), StringComparer.Ordinal).ToList();
        return new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["number"] = bale.Number,
            ["weightKg"] = Fixed.Of(bale.WeightKg, 2),
            ["lines"] = ordered.Count,
            ["pieces"] = ordered.Sum(r => r.Quantity),
            ["areaM2"] = Fixed.Of(ordered.Sum(LineArea), 2),
            ["usdTotal"] = Fixed.Of(ordered.Sum(r => r.HasValue ? r.LineUsd ?? 0m : 0m), 2),
            ["rows"] = ordered.Select(r => (object?)Row(r, customKeys)).ToList()
        };
    }

    private static SortedDictionary<string, object?> Row(ShipmentRowData r, IReadOnlyList<CustomFieldInfo> customKeys)
    {
        var custom = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var field in customKeys)
            custom[field.Key] = DeclaredValueMath.MetadataValue(r.MetadataJson, field.Key);

        return new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["rugId"] = r.RugId.ToString("D"),
            ["sku"] = r.Sku,
            ["seq"] = r.Seq,
            ["serial"] = r.SerialNo,
            ["qty"] = r.Quantity,
            ["title"] = r.Title,
            ["origin"] = r.Origin,
            ["pattern"] = r.Pattern,
            ["material"] = r.Material,
            ["knotDensity"] = r.KnotDensity,
            ["custom"] = custom,
            ["intakeLength"] = Fixed.Of(r.IntakeLength, 2),
            ["intakeWidth"] = Fixed.Of(r.IntakeWidth, 2),
            ["finalLength"] = Fixed.Of(r.FinalLength, 2),
            ["finalWidth"] = Fixed.Of(r.FinalWidth, 2),
            ["pieceArea"] = Fixed.Of(r.AreaM2, 2),
            ["lineArea"] = Fixed.Of(LineArea(r), 2),
            ["usdPerM2"] = Fixed.Of(r.HasValue ? r.UsdPerM2 : null, 2),
            ["rateSource"] = SourceName(r.HasValue ? r.RateSource : DeclaredRateSource.Missing),
            ["pieceUsd"] = Fixed.Of(r.HasValue ? r.PieceUsd : null, 2),
            ["lineUsd"] = Fixed.Of(r.HasValue ? r.LineUsd : null, 2)
        };
    }

    private static decimal LineArea(ShipmentRowData r) => r.HasValue ? r.LineAreaM2 : r.AreaM2 * r.Quantity;

    /// <summary>نام پایدار منبع نرخ (عدد enum در عکس نمی‌نشیند تا خواندنش بی کد ممکن باشد).</summary>
    public static string SourceName(DeclaredRateSource source) => source switch
    {
        DeclaredRateSource.Override => "override",
        DeclaredRateSource.Table => "table",
        DeclaredRateSource.Fallback => "fallback",
        _ => "missing"
    };

    /// <summary>عدد اعشاری با تعداد رقم ثابت (۲٫۱ → «2.10»)، نوشته‌شده به‌صورت عدد JSON خام.</summary>
    private readonly record struct Fixed(decimal Value, int Scale)
    {
        public static object? Of(decimal? value, int scale) => value is { } v ? new Fixed(v, scale) : null;

        public string Text => Domain.Common.Num.Round(Value, Scale).ToString("F" + Scale.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    private static void Write(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string text:
                writer.WriteStringValue(text.Normalize(NormalizationForm.FormC));
                break;
            case int number:
                writer.WriteNumberValue(number);
                break;
            case Fixed fixedNumber:
                writer.WriteRawValue(fixedNumber.Text, skipInputValidation: true);
                break;
            case bool flag:
                writer.WriteBooleanValue(flag);
                break;
            case SortedDictionary<string, object?> map:
                writer.WriteStartObject();
                foreach (var (key, item) in map)
                {
                    writer.WritePropertyName(key);
                    Write(writer, item);
                }

                writer.WriteEndObject();
                break;
            case List<object?> list:
                writer.WriteStartArray();
                foreach (var item in list)
                    Write(writer, item);
                writer.WriteEndArray();
                break;
            default:
                throw new InvalidOperationException($"نوع {value.GetType().Name} در عکس محموله پیش‌بینی نشده است.");
        }
    }
}
