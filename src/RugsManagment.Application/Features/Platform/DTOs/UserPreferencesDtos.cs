using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Platform.DTOs;

/// <summary>ترجیحات خودِ کاربر — همان که پوستهٔ صفحه (<c>data-ui</c>، <c>data-text-scale</c>) و جدول‌ها می‌خوانند.</summary>
/// <param name="UiMode">ساده یا کامل (فقط پوسته، نه دسترسی)</param>
/// <param name="TextScale">اندازهٔ نوشته</param>
/// <param name="GridEnter">جهت Enter در جدول‌ها</param>
/// <param name="CoachDismissed">کلید راهنماهای شروعی که کاربر بسته است</param>
public record UserPreferencesDto(
    UiMode UiMode,
    TextScale TextScale,
    GridEnterDirection GridEnter,
    IReadOnlyList<string> CoachDismissed);

/// <summary>
/// بدنهٔ <c>PUT /api/me/preferences</c> (Floor؛ همیشه فقط کاربرِ خودِ درخواست، پاسخ ۲۰۴). هر عضو null
/// یعنی «دست نزن»، پس کنترل اندازهٔ نوشته فقط <see cref="TextScale"/> را می‌فرستد.
///
/// <see cref="UiMode"/> فقط از تنظیم‌کننده (TenantAdmin) پذیرفته می‌شود و برای بقیه نادیده گرفته می‌شود:
/// حالتِ بازرگان و انباردار را تنظیم‌کننده در صفحهٔ کاربران می‌گذارد (چک‌لیست آماده‌سازی به آن تکیه
/// دارد). پیش‌نمایش «نمای ساده»ی تنظیم‌کننده کوکی است و از این مسیر نمی‌گذرد.
/// </summary>
/// <param name="TextScale">اندازهٔ نوشتهٔ تازه</param>
/// <param name="GridEnter">جهت Enter تازه</param>
/// <param name="UiMode">حالت تازه (فقط تنظیم‌کننده)</param>
/// <param name="DismissCoach">کلید یک راهنمای شروع که بسته شد (به فهرست اضافه می‌شود؛ تکراری بی‌اثر)</param>
public record UpdateMyPreferencesRequest(
    TextScale? TextScale = null,
    GridEnterDirection? GridEnter = null,
    UiMode? UiMode = null,
    string? DismissCoach = null);

/// <summary>پاسخ <c>GET /api/me</c>: کاربر جاری و ترجیحاتش (بدون مهر امنیتی).</summary>
/// <param name="UserId">شناسهٔ کاربر</param>
/// <param name="TenantId">کارگاه (null برای مدیر سامانه)</param>
/// <param name="TenantName">نام کارگاه</param>
/// <param name="FullName">نام کاربر</param>
/// <param name="Email">ایمیل ورود</param>
/// <param name="Role">نقش (عدد)</param>
/// <param name="RoleLabel">برچسب فارسی نقش از <c>RoleLabels</c></param>
/// <param name="Preferences">ترجیحات کاربر</param>
public record MeDto(
    Guid UserId,
    Guid? TenantId,
    string? TenantName,
    string FullName,
    string Email,
    UserRole Role,
    string RoleLabel,
    UserPreferencesDto Preferences);
