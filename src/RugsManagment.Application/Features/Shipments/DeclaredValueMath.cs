using System.Text;
using RugsManagment.Application.Common;
using RugsManagment.Application.Fields;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Shipments;

/// <summary>
/// قاعده‌های ارزش گمرکی (spec C.5)، خالص و بی دیتابیس — تنها جای این ترتیب در C#؛ مرحلهٔ پاس، آمادگی، enricher و آزمون‌ها
/// همه از همین می‌خوانند.
/// </summary>
public static class DeclaredValueMath
{
    /// <summary>
    /// «KeyNormalize»: شکل NFC، بعد <see cref="PersianText.Normalize"/> (ی و ک فارسی، ارقام لاتین، نیم‌فاصله به فاصله، فاصله‌های
    /// جمع‌شده) و حروف لاتین کوچک. پس «نائین ۹ لا» = «نائين 9 لا» و «Tabriz» = «tabriz». متن خالی → رشتهٔ خالی.
    /// </summary>
    public static string KeyNormalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return PersianText.Normalize(value.Normalize(NormalizationForm.FormC)).ToLowerInvariant();
    }

    /// <summary>
    /// نرخ یک خط به ترتیب اولویت: نرخ دستیِ خط ← جدول با مقدار یکسان‌شدهٔ فیلد کلید ← نرخ پیش‌فرض کارگاه ← «ندارد».
    /// </summary>
    /// <param name="lineOverride">نرخ دستی خط (بازرگان).</param>
    /// <param name="keyValue">مقدار فیلد کلید فرش (مثل «گبه»)، یکسان‌نشده.</param>
    /// <param name="table">جدول با کلید یکسان‌شده (<see cref="KeyNormalize"/>).</param>
    /// <param name="fallback">نرخ پیش‌فرض کارگاه.</param>
    public static (decimal? Rate, DeclaredRateSource Source) Resolve(
        decimal? lineOverride, string? keyValue, IReadOnlyDictionary<string, decimal> table, decimal? fallback)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (lineOverride is { } manual)
            return (manual, DeclaredRateSource.Override);

        var key = KeyNormalize(keyValue);
        if (key.Length > 0 && table.TryGetValue(key, out var rate))
            return (rate, DeclaredRateSource.Table);

        if (fallback is { } f)
            return (f, DeclaredRateSource.Fallback);

        return (null, DeclaredRateSource.Missing);
    }

    /// <summary>
    /// ارزش یک خط با قاعدهٔ دسته (G.4، D.3): مساحت تخته × تعداد؛ دلار تخته = ROUND(مساحت تخته × نرخ، ۲)؛ دلار خط = دلار تخته
    /// × تعداد. بی نرخ، دلارها null و متراژ همچنان حساب می‌شود.
    /// </summary>
    public static (decimal LineAreaM2, decimal? PieceUsd, decimal? LineUsd) Value(decimal pieceAreaM2, int quantity, decimal? rate)
    {
        var lineArea = AreaMath.LineArea(pieceAreaM2, quantity);
        if (rate is not { } r) return (lineArea, null, null);
        var piece = AreaMath.PieceUsd(pieceAreaM2, r);
        return (lineArea, piece, AreaMath.LineUsd(piece, quantity));
    }

    /// <summary>
    /// تغییر علامت‌دار اندازه به سانت (C.4): از دو تغییر طول و عرض، آنکه قدر مطلقش بزرگ‌تر است (برابر: طول)؛ منفی یعنی
    /// کوچک‌تر شده. بی اندازهٔ نهایی null؛ بی تغییر ۰.
    /// </summary>
    public static int? SizeChangeCm(decimal intakeLength, decimal intakeWidth, decimal? finalLength, decimal? finalWidth)
    {
        if (finalLength is not { } fl || finalWidth is not { } fw) return null;
        var dl = Num.Round((fl - intakeLength) * 100m, 0);
        var dw = Num.Round((fw - intakeWidth) * 100m, 0);
        var change = Math.Abs(dw) > Math.Abs(dl) ? dw : dl;
        return (int)change;
    }

    /// <summary>آیا متراژ نگله از هدف + تلورانس گذشته (C.2: هدف ۱۳ با ۱۰٪ یعنی بالای ۱۴٫۳).</summary>
    public static bool IsOverTarget(decimal areaM2, decimal? targetAreaM2, short tolerancePercent)
        => targetAreaM2 is { } target && target > 0 && areaM2 > target * (1m + tolerancePercent / 100m);

    /// <summary>برچسب فارسیِ فیلد کلید برای پیام‌ها («محل بافت»، یا برچسب فیلد سفارشی اگر داده شد).</summary>
    public static string KeyFieldLabel(string keyField, string? customLabel = null)
    {
        if (customLabel is not null) return customLabel;
        return RugFieldContributor.StaticFields.FirstOrDefault(f => f.Key == keyField)?.Label ?? keyField;
    }

    /// <summary>
    /// مقدار فیلد کلید از ستون‌های فرش در C# (همان که SQL پاس می‌خواند): <c>rug.origin</c>، <c>rug.pattern</c>،
    /// <c>rug.material</c>، <c>rug.title</c>، یا <c>rug.custom.{key}</c> از <c>MetadataJson</c>. کلید ناشناخته → null.
    /// </summary>
    public static string? KeyValueOf(string keyField, string? title, string? origin, string? pattern, string? material, string? metadataJson)
    {
        switch (keyField)
        {
            case RugFieldKeys.Origin: return origin;
            case RugFieldKeys.Pattern: return pattern;
            case RugFieldKeys.Material: return material;
            case RugFieldKeys.Title: return title;
        }

        var custom = CustomKeyOf(keyField);
        return custom is null ? null : MetadataValue(metadataJson, custom);
    }

    /// <summary>کلید فیلد سفارشیِ <c>rug.custom.{key}</c>؛ برای بقیه null.</summary>
    public static string? CustomKeyOf(string keyField)
        => keyField.StartsWith(RugFieldKeys.CustomPrefix, StringComparison.Ordinal) && keyField.Length > RugFieldKeys.CustomPrefix.Length
            ? keyField[RugFieldKeys.CustomPrefix.Length..]
            : null;

    /// <summary>مقدار متنی یک کلید از JSON متادیتای فرش (عدد و بولی به متن invariant)؛ نبود یا JSON نامعتبر → null.</summary>
    public static string? MetadataValue(string? metadataJson, string key)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return null;
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(metadataJson);
            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object
                || !document.RootElement.TryGetProperty(key, out var value))
                return null;

            return value.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => value.GetString(),
                System.Text.Json.JsonValueKind.Number => value.GetRawText(),
                System.Text.Json.JsonValueKind.True => "true",
                System.Text.Json.JsonValueKind.False => "false",
                _ => null
            };
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
