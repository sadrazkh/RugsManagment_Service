namespace RugsManagment.Application.Common;

/// <summary>
/// درخواستی که سرویس‌های Application رد می‌کنند چون خانه‌ای درست پر نشده → ۴۰۰ با بدنهٔ
/// <c>{message, errors: {field: [msg]}}</c> (هم در API و هم با toast در صفحه‌ها).
///
/// جای <c>InvalidOperationException</c>/<c>ArgumentException</c> برای خطاهای فرم: آن‌ها فقط یک پیام
/// دارند و جزیره نمی‌داند زیر کدام خانه نشانش دهد. نام عمداً «AppValidation» است تا با
/// <c>System.ComponentModel.DataAnnotations.ValidationException</c> قاطی نشود. پیام‌ها از
/// <see cref="Messages"/> می‌آیند.
/// </summary>
public class AppValidationException : Exception
{
    private static readonly IReadOnlyDictionary<string, string[]> NoErrors =
        new Dictionary<string, string[]>(StringComparer.Ordinal);

    /// <summary>خطای کلی بدون فیلد مشخص.</summary>
    public AppValidationException(string message)
        : this(message, NoErrors)
    {
    }

    /// <summary>خطای چند فیلد؛ کلیدها نام فیلد به camelCase (همان نام عضو JSON).</summary>
    public AppValidationException(string message, IReadOnlyDictionary<string, string[]> errors, string? code = null)
        : base(message)
    {
        Errors = errors;
        Code = code;
    }

    /// <summary>نام فیلد → پیام‌های فارسی آن؛ ممکن است خالی باشد.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }
    public string? Code { get; }

    /// <summary>خطای یک فیلد؛ همان پیام پیام کلی هم هست.</summary>
    public static AppValidationException ForField(string field, string message, string? code = null)
        => new(message, new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [message] }, code);
}
