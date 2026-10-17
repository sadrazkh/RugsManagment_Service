using System.Globalization;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Features.Rugs;

/// <summary>کد انبار (SKU) فرش تازه.</summary>
public interface IRugSkuGenerator
{
    /// <summary>
    /// <paramref name="count"/> کد پشت‌سرهم برای ماه جاری (UTC، میلادی — همان قالب همیشگی <c>RUG-202609-0001</c>)،
    /// به ترتیب. پیشوند از ترجیحات کارگاه (<see cref="TenantPreferences.SkuPrefix"/>).
    /// </summary>
    Task<IReadOnlyList<string>> NextAsync(Guid tenantId, int count = 1, CancellationToken cancellationToken = default);
}

/// <summary>
/// کد انبار از شمارندهٔ اتمیک کارگاه (<see cref="ITenantCounters"/>، کلید <c>sku:{yyyyMM}</c>) — یک UPSERT، بی تلاش دوباره.
///
/// چرا نه «بزرگ‌ترین کد + ۱» مثل قبل: دو ثبت هم‌زمان یک عدد می‌گرفتند و دومی با خطای کلید تکراری برمی‌گشت؛ حلقهٔ
/// تلاش دوباره هم کار را بدتر می‌کرد (فرشِ شکست‌خورده در DbContext می‌ماند). شمارنده عدد را در همان تراکنش رزرو
/// می‌کند؛ با rollback عدد هم برمی‌گردد و ایندکس یکتای (کارگاه، SKU) فقط پشتیبان است. اولین استفادهٔ کلید هر ماه از
/// بزرگ‌ترین SKU همان ماه (حتی سطل زباله) ادامه می‌دهد.
/// </summary>
public sealed class RugSkuGenerator(
    ITenantCounters counters,
    ITenantPreferencesRepository preferences) : IRugSkuGenerator
{
    public async Task<IReadOnlyList<string>> NextAsync(
        Guid tenantId, int count = 1, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        var prefix = (await preferences.GetAsync(tenantId, cancellationToken))?.Preferences.SkuPrefix;
        if (string.IsNullOrWhiteSpace(prefix)) prefix = TenantPreferences.DefaultSkuPrefix;

        var month = DateTime.UtcNow.ToString("yyyyMM", CultureInfo.InvariantCulture);
        var last = await counters.NextBlockAsync(tenantId, "sku:" + month, count, cancellationToken);

        var skus = new string[count];
        for (var i = 0; i < count; i++)
            skus[i] = Format(prefix, month, last - count + 1 + i);
        return skus;
    }

    /// <summary>«RUG-202609-0001» — حداقل چهار رقم، بیشتر اگر ماه از ۹۹۹۹ فرش گذشت.</summary>
    public static string Format(string prefix, string month, long sequence)
        => $"{prefix}-{month}-{sequence.ToString("D4", CultureInfo.InvariantCulture)}";
}
