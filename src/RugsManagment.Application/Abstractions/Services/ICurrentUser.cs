namespace RugsManagment.Application.Abstractions.Services;

/// <summary>
/// کاربری که همین حالا درخواست را فرستاده.
///
/// لایهٔ Application نباید HttpContext را بشناسد، ولی برای «چه کسی این کار را کرد»
/// به هویت کاربر نیاز دارد؛ این انتزاع همان پل است. پیاده‌سازی در لایهٔ وب از کوکی می‌خواند.
///
/// در اجرای پس‌زمینه (seed، cron) کاربری وجود ندارد و همهٔ مقادیر null می‌شوند.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    /// <summary>نام نمایشی برای ثبت در لاگ — تا حتی اگر کاربر بعداً حذف شد، لاگ خوانا بماند</summary>
    string? DisplayName { get; }

    Guid? TenantId { get; }
}
