using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;

namespace RugsManagment.Infrastructure.Persistence.Seed;

/// <summary>
/// پیش‌فرض‌های هر کارگاه را از همهٔ <see cref="ITenantSeedContributor"/>ها می‌سازد: یک بار برای کارگاه تازه
/// (از <c>TenantManagementService.CreateAsync</c>) و در هر استارت برای همهٔ کارگاه‌ها (کار استارت با ترتیب ۱۰۰،
/// بعد از migration و DatabaseSeeder، زیر قفل advisory استارت).
///
/// چرا در هر استارت: هر فاز contributor تازه‌ای اضافه می‌کند (ترجیحات، دسته‌های هزینه، قالب اظهار، …) و
/// کارگاه‌های قدیمی هم باید آن را بگیرند بی‌آنکه کسی migration دادهٔ جدا بنویسد. contributorها idempotent‌اند،
/// پس استارت دوم هیچ چیزی نمی‌نویسد.
///
/// خطای یک کارگاه استارت را نمی‌خواباند: ثبت می‌شود، UnitOfWork پاک می‌شود و کارگاه بعدی ادامه می‌دهد؛
/// خواننده‌ها نبودِ ردیف را تحمل می‌کنند (مثلاً ترجیحاتِ پیش‌فرض با نسخهٔ ۰) و استارت بعدی دوباره امتحان می‌کند.
/// </summary>
public sealed class TenantSeedRunner(
    IEnumerable<ITenantSeedContributor> contributors,
    AppDbContext db,
    IUnitOfWork unitOfWork,
    ILogger<TenantSeedRunner> logger) : ITenantSeedRunner, IStartupTask
{
    /// <summary>بعد از کارهای پایهٔ استارت؛ کارهای فاز حساب‌ها (۲۰۰) بعد از این اجرا می‌شوند.</summary>
    public int Order => 100;

    public async Task SeedTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        foreach (var contributor in Ordered())
            await contributor.SeedAsync(tenantId, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// کار استارت. نمونه‌ای از runner که در scope همین کار ساخته شده اجرا می‌شود (نه این نمونه که فقط برای
    /// فهرست‌کردن کارها ساخته شد)، تا DbContext و contributorها مال همین scope باشند.
    /// </summary>
    public Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
        => services.GetRequiredService<TenantSeedRunner>().SeedAllTenantsAsync(cancellationToken);

    /// <summary>همهٔ کارگاه‌ها، قدیمی‌ترین اول. تعداد کارگاه‌هایی را که خطا داشتند برمی‌گرداند.</summary>
    public async Task<int> SeedAllTenantsAsync(CancellationToken cancellationToken = default)
    {
        var tenantIds = await db.Tenants
            .AsNoTracking()
            .OrderBy(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var failed = 0;
        foreach (var tenantId in tenantIds)
        {
            try
            {
                await SeedTenantAsync(tenantId, cancellationToken);
            }
            catch (DuplicateKeyException)
            {
                // کارگاهی هم‌زمان (مثلاً همین حالا ساخته شده) خودش پیش‌فرض‌هایش را گرفت
                unitOfWork.Reset();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                unitOfWork.Reset();
                logger.LogError(ex, "ساخت پیش‌فرض‌های کارگاه {TenantId} ناموفق بود؛ استارت بعدی دوباره امتحان می‌کند.",
                    tenantId);
            }
        }

        if (tenantIds.Count > 0)
            logger.LogInformation("پیش‌فرض‌های {Count} کارگاه بررسی شد ({Failed} خطا).", tenantIds.Count, failed);

        return failed;
    }

    /// <summary>ترتیب قطعی: Order و برای ترتیب برابر نام کلاس.</summary>
    private IEnumerable<ITenantSeedContributor> Ordered()
        => contributors
            .OrderBy(c => c.Order)
            .ThenBy(c => c.GetType().FullName, StringComparer.Ordinal);
}
