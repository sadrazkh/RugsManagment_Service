using RugsManagment.Application.Features.Platform.DTOs;

namespace RugsManagment.Application.Features.Platform;

/// <summary>
/// خواندن و ذخیرهٔ ترجیحات کارگاه (<c>/api/settings/preferences</c>، صفحهٔ ترجیحات، app-context).
/// </summary>
public interface ITenantPreferencesService
{
    /// <summary>
    /// ترجیحات کارگاه. اگر ردیف هنوز ساخته نشده (کارگاهی که seed به آن نرسیده) خطا نمی‌دهد و همان
    /// پیش‌فرض‌ها را با <c>Version = 0</c> برمی‌گرداند، بی‌آنکه چیزی ذخیره کند؛ چون layout هر صفحه این را
    /// می‌خواند و نباید هیچ صفحه‌ای به‌خاطرش بشکند.
    /// </summary>
    Task<TenantPreferencesDto> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// ذخیره با بررسی نسخه: نسخهٔ کهنه → ۴۰۹ (<c>ConcurrencyConflictException</c>)؛ تغییر MoneyScale وقتی مبلغی
    /// ثبت شده → ۴۰۹ (<c>DomainConflictException</c> با field = «moneyScale»)؛ مقدار نامعتبر → ۴۰۰
    /// (<c>AppValidationException</c> با خطای هر خانه). متن‌ها با <c>PersianText.Normalize</c> تمیز و رویداد
    /// <c>PreferencesChanged</c> با جزئیات تغییرها ثبت می‌شود. ترجیحات تازه را با نسخهٔ جدید برمی‌گرداند.
    /// </summary>
    Task<TenantPreferencesDto> UpdateAsync(
        Guid tenantId, TenantPreferencesDto request, CancellationToken cancellationToken = default);
}
