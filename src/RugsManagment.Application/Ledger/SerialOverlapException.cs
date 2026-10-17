using System.Globalization;
using RugsManagment.Application.Common;
using RugsManagment.Domain.Common;

namespace RugsManagment.Application.Ledger;

/// <summary>
/// شماره‌ای که با شمارهٔ یک فرش فعال دیگرِ همین کارگاه همپوشانی دارد → ۴۰۹ با <c>field: "rug.serial"</c> و
/// <c>nextFree</c> (spec I.1، G.4). همپوشانی دسته‌ها را ایندکس یکتا نمی‌بیند (فقط متن یکسان را)، پس
/// <c>SerialOverlapStage</c> زیر قفل کارگاه و روی ردیف‌های flushشدهٔ همین درخواست می‌سنجد؛ برش خودکار دسته (که فرش اصلی را
/// کوتاه و فرش تازه را با بقیهٔ بازه می‌سازد) پس همپوشانی با خودش نیست.
///
/// <see cref="NextFree"/> همان قاعدهٔ «شمارهٔ بعدی» است (max(SerialStart ?? 1، بزرگ‌ترین شمارهٔ دیگران + ۱)) تا جدول بپرسد
/// «شمارهٔ ۴۰۰۵ آزاد است؛ همان را بگذارم؟». متن پیام با کد <see cref="Code"/> در <c>Messages.fa.Rugs</c> و
/// <c>lib/messages/rugs.ts</c> آینه می‌شود (نشانه‌ها: <c>{serial}</c>، <c>{other}</c>).
/// </summary>
public sealed class SerialOverlapException : DomainConflictException
{
    /// <summary>کد پیام.</summary>
    public const string Code = "E_SERIAL_OVERLAP";

    /// <summary>الگوی پیام؛ <c>{serial}</c> شمارهٔ درخواست‌شده و <c>{other}</c> شمارهٔ فرشی که آن را دارد.</summary>
    public const string OverlapMessage = "شمارهٔ «{serial}» با شمارهٔ فرش دیگری («{other}») هم‌پوشانی دارد.";

    /// <summary>الگوی پیام وقتی فرش دیگر معلوم نیست (فقط متن یکسان از ایندکس یکتا).</summary>
    public const string TakenMessage = "شمارهٔ «{serial}» مال فرش دیگری است.";

    public SerialOverlapException(string serial, string? conflictingSerial, int? nextFree, Exception? inner = null)
        : base(BuildMessage(serial, conflictingSerial), SerialNumber.Field, inner)
    {
        Serial = serial;
        ConflictingSerial = conflictingSerial;
        NextFree = nextFree;
    }

    /// <summary>شمارهٔ درخواست‌شده (شکل نمایشی).</summary>
    public string Serial { get; }

    /// <summary>شمارهٔ فرشی که همپوشانی دارد؛ null وقتی معلوم نیست.</summary>
    public string? ConflictingSerial { get; }

    /// <summary>اولین شمارهٔ آزاد بعد از بزرگ‌ترین شمارهٔ فرش‌های دیگر؛ null وقتی حساب نشد.</summary>
    public int? NextFree { get; }

    /// <summary>همان نمونه با <see cref="NextFree"/> پرشده (پاس بعد از برگرداندن تراکنش حسابش می‌کند).</summary>
    public SerialOverlapException WithNextFree(int? nextFree)
        => new(Serial, ConflictingSerial, nextFree, InnerException);

    public override IReadOnlyDictionary<string, object?>? Extensions
        => new Dictionary<string, object?>(StringComparer.Ordinal) { ["nextFree"] = NextFree };

    private static string BuildMessage(string serial, string? other)
    {
        var text = other is null ? TakenMessage : OverlapMessage;
        return text
            .Replace("{serial}", PersianText.ToPersianDigits(serial), StringComparison.Ordinal)
            .Replace("{other}", PersianText.ToPersianDigits(other ?? string.Empty), StringComparison.Ordinal);
    }

    /// <summary>عدد برای لاگ (فرهنگ ثابت).</summary>
    public override string ToString()
        => $"{nameof(SerialOverlapException)}: {Serial} vs {ConflictingSerial} (nextFree {NextFree?.ToString(CultureInfo.InvariantCulture) ?? "-"})";
}
