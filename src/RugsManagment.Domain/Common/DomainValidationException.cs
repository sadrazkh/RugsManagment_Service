namespace RugsManagment.Domain.Common;

/// <summary>
/// ورودی‌ای که قاعدهٔ دامنه را می‌شکند (اندازهٔ بیرون از ۵ سانت تا ۳۰ متر، شمارهٔ برعکس، …) → ۴۰۰ با
/// بدنهٔ <c>{message, errors: {field: [msg]}}</c>.
///
/// نامش عمداً «Validation» خالی نیست تا با <c>System.ComponentModel.DataAnnotations.ValidationException</c>
/// قاطی نشود. پیام‌ها فارسی و آمادهٔ نمایش‌اند؛ کلید <see cref="Errors"/> نام فیلد به camelCase است
/// (مثل «rug.serial» یا «length») تا جزیره خطا را زیر همان خانه نشان دهد.
/// </summary>
public class DomainValidationException : Exception
{
    private static readonly IReadOnlyDictionary<string, string[]> NoErrors =
        new Dictionary<string, string[]>(StringComparer.Ordinal);

    /// <summary>خطای کلی بدون فیلد مشخص.</summary>
    public DomainValidationException(string message)
        : this(message, NoErrors)
    {
    }

    /// <summary>خطای یک فیلد؛ همان پیام هم پیام کلی است.</summary>
    public DomainValidationException(string message, string field)
        : this(message, new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [message] })
    {
    }

    /// <summary>چند خطا روی چند فیلد.</summary>
    public DomainValidationException(string message, IReadOnlyDictionary<string, string[]> errors)
        : base(message)
    {
        Errors = errors;
    }

    /// <summary>نام فیلد → پیام‌های فارسی آن؛ ممکن است خالی باشد.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
