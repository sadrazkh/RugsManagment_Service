namespace RugsManagment.Application.Abstractions.Persistence;

/// <summary>
/// خطای هم‌زمانی: رکورد بین خواندن و ذخیره توسط کاربر دیگری تغییر کرده است.
/// لایهٔ Infrastructure استثنای EF را به این تبدیل می‌کند تا Application به EF وابسته نشود.
/// </summary>
public sealed class ConcurrencyConflictException(string? message = null)
    : Exception(message ?? "این رکورد هم‌زمان توسط کاربر دیگری تغییر کرده است. صفحه را تازه کنید و دوباره تلاش کنید.");

/// <summary>
/// نقض قید یکتایی دیتابیس (مثلاً SKU یا ایمیل تکراری).
/// </summary>
public sealed class DuplicateKeyException(string? message = null)
    : Exception(message ?? "رکوردی با همین مشخصات یکتا از قبل وجود دارد.");
