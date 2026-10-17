using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.Mapping;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Rugs;

/// <summary>ساختن <see cref="RugDto"/> کامل برای بینندهٔ جاری.</summary>
public interface IRugDtoBuilder
{
    /// <summary>
    /// فرش ردیابی‌شده (معمولاً از <c>GetWithWorkflowAsync</c> یا همان نمونه‌ای که همین حالا ذخیره شد) به DTO: عضوهای فاز ۲
    /// (شماره، تعداد، اندازهٔ اول و نهایی، متراژها، ورودی، نسخه، جا، سطرهای خرید) پر و پول برای غیر Business پنهان.
    /// </summary>
    Task<RugDto> BuildAsync(Rug rug, CancellationToken cancellationToken = default);

    /// <summary>همان <see cref="BuildAsync"/> برای چند فرش یک کارگاه، با یک پرس‌وجو برای همهٔ سطرهای خرید.</summary>
    Task<IReadOnlyList<RugDto>> BuildManyAsync(IReadOnlyList<Rug> rugs, CancellationToken cancellationToken = default);

    /// <summary>سطرهای خرید یک فرش از دیتابیس (مبلغ‌ها همان که پاس نوشته)، به ترتیب نمایش.</summary>
    Task<IReadOnlyList<PurchaseLineDto>> PurchaseLinesAsync(Guid tenantId, Guid rugId, CancellationToken cancellationToken = default);
}

/// <summary>
/// یک جا برای ساختن DTO فرش در همهٔ مسیرهای فاز ۲ (جزئیات، PUT، اندازهٔ نهایی، سطرهای خرید، برش).
///
/// چرا جدا از <c>EntityMappers</c>: آن نگاشت ایستا است و به نسخه (xmin، فقط از <see cref="IConcurrencyGuard"/>) و سطرهای
/// خرید تازه (که پاس سازگاری با SQL می‌نویسد) راهی ندارد. این‌جا اول همان نگاشت پایه صدا زده می‌شود و بعد عضوهای فاز ۲ از
/// خودِ موجودیت پر می‌شوند — مقدارهایی که هر دو از یک منبع می‌خوانند، پس پر کردن دوباره همان عدد را می‌دهد. سطرهای خرید
/// همیشه با یک پرس‌وجوی بی‌ردیابی خوانده می‌شوند تا مبلغ‌هایی که پاس همین الان نوشته (و موجودیت ردیابی‌شده هنوز ندارد)
/// در پاسخ همین درخواست باشند (D.4 «یک درخواست، عددهای تازه»).
///
/// <para>پول: نگاشت پایه با <see cref="IMoneyVisibility"/> قیمت خرید، قیمت هدف، خلاصهٔ هزینه و مبلغ مراحل را برای
/// غیر Business پنهان می‌کند؛ این‌جا سطرهای خرید هم (A.5). <see cref="RugDto.PurchaseCost"/> از فاز ۲ جمع مبلغ سطرهای
/// خرید است، نه ستون منسوخ (B.4).</para>
/// </summary>
public sealed class RugDtoBuilder(
    IWorkflowEngine workflowEngine,
    IMoneyVisibility money,
    IConcurrencyGuard concurrency,
    IRepository<RugPurchaseLine> purchaseLines,
    IEnumerable<IRugDtoEnricher> enrichers) : IRugDtoBuilder
{
    public async Task<RugDto> BuildAsync(Rug rug, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rug);
        var lines = money.CanSeeMoney
            ? await PurchaseLinesAsync(rug.TenantId, rug.Id, cancellationToken)
            : null;
        // پنهان‌سازی دوباره بعد از enricherها: فازهای بعد (محموله، هزینه‌ها) عضو تازه پر می‌کنند و نباید هر کدام به یاد داشته
        // باشد که برای انباردار پول نفرستد (A.5)؛ برای Business همان شیء برمی‌گردد
        return money.Redact(await enrichers.EnrichAsync(Complete(rug, lines), cancellationToken));
    }

    public async Task<IReadOnlyList<RugDto>> BuildManyAsync(
        IReadOnlyList<Rug> rugs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rugs);
        if (rugs.Count == 0) return [];

        ILookup<Guid, PurchaseLineDto>? byRug = null;
        if (money.CanSeeMoney)
        {
            var tenantId = rugs[0].TenantId;
            var ids = rugs.Select(r => r.Id).ToList();
            var all = await purchaseLines.ListAsync(l => l.TenantId == tenantId && ids.Contains(l.RugId), cancellationToken);
            byRug = Order(all).ToLookup(l => l.RugId, ToDto);
        }

        var completed = rugs.Select(r => Complete(r, byRug?[r.Id].ToList())).ToList();
        return money.Redact(await enrichers.EnrichAsync(completed, cancellationToken));
    }

    private RugDto Complete(Rug rug, IReadOnlyList<PurchaseLineDto>? lines)
    {
        var dto = rug.ToDto(workflowEngine, money);
        return dto with
        {
            AreaSquareMeters = rug.AreaM2,
            // بی سطر null (نه صفر): فرم قدیمی null را «دست نزن» می‌خواند و صفر را «یک سطر صفر بساز»
            PurchaseCost = lines is { Count: > 0 } ? lines.Sum(l => l.Amount) : null,
            SerialNo = rug.SerialNo,
            Quantity = rug.Quantity,
            IntakeLength = rug.IntakeLength,
            IntakeWidth = rug.IntakeWidth,
            FinalLength = rug.FinalLength,
            FinalWidth = rug.FinalWidth,
            TotalAreaM2 = rug.TotalAreaM2,
            Location = LocationOf(rug, dto.Location),
            Version = concurrency.VersionOf(rug),
            PurchaseLines = lines,
            IntakeAreaM2 = rug.IntakeAreaM2,
            FinalMeasuredAt = rug.FinalMeasuredAt,
            BatchNumber = rug.Batch?.Number is > 0 ? rug.Batch.Number : null,
            SellerName = string.IsNullOrWhiteSpace(rug.Batch?.SellerName) ? null : rug.Batch.SellerName
        };
    }

    public async Task<IReadOnlyList<PurchaseLineDto>> PurchaseLinesAsync(
        Guid tenantId, Guid rugId, CancellationToken cancellationToken = default)
    {
        var lines = await purchaseLines.ListAsync(l => l.RugId == rugId && l.TenantId == tenantId, cancellationToken);
        return Order(lines).Select(ToDto).ToList();
    }

    /// <summary>ترتیب نمایش سطرها: SortOrder، بعد زمان ساخت، بعد شناسه (پایدار).</summary>
    private static IEnumerable<RugPurchaseLine> Order(IEnumerable<RugPurchaseLine> lines)
        => lines.OrderBy(l => l.SortOrder).ThenBy(l => l.CreatedAt).ThenBy(l => l.Id);

    /// <summary>یک سطر خرید به DTO.</summary>
    public static PurchaseLineDto ToDto(RugPurchaseLine line) => new(
        line.Id, line.CategoryId, line.Label, line.Basis, line.Rate, line.Amount, line.SortOrder, line.Notes);

    /// <summary>
    /// جای فرش (C.10) تا جایی که فاز ۲ می‌بیند: سطل زباله، فروخته‌شده، وگرنه همان که نگاشت/enricher گفته (نگله و صادرشده
    /// را فاز محموله پر می‌کند).
    /// </summary>
    public static RugLocation LocationOf(Rug rug, RugLocation mapped)
    {
        ArgumentNullException.ThrowIfNull(rug);
        if (rug.DeletedAt is not null) return RugLocation.Trashed;
        if (rug.Sale is not null) return RugLocation.Sold;
        return mapped;
    }
}
