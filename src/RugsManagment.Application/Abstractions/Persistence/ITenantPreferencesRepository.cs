using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Abstractions.Persistence;

/// <summary>ردیف ترجیحات کارگاه همراه نسخهٔ هم‌زمانی‌اش (xmin) در لحظهٔ خواندن.</summary>
/// <param name="Preferences">ردیف (بدون ردیابی؛ برای نمایش)</param>
/// <param name="Version">xmin همان لحظه — همان عددی که در DTO به‌عنوان Version می‌رود</param>
public sealed record VersionedTenantPreferences(TenantPreferences Preferences, uint Version);

/// <summary>
/// دسترسی به ردیف ترجیحات کارگاه (یک ردیف برای هر کارگاه، کلید = TenantId).
///
/// نسخهٔ هم‌زمانی ستون سیستمی xmin پستگرس است و در موجودیت دامنه نیست (shadow property)؛ پس خواندن و
/// «مسلح کردن» آن همین‌جا انجام می‌شود تا Application به EF وابسته نشود.
/// </summary>
public interface ITenantPreferencesRepository
{
    /// <summary>ردیف و نسخه‌اش برای نمایش؛ null یعنی هنوز ردیفی ساخته نشده.</summary>
    Task<VersionedTenantPreferences?> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// ردیفِ ردیابی‌شده برای نوشتن، با بررسی نسخه: اگر تا ذخیره xmin ردیف با <paramref name="expectedVersion"/>
    /// برابر نباشد، SaveChanges خطای هم‌زمانی می‌دهد (<see cref="ConcurrencyConflictException"/> → ۴۰۹).
    /// null یعنی ردیف وجود ندارد.
    /// </summary>
    Task<TenantPreferences?> GetForUpdateAsync(
        Guid tenantId, uint expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>نسخهٔ فعلیِ ردیفی که ردیابی می‌شود — بعد از SaveChanges همان xmin تازه.</summary>
    uint CurrentVersion(TenantPreferences tracked);

    /// <summary>آیا کارگاه ردیف ترجیحات دارد (برای seed یک‌باره).</summary>
    Task<bool> ExistsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>ردیف تازه در UnitOfWork (بدون ذخیره).</summary>
    Task AddAsync(TenantPreferences preferences, CancellationToken cancellationToken = default);

    /// <summary>
    /// آیا در این کارگاه هیچ مبلغی ذخیره شده (فهرست D.5 #22 تا جایی که جدول‌هایش هست): قیمت خرید یا قیمت هدف
    /// روی یک فرش (حتی در سطل زباله)، مرحله‌ای با هزینه، فروش، پرداخت به طرف خدمات، نرخ توافقی، یا نوع مرحلهٔ
    /// اختصاصی با نرخ پیش‌فرض بیشتر از صفر. اگر بله، تعداد رقم اعشار مبلغ‌ها و واحد پول دیگر عوض نمی‌شود.
    /// </summary>
    Task<bool> HasStoredMoneyAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
