using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Infrastructure.Persistence.Repositories;

/// <summary>
/// شمارندهٔ اتمیک هر کارگاه با یک UPSERT در پستگرس.
///
/// چرا یک دستور: «خواندن بزرگ‌ترین مقدار و +۱» در دو درخواست هم‌زمان عدد تکراری می‌ساخت. اینجا
/// INSERT … ON CONFLICT DO UPDATE … RETURNING روی کلید اصلی (کارگاه، نام) قفل ردیفی می‌گیرد؛ درخواست
/// دوم پشت اولی صبر می‌کند و مقدارِ به‌روزشده را می‌بیند. اگر فراخواننده تراکنش باز داشته باشد،
/// همین دستور جزو آن است: با rollback عدد هم برمی‌گردد و شکافی نمی‌ماند.
/// </summary>
public sealed class TenantCounterRepository(AppDbContext db) : ITenantCounters
{
    /// <summary>هم‌اندازهٔ ستون Key در <c>TenantCounterConfiguration</c>.</summary>
    public const int MaxKeyLength = 40;

    /// <summary>کلیدهای SKU به شکل <c>sku:{yyyyMM}</c> هستند.</summary>
    private const string SkuKeyPrefix = "sku:";

    public async Task<long> NextBlockAsync(
        Guid tenantId, string key, int count = 1, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (key.Length > MaxKeyLength)
            throw new ArgumentException($"نام شمارنده حداکثر {MaxKeyLength} نویسه است: «{key}».", nameof(key));
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        // seed فقط وقتی معنا دارد که ردیف هنوز نیست؛ اگر دو درخواست هم‌زمان هر دو seed را حساب کنند،
        // یکی INSERT می‌کند و دیگری به شاخهٔ ON CONFLICT می‌رسد که seed را نادیده می‌گیرد — پس درست است.
        var seed = key.StartsWith(SkuKeyPrefix, StringComparison.Ordinal)
                   && !await CounterExistsAsync(tenantId, key, cancellationToken)
            ? await MaxSkuSequenceAsync(tenantId, key[SkuKeyPrefix.Length..], cancellationToken)
            : 0L;

        long firstValue = seed + count;
        var result = await db.Database
            .SqlQuery<long>($"""
                INSERT INTO "TenantCounters" ("TenantId", "Key", "Value")
                VALUES ({tenantId}, {key}, {firstValue})
                ON CONFLICT ("TenantId", "Key") DO UPDATE SET "Value" = "TenantCounters"."Value" + {count}
                RETURNING "Value"
                """)
            .ToListAsync(cancellationToken);

        return result[0];
    }

    private Task<bool> CounterExistsAsync(Guid tenantId, string key, CancellationToken cancellationToken)
        => db.Set<TenantCounter>()
            .AsNoTracking()
            .AnyAsync(c => c.TenantId == tenantId && c.Key == key, cancellationToken);

    /// <summary>
    /// بزرگ‌ترین شمارهٔ SKUهای موجودِ همان ماه (قالب <c>RUG-{yyyyMM}-NNNN</c>، همان قالب
    /// <c>RugRepository.GenerateNextSkuAsync</c>) تا شمارنده از دادهٔ قدیمی جلوتر شروع شود.
    /// IgnoreQueryFilters لازم است: فرشِ سطل‌زباله هنوز SKU خود را در ایندکس یکتا نگه داشته است.
    /// </summary>
    private async Task<long> MaxSkuSequenceAsync(Guid tenantId, string month, CancellationToken cancellationToken)
    {
        var prefix = $"RUG-{month}-";
        var skus = await db.Rugs
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.Sku.StartsWith(prefix))
            .Select(r => r.Sku)
            .ToListAsync(cancellationToken);

        // فرهنگ ثابت (Invariant): با فرهنگ fa عدد و علامت منفی ASCII نیستند
        return skus
            .Select(sku => long.TryParse(sku.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0L)
            .DefaultIfEmpty(0L)
            .Max();
    }
}
