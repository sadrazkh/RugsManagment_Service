using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Features.Platform;

/// <summary>
/// ردیف ترجیحات هر کارگاه را، اگر نیست، با پیش‌فرض‌های <see cref="TenantPreferences.CreateDefault"/> می‌سازد —
/// هم برای کارگاه تازه و هم در هر استارت برای کارگاه‌های قدیمی. ردیف موجود هرگز دست نمی‌خورد.
/// </summary>
public sealed class TenantPreferencesSeedContributor(ITenantPreferencesRepository preferences) : ITenantSeedContributor
{
    /// <summary>اولین contributor: بقیه (مثلاً دسته‌های هزینه با واژهٔ نگله/عدل) ممکن است به این ردیف تکیه کنند.</summary>
    public int Order => 100;

    public async Task SeedAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (await preferences.ExistsAsync(tenantId, cancellationToken))
            return;

        await preferences.AddAsync(TenantPreferences.CreateDefault(tenantId), cancellationToken);
    }
}
