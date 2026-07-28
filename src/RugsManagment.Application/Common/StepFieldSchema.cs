using System.Text.Json;
using System.Text.Json.Serialization;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Common;

/// <summary>یک فیلد در فرم داینامیک یک مرحله.</summary>
public sealed record StepFieldDefinition
{
    /// <summary>کلید ذخیره در FieldValuesJson — انگلیسی و بدون فاصله</summary>
    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public CustomFieldType Type { get; init; } = CustomFieldType.Text;

    [JsonPropertyName("required")]
    public bool Required { get; init; }

    /// <summary>گزینه‌های نوع «انتخابی»</summary>
    [JsonPropertyName("options")]
    public string[]? Options { get; init; }

    /// <summary>راهنمای کوتاه زیر فیلد</summary>
    [JsonPropertyName("hint")]
    public string? Hint { get; init; }
}

/// <summary>
/// خواندن و اعتبارسنجی اسکیمای فیلدهای یک مرحله.
///
/// اسکیما از رابط کاربری می‌آید، پس سرور نباید به آن اعتماد کند: ساختار، یکتایی کلیدها
/// و کامل بودن گزینه‌های «انتخابی» اینجا بررسی می‌شود. مقادیر واردشده هم هنگام
/// تکمیل مرحله در برابر همین اسکیما سنجیده می‌شوند.
/// </summary>
public static class StepFieldSchema
{
    /// <summary>سقف تعداد فیلد در یک مرحله — فرم طولانی‌تر از این عملاً استفاده نمی‌شود.</summary>
    public const int MaxFields = 15;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>فیلدهای اسکیما؛ برای اسکیمای خالی یا نامعتبر آرایهٔ خالی برمی‌گردد.</summary>
    public static IReadOnlyList<StepFieldDefinition> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        try
        {
            return JsonSerializer.Deserialize<StepFieldDefinition[]>(json, Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static bool IsValid(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return true;

        IReadOnlyList<StepFieldDefinition> fields;
        try
        {
            fields = JsonSerializer.Deserialize<StepFieldDefinition[]>(json, Options) ?? [];
        }
        catch (JsonException)
        {
            return false;
        }

        if (fields.Count > MaxFields) return false;

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field.Key) || string.IsNullOrWhiteSpace(field.Label))
                return false;

            // کلید باید شناسهٔ ساده باشد تا در JSON و آدرس‌ها دردسر نسازد
            if (!field.Key.All(c => char.IsAsciiLetterOrDigit(c) || c is '_'))
                return false;

            if (!keys.Add(field.Key)) return false;

            // «انتخابی» بدون گزینه فرمی می‌سازد که کاربر نمی‌تواند پرش کند
            if (field.Type == CustomFieldType.Select && (field.Options is null || field.Options.Length == 0))
                return false;
        }

        return true;
    }

    /// <summary>
    /// مقادیر واردشده را در برابر اسکیما می‌سنجد و نسخهٔ پاک‌شده برمی‌گرداند.
    /// کلیدهای ناشناخته حذف می‌شوند تا کسی نتواند دادهٔ دلخواه در ستون jsonb بریزد.
    /// </summary>
    public static string? ValidateValues(string? schemaJson, string? valuesJson)
    {
        var fields = Parse(schemaJson);
        if (fields.Count == 0) return null;

        Dictionary<string, JsonElement> values = [];
        if (!string.IsNullOrWhiteSpace(valuesJson))
        {
            try
            {
                values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(valuesJson, Options) ?? [];
            }
            catch (JsonException)
            {
                throw new InvalidOperationException("مقادیر فرم مرحله معتبر نیست.");
            }
        }

        var clean = new Dictionary<string, object?>();
        foreach (var field in fields)
        {
            values.TryGetValue(field.Key, out var raw);
            var text = raw.ValueKind switch
            {
                JsonValueKind.Undefined or JsonValueKind.Null => null,
                JsonValueKind.String => raw.GetString(),
                _ => raw.ToString()
            };

            if (string.IsNullOrWhiteSpace(text))
            {
                if (field.Required)
                    throw new InvalidOperationException($"فیلد «{field.Label}» الزامی است.");
                continue;
            }

            text = text.Trim();

            switch (field.Type)
            {
                case CustomFieldType.Number:
                    // ارقام فارسی هم پذیرفته می‌شوند و به لاتین تبدیل می‌گردند
                    var normalized = PersianText.Normalize(text);
                    if (!decimal.TryParse(normalized, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var number))
                        throw new InvalidOperationException($"مقدار فیلد «{field.Label}» عدد معتبر نیست.");
                    clean[field.Key] = number;
                    break;

                case CustomFieldType.Boolean:
                    clean[field.Key] = text is "true" or "True" or "1" or "بله";
                    break;

                case CustomFieldType.Select:
                    if (field.Options is null || !field.Options.Contains(text))
                        throw new InvalidOperationException($"مقدار فیلد «{field.Label}» در فهرست گزینه‌ها نیست.");
                    clean[field.Key] = text;
                    break;

                default:
                    clean[field.Key] = text.Length > 500 ? text[..500] : text;
                    break;
            }
        }

        return clean.Count == 0 ? null : JsonSerializer.Serialize(clean);
    }
}
