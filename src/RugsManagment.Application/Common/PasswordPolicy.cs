namespace RugsManagment.Application.Common;

/// <summary>
/// قاعدهٔ رمز عبور، یک جا برای هر سه مسیری که رمز می‌نویسند: کاربر تازه، بازنشانی رمز به دست تنظیم‌کننده
/// (صفحهٔ کاربران) و تغییر رمز به دست خودِ کاربر (پروفایل).
///
/// چرا یک جا: پیش از این فقط تغییر رمزِ خودِ کاربر حداقل طول داشت و تنظیم‌کننده می‌توانست برای بازرگان رمز
/// یک‌حرفی بگذارد؛ همان حسابی که همهٔ مبلغ‌ها را می‌بیند. حالا هر سه مسیر همین عدد و همین پیام را دارند.
/// </summary>
public static class PasswordPolicy
{
    /// <summary>حداقل طول رمز. کوتاه‌تر از این در برابر حدس‌زدن بی‌دفاع است.</summary>
    public const int MinLength = 8;

    /// <summary>رمز پذیرفتنی است: خالی یا فقط فاصله نیست و دست‌کم <see cref="MinLength"/> نویسه دارد.</summary>
    public static bool IsAcceptable(string? password)
        => !string.IsNullOrWhiteSpace(password) && password.Length >= MinLength;

    /// <summary>پیام فارسی رمز کوتاه (رقم‌ها فارسی).</summary>
    public static string TooShortMessage
        => $"رمز عبور باید حداقل {PersianText.ToPersianDigits(MinLength.ToString())} کاراکتر باشد.";
}
