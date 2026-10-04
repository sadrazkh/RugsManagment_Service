using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Rugs;

/// <summary>
/// «شمارهٔ بعدی» فرش (spec G.4) — برای <c>GET /api/rugs/next-serial</c>، شمارهٔ پیشنهادی فرم و جدول، شمارهٔ خودکار
/// (حالت <see cref="SerialMode.Auto"/>) و <c>nextFree</c> در پاسخ ۴۰۹ همپوشانی.
/// </summary>
public interface ISerialSuggester
{
    /// <summary>
    /// شمارهٔ پیشنهادی برای نمایش؛ در حالت «دستی» null، چون آن حالت عمداً هیچ پیشنهادی نشان نمی‌دهد.
    /// </summary>
    Task<int?> SuggestAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// اولین شمارهٔ آزاد بعد از بزرگ‌ترین شمارهٔ در حال استفاده، بی‌توجه به حالت — برای شمارهٔ خودکار و <c>nextFree</c>.
    /// </summary>
    Task<int> NextFreeAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>حالت شمارهٔ کارگاه (پیش‌فرض «پیشنهاد» وقتی ردیف ترجیحات هنوز نیست).</summary>
    Task<SerialMode> ModeAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// قاعدهٔ سرور: <c>next = max(SerialStart ?? 1, MAX(COALESCE(SerialTo, SerialFrom)) + 1)</c> روی فرش‌های فعال کارگاه،
/// صادرشده و فروخته‌شده هم (شماره بینشان یکتا می‌ماند، M.2 #4)، شماره‌های متنی نه.
///
/// <para>چرا «آخرِ بازه + ۱» و نه «اولِ بازه + تعداد»: بعد از «3985 - 4003» شمارهٔ بعدی ۴۰۰۴ است؛ اگر بازه از تعداد
/// بلندتر باشد (که مجاز است) «اول + تعداد» وسط همان بازه می‌افتاد. و چرا «بزرگ‌ترین» و نه «آخرین ثبت‌شده»: شماره‌های
/// اشتباه‌تایپ‌شدهٔ کوچک‌تر (۲۰۲۶ تا ۲۰۲۸ در فایل نمونه) شمارهٔ بعدی را پایین نمی‌کشند.</para>
///
/// <para>همپوشانی را این‌جا نمی‌سنجیم: دو کاربر ممکن است همان پیشنهاد را بگیرند؛ دومی در <c>SerialOverlapStage</c>
/// (زیر قفل کارگاه) ۴۰۹ می‌گیرد و جدول «شمارهٔ ۴۰۰۵ آزاد است؛ همان را بگذارم؟» را نشان می‌دهد.</para>
/// </summary>
public sealed class SerialSuggester(
    IRugRepository rugs,
    ITenantPreferencesRepository preferences) : ISerialSuggester
{
    public async Task<int?> SuggestAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var prefs = await LoadAsync(tenantId, cancellationToken);
        if (prefs.SerialMode == SerialMode.Manual)
            return null;

        return Next(prefs.SerialStart, await rugs.GetMaxSerialAsync(tenantId, cancellationToken));
    }

    public async Task<int> NextFreeAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var prefs = await LoadAsync(tenantId, cancellationToken);
        return Next(prefs.SerialStart, await rugs.GetMaxSerialAsync(tenantId, cancellationToken));
    }

    public async Task<SerialMode> ModeAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => (await LoadAsync(tenantId, cancellationToken)).SerialMode;

    // ═════════════════════════════════════════════════════════════════════
    // قاعده‌های خالص (آزمون واحد)
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <c>max(serialStart ?? 1, maxInUse + 1)</c>. بی هیچ شمارهٔ عددی همان کف است. در لبهٔ int (شمارهٔ
    /// ۲٬۱۴۷٬۴۸۳٬۶۴۷) جلوتر نمی‌رود؛ همپوشانی آن را رد می‌کند و کاربر شمارهٔ دیگری می‌نویسد.
    /// </summary>
    public static int Next(int? serialStart, int? maxInUse)
    {
        var floor = Math.Max(serialStart ?? 1, 1);
        var afterMax = maxInUse is { } max ? (long)max + 1 : 1L;
        return (int)Math.Min(Math.Max(floor, afterMax), int.MaxValue);
    }

    /// <summary>
    /// شمارهٔ بعد از یک ردیفِ همین درخواست یا جدول (پیش از ذخیره): <c>(SerialTo ?? SerialFrom) + 1</c>؛ شمارهٔ متنی یا
    /// بی‌شماره → null (ردیف بعدی از قاعدهٔ سرور می‌گیرد).
    /// </summary>
    public static int? NextAfter(SerialNumber? serial) => serial?.NextAfter;

    /// <summary>
    /// شمارهٔ خودکار برای ردیف تازه‌ای با <paramref name="quantity"/> تخته که از <paramref name="next"/> شروع می‌شود:
    /// تک برای یک تخته، و بازهٔ <c>next .. next + qty − 1</c> برای دسته (G.4).
    /// </summary>
    public static SerialNumber ForNewRow(int next, int quantity)
        => quantity <= 1 ? SerialNumber.Single(next) : SerialNumber.ForLot(next, quantity);

    private async Task<TenantPreferences> LoadAsync(Guid tenantId, CancellationToken cancellationToken)
        => (await preferences.GetAsync(tenantId, cancellationToken))?.Preferences
           ?? TenantPreferences.CreateDefault(tenantId);
}
