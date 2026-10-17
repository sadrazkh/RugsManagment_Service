using RugsManagment.Application.DTOs.Rugs;

namespace RugsManagment.Application.Features.Rugs;

/// <summary>
/// «درز» تکمیل <see cref="RugDto"/> با داده‌ای که مال فاز دیگری است (spec I.3، K.1 «Seams»): محموله، نگله، ارزش دلاری خط و
/// جای «در نگله/صادر شده». فاز ۲ هیچ پیاده‌سازی‌ای ثبت نمی‌کند (آن عضوها null می‌مانند)؛ فاز محموله
/// <c>ShipmentRugDtoEnricher</c> را به‌صورت فایل تازه ثبت می‌کند و با یک پرس‌وجو بر اساس RugId همهٔ DTOها را پر می‌کند.
/// پس سرویس‌های فرش هرگز برای محموله ویرایش نمی‌شوند.
///
/// <para>قاعده: enricher فقط عضوهای خودش را عوض می‌کند (با <c>with</c>)، ترتیب و تعداد فهرست را نگه می‌دارد، و پول را
/// (اگر داشت) به همان قاعدهٔ <c>IMoneyVisibility</c> پنهان می‌کند. دلار گمرکی پول داخلی نیست و پنهان نمی‌شود (A.5).</para>
/// </summary>
public interface IRugDtoEnricher
{
    /// <summary>DTOها را تکمیل می‌کند و فهرستی با همان ترتیب و همان تعداد برمی‌گرداند.</summary>
    Task<IReadOnlyList<RugDto>> EnrichAsync(IReadOnlyList<RugDto> rugs, CancellationToken cancellationToken = default);
}

/// <summary>اجرای همهٔ enricherهای ثبت‌شده به ترتیب ثبت.</summary>
public static class RugDtoEnrichment
{
    /// <summary>فهرست DTOها از همهٔ enricherها می‌گذرد.</summary>
    public static async Task<IReadOnlyList<RugDto>> EnrichAsync(
        this IEnumerable<IRugDtoEnricher> enrichers, IReadOnlyList<RugDto> rugs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(enrichers);
        ArgumentNullException.ThrowIfNull(rugs);
        var current = rugs;
        foreach (var enricher in enrichers)
        {
            if (current.Count == 0) break;
            current = await enricher.EnrichAsync(current, cancellationToken);
        }

        return current;
    }

    /// <summary>یک DTO از همهٔ enricherها می‌گذرد.</summary>
    public static async Task<RugDto> EnrichAsync(
        this IEnumerable<IRugDtoEnricher> enrichers, RugDto rug, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rug);
        var result = await enrichers.EnrichAsync([rug], cancellationToken);
        return result.Count == 1 ? result[0] : rug;
    }
}
