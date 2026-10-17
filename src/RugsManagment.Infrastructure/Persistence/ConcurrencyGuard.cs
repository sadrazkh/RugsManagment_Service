using Microsoft.EntityFrameworkCore;
using RugsManagment.Application.Abstractions.Persistence;

namespace RugsManagment.Infrastructure.Persistence;

/// <summary>
/// پیاده‌سازی <see cref="IConcurrencyGuard"/> روی ستون سیستمی <c>xmin</c> که EF به‌صورت shadow property
/// (<c>Property&lt;uint&gt;("xmin").IsRowVersion()</c>) می‌شناسد. همان الگوی <c>TenantPreferencesRepository</c>: مقدار «اصلی»
/// توکن هم‌زمانی برابر نسخه‌ای می‌شود که کاربر دیده بود، پس UPDATE با <c>WHERE xmin = expected</c> اجرا می‌شود.
/// </summary>
public sealed class ConcurrencyGuard(AppDbContext db) : IConcurrencyGuard
{
    private const string VersionProperty = "xmin";

    public uint VersionOf(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var entry = db.Entry(entity);
        if (entry.State is EntityState.Detached or EntityState.Added)
            return 0;
        if (entry.Metadata.FindProperty(VersionProperty) is null)
            return 0;

        return entry.Property(VersionProperty).CurrentValue is uint version ? version : 0;
    }

    public void Expect(object entity, uint expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var entry = db.Entry(entity);
        if (entry.State == EntityState.Detached)
            throw new InvalidOperationException($"{entity.GetType().Name} ردیابی نمی‌شود؛ نسخه فقط روی موجودیت بارگذاری‌شده مسلح می‌شود.");
        if (entry.Metadata.FindProperty(VersionProperty) is null)
            throw new InvalidOperationException($"{entity.GetType().Name} ستون نسخه (xmin) ندارد.");

        entry.Property(VersionProperty).OriginalValue = expectedVersion;
    }
}
