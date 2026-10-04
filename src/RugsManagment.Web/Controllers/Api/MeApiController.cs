using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Platform;
using RugsManagment.Application.Features.Platform.DTOs;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>
/// کاربر جاری و ترجیحات خودش (Floor). شناسهٔ کاربر همیشه از کوکی می‌آید، نه از آدرس یا بدنه؛ پس کسی
/// نمی‌تواند ترجیحات دیگری را بخواند یا عوض کند.
/// </summary>
[Route("api/me")]
public sealed class MeApiController(IUserPreferencesService preferences) : ApiControllerBase
{
    /// <summary>کاربر جاری با برچسب نقش و ترجیحاتش.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await preferences.GetMeAsync(User.GetUserId(), ct));

    /// <summary>فقط ترجیحات (اندازهٔ نوشته، حالت، جهت Enter، راهنماهای بسته‌شده).</summary>
    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences(CancellationToken ct)
        => Ok(await preferences.GetAsync(User.GetUserId(), ct));

    /// <summary>
    /// <c>PUT /api/me/preferences</c> → ۲۰۴. بدنه <c>{textScale?, gridEnter?, uiMode?, dismissCoach?}</c>؛ هر عضوِ
    /// نیامده یا null یعنی «دست نزن»، پس کنترل اندازهٔ نوشته فقط <c>textScale</c> را می‌فرستد.
    ///
    /// چرا بدنهٔ خام و نه مستقیم <see cref="UpdateMyPreferencesRequest"/>: app-context نام‌ها را می‌نویسد
    /// («large»، «simple») و enum در JSON بقیهٔ API عدد است؛ این‌جا هر دو پذیرفته می‌شود (۱ یا «large» یا «Large»)
    /// تا جزیره‌ای که نام را از app-context برداشته بی‌صدا ۴۰۰ نگیرد. مقدار بیرون از enum → ۴۰۰ با خطای همان خانه.
    /// <c>uiMode</c> فقط از تنظیم‌کننده پذیرفته می‌شود (سرویس برای بقیه نادیده‌اش می‌گیرد).
    /// </summary>
    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] JsonElement body, CancellationToken ct)
    {
        var request = ParseRequest(body);
        await preferences.UpdateAsync(User.GetUserId(), request, ct);
        return NoContent();
    }

    // ── خواندن بدنه ──────────────────────────────────────────────────

    private static UpdateMyPreferencesRequest ParseRequest(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object)
            throw new AppValidationException(Messages.ValidationFailed.Text);

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        var textScale = ReadEnum<TextScale>(body, "textScale", "اندازهٔ نوشته", errors);
        var gridEnter = ReadEnum<GridEnterDirection>(body, "gridEnter", "جهت حرکت در جدول", errors);
        var uiMode = ReadEnum<UiMode>(body, "uiMode", "حالت نمایش", errors);
        var dismissCoach = ReadString(body, "dismissCoach", "راهنمای بسته‌شده", errors);

        if (errors.Count > 0)
            throw new AppValidationException(Messages.ValidationFailed.Text, errors);

        return new UpdateMyPreferencesRequest(textScale, gridEnter, uiMode, dismissCoach);
    }

    /// <summary>
    /// یک enum از عدد (۰، ۱، …) یا نام (بی‌حساسیت به بزرگی حروف؛ «large»، «XLarge»). نیامده یا null → null.
    /// </summary>
    private static TEnum? ReadEnum<TEnum>(
        JsonElement body, string name, string label, Dictionary<string, string[]> errors)
        where TEnum : struct, Enum
    {
        if (!TryGetProperty(body, name, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;

        if (value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var number)
            && Enum.IsDefined(typeof(TEnum), number))
        {
            return (TEnum)Enum.ToObject(typeof(TEnum), number);
        }

        // فقط نام؛ رشتهٔ عددی («1») یا ترکیب پرچم‌ها («Normal, Large») پذیرفته نیست
        if (value.ValueKind == JsonValueKind.String
            && value.GetString() is { Length: > 0 } text
            && char.IsLetter(text[0])
            && !text.Contains(',')
            && Enum.TryParse<TEnum>(text, ignoreCase: true, out var named)
            && Enum.IsDefined(named))
        {
            return named;
        }

        errors[name] = [Messages.InvalidValue.Format(("label", label))];
        return null;
    }

    private static string? ReadString(
        JsonElement body, string name, string label, Dictionary<string, string[]> errors)
    {
        if (!TryGetProperty(body, name, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;

        if (value.ValueKind == JsonValueKind.String)
            return value.GetString();

        errors[name] = [Messages.InvalidValue.Format(("label", label))];
        return null;
    }

    /// <summary>عضو بدنه بی‌حساسیت به بزرگی حروف (همان رفتار bind پیش‌فرض JSON در بقیهٔ API).</summary>
    private static bool TryGetProperty(JsonElement body, string name, out JsonElement value)
    {
        foreach (var property in body.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
