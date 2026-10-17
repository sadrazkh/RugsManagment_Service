namespace RugsManagment.Application.Features.Platform;

/// <summary>کارت «انبار» خانه: تعداد تخته و متراژ.</summary>
/// <param name="Pieces">تعداد تخته‌ها (Σ تعداد؛ تا فاز دسته هر فرش یک تخته است)</param>
/// <param name="AreaM2">متراژ، با قاعدهٔ دسته: هر تخته ROUND(L×W, 2) و بعد × تعداد</param>
public sealed record HomeStock(int Pieces, decimal AreaM2)
{
    public static readonly HomeStock Empty = new(0, 0m);
}

/// <summary>
/// «انبار» برای صفحهٔ خانه — درزی که فاز محموله تعریفش را دقیق‌تر می‌کند.
///
/// در این فاز انبار یعنی هر فرشِ فعالی که نه فروخته شده و نه صادر (هنوز محموله‌ای وجود ندارد).
/// فاز محموله که «در نگله» را هم می‌شناسد، پیاده‌سازی خودش را با <c>services.Replace(...)</c> می‌گذارد
/// بی‌آنکه سرویس خلاصهٔ خانه را ویرایش کند. کوئری همیشه به <c>tenantId</c> محدود است و پول ندارد.
/// </summary>
public interface IHomeStockSource
{
    Task<HomeStock> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
