using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.Features.Rugs;
using RugsManagment.Domain.Costing;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Features.Costs;

/// <summary>
/// سهم دفتر هزینه در «برش دسته» (spec D.9): فرش تازه به هر ثبتی که فرش مبدأ را صریح هدف گرفته (دامنهٔ «انتخاب» و عکس خرید
/// یکجای ورودی) می‌پیوندد — ردیف‌های <c>CostEntryTarget</c> تکثیر می‌شوند — و هر مبلغ ثابتِ فرش مبدأ به نسبت تعداد با «بزرگ‌ترین
/// باقی‌مانده» بین دو بخش تقسیم می‌شود. عضویت نگله، محموله و ورودیِ زنده خودبه‌خود دنبال می‌شود.
///
/// <para>چرا decorator: <c>RugSplitService</c> مال فاز ۲ است و برش را بی ذخیره در UnitOfWork می‌گذارد؛ این لایه بعد از آن و در
/// همان UnitOfWork هدف‌ها و مبلغ‌های ثابت را می‌افزاید، پس پاس سازگاریِ همان ذخیره همه را با هم تقسیم می‌کند.</para>
/// </summary>
public sealed class CostSplitDecorator(IRugSplitService inner, ICostSplitRepository costs) : IRugSplitService
{
    public async Task<Rug> SplitAsync(
        Rug original, SplitRugRequest request, RugSplitContext context, CancellationToken cancellationToken = default)
    {
        var created = await inner.SplitAsync(original, request, context, cancellationToken);

        var targets = await costs.RugTargetsAsync(context.TenantId, original.Id, cancellationToken);
        foreach (var target in targets)
        {
            await costs.AddTargetAsync(new CostEntryTarget
            {
                TenantId = context.TenantId, EntryId = target.EntryId, RugId = created.Id, CreatedAt = context.Now
            }, cancellationToken);
        }

        var members = new[]
        {
            new LrMember(original.Id, original.Quantity, original.SerialSort),
            new LrMember(created.Id, created.Quantity, created.SerialSort)
        };
        foreach (var pin in await costs.PinsTrackedAsync(context.TenantId, original.Id, cancellationToken))
        {
            var shares = LargestRemainder.Split(pin.Amount, context.MoneyScale, members).Shares;
            pin.Amount = shares[0];
            await costs.AddPinAsync(new CostEntryPin
            {
                TenantId = context.TenantId, EntryId = pin.EntryId, RugId = created.Id, Amount = shares[1], CreatedAt = context.Now
            }, cancellationToken);
        }

        return created;
    }
}

/// <summary>خواندن و افزودن هدف‌ها و مبلغ‌های ثابت برای برش دسته (پیاده‌سازی: <c>CostEntryRepository</c>).</summary>
public interface ICostSplitRepository
{
    /// <summary>هدف‌های فرشیِ ثبت‌های حذف‌نشده که این فرش را صریح دارند.</summary>
    Task<IReadOnlyList<CostEntryTarget>> RugTargetsAsync(Guid tenantId, Guid rugId, CancellationToken cancellationToken = default);

    /// <summary>مبلغ‌های ثابتِ این فرش (ردیابی‌شده).</summary>
    Task<IReadOnlyList<CostEntryPin>> PinsTrackedAsync(Guid tenantId, Guid rugId, CancellationToken cancellationToken = default);

    Task AddTargetAsync(CostEntryTarget target, CancellationToken cancellationToken = default);

    Task AddPinAsync(CostEntryPin pin, CancellationToken cancellationToken = default);
}
