using RugsManagment.Application.Ledger;

namespace RugsManagment.Application.Features.Shipments.Ledger;

/// <summary>
/// خط هر فرش (کدام محموله و نگله) برای یک پاس سازگاری، یک بار. هم <see cref="ShipmentImpactContributor"/> (گام ۴) و هم
/// <see cref="ShipmentLockGuard"/> (گام ۵) خط فرش‌های عوض‌شده را لازم دارند و هر دو پیش از flush اجرا می‌شوند، پس وضعیت
/// دیتابیس برایشان یکی است؛ بی این حافظه یک «اندازه» دو بار همان پرس‌وجو را می‌زد (D.4 «Performance»: اندازه زیر ۲۰۰
/// میلی‌ثانیه). حافظه در <see cref="LedgerContext.Items"/> است و با پاس می‌رود؛ هر flush پاس تازه و حافظهٔ تازه دارد.
/// </summary>
public static class RugLineCache
{
    /// <summary>
    /// خط‌های این فرش‌ها (فرش بی‌خط در نتیجه نیست). فقط فرش‌هایی که در این پاس هنوز پرسیده نشده‌اند به دیتابیس می‌روند.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, LinePlacement>> LinesOfRugsAsync(
        LedgerContext context, IShipmentLedgerRepository repository, IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(rugIds);

        var cache = Of(context);
        var missing = rugIds.Where(id => !cache.ContainsKey(id)).Distinct().ToList();
        if (missing.Count > 0)
        {
            var found = await repository.LinesAsync(context.TenantId, missing, null, null, cancellationToken);
            foreach (var id in missing)
                cache[id] = null;
            Remember(context, found);
        }

        var result = new Dictionary<Guid, LinePlacement>();
        foreach (var id in rugIds)
        {
            if (cache.TryGetValue(id, out var line) && line is not null)
                result[id] = line;
        }

        return result;
    }

    /// <summary>
    /// خط‌هایی که با پرس‌وجوی دیگری (مثلاً همهٔ خط‌های یک محمولهٔ «باز کردن دوباره») خوانده شده‌اند هم به حافظه می‌روند؛
    /// چون هر فرش حداکثر یک خط دارد (ایندکس یکتا)، خط پیداشده پاسخ قطعیِ همان فرش است.
    /// </summary>
    public static void Remember(LedgerContext context, IEnumerable<LinePlacement> lines)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(lines);
        var cache = Of(context);
        foreach (var line in lines)
            cache[line.RugId] = line;
    }

    private static Dictionary<Guid, LinePlacement?> Of(LedgerContext context)
    {
        if (context.Items.TryGetValue(typeof(RugLineCache), out var existing) && existing is Dictionary<Guid, LinePlacement?> cache)
            return cache;

        cache = [];
        context.Items[typeof(RugLineCache)] = cache;
        return cache;
    }
}
