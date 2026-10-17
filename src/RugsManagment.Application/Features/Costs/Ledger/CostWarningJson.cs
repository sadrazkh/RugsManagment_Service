using System.Text.Json;
using System.Text.Json.Serialization;

namespace RugsManagment.Application.Features.Costs.Ledger;

/// <summary>
/// هشدارهای تقسیم در <c>CostEntryResult.WarningsJson</c> (jsonb): آرایهٔ <c>{code, count, amount?, serial?}</c>. پیام فارسی ذخیره
/// نمی‌شود (هنگام خواندن با واژهٔ نگله و واحد پول کارگاه ساخته می‌شود)، پس عوض شدن متن یک پیام هیچ ردیفی را کهنه نمی‌کند.
/// پستگرس jsonb را با فاصله و ترتیب خودش برمی‌گرداند؛ پس مقایسه همیشه بعد از <see cref="Parse"/> است، نه روی متن.
/// </summary>
public static class CostWarningJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All)
    };

    /// <summary>هشدارها به JSON (ترتیب همان ورودی).</summary>
    public static string Serialize(IReadOnlyList<AllocationWarning> warnings)
    {
        ArgumentNullException.ThrowIfNull(warnings);
        return JsonSerializer.Serialize(warnings.Select(w => new Row(w.Code, w.Count, w.Amount, w.Serial)).ToList(), Options);
    }

    /// <summary>JSON ذخیره‌شده به هشدارها؛ متن خالی یا نادرست → هیچ.</summary>
    public static IReadOnlyList<AllocationWarning> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            var rows = JsonSerializer.Deserialize<List<Row>>(json, Options) ?? [];
            return rows.Where(r => !string.IsNullOrEmpty(r.Code))
                .Select(r => new AllocationWarning(r.Code!, r.Count, r.Amount, r.Serial))
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed record Row(string? Code, int Count, decimal? Amount, string? Serial);
}
