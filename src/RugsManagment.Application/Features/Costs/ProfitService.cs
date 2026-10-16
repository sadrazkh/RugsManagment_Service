using System.Globalization;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Costs;
using RugsManagment.Application.Features.Platform;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Costing;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Costs;

/// <summary>
/// «حساب محموله» (spec D.6، H.1، I.2: <c>GET /api/shipments/{id}/profit</c>، <c>PUT /api/shipments/{id}/revenue</c>، Business):
/// جمع بهای فرش‌های محموله، قیمت تمام‌شدهٔ هر متر، درآمد به ارز کارگاه و سود = درآمد − Σ بها.
/// </summary>
public interface IProfitService
{
    Task<ShipmentProfitDto> GetAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// «فروش این محموله: [__] دلار × نرخ [__] = … تومان». با نسخهٔ محموله؛ روی محمولهٔ قطعی هم مجاز است (درآمد قفل نیست، C.8).
    /// سهم درآمد هر فرش در همین ذخیره (پاس سازگاری، <c>RevenueShareStage</c>) نوشته می‌شود.
    /// </summary>
    Task<ShipmentProfitDto> SetRevenueAsync(Guid tenantId, Guid shipmentId, SaveRevenueRequest request, CancellationToken cancellationToken = default);

    /// <summary>سود هر فرش (سهم درآمد − بها)، به ترتیب شماره.</summary>
    Task<IReadOnlyList<RugProfitDto>> RugProfitsAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// پیاده‌سازی «حساب محموله». جمع‌ها از <c>RugCostTotal</c> (همان که پاس نوشته)، متراژ و دلار از ارزش خط‌ها؛ درآمد پایه با
/// همان قاعدهٔ «کل» (ROUND(مبلغ × نرخ، s) یک بار، D.3) و سهم هر فرش با همان تقسیم منبع سهم‌ها (<see cref="CostAllocator.RevenueShares"/>).
/// </summary>
public sealed class ProfitService(
    ICostEntryRepository entries,
    IShipmentRepository shipments,
    IConcurrencyGuard concurrency,
    IUnitOfWork unitOfWork,
    IAuditLog audit,
    TimeProvider? time = null) : IProfitService
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public async Task<ShipmentProfitDto> GetAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var data = await entries.ShipmentMoneyAsync(tenantId, shipmentId, cancellationToken)
                   ?? throw new KeyNotFoundException("محموله یافت نشد.");
        var settings = await entries.TenantCostSettingsAsync(tenantId, cancellationToken);
        return Build(data, settings);
    }

    public async Task<IReadOnlyList<RugProfitDto>> RugProfitsAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var data = await entries.ShipmentMoneyAsync(tenantId, shipmentId, cancellationToken)
                   ?? throw new KeyNotFoundException("محموله یافت نشد.");
        var settings = await entries.TenantCostSettingsAsync(tenantId, cancellationToken);
        var shares = Shares(data, settings);
        return data.Lines
            .OrderBy(l => l.SerialSort is null ? 1 : 0).ThenBy(l => l.SerialSort).ThenBy(l => l.SerialNo, StringComparer.Ordinal)
            .Select(l =>
            {
                decimal? share = shares is null ? null : shares.GetValueOrDefault(l.RugId);
                return new RugProfitDto(l.RugId, l.SerialNo ?? l.Sku, l.Quantity, l.Total, share, share - l.Total);
            })
            .ToList();
    }

    public async Task<ShipmentProfitDto> SetRevenueAsync(
        Guid tenantId, Guid shipmentId, SaveRevenueRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var shipment = await shipments.GetTrackedAsync(tenantId, shipmentId, cancellationToken)
                       ?? throw new KeyNotFoundException("محموله یافت نشد.");
        concurrency.Expect(shipment, request.Version);
        var settings = await entries.TenantCostSettingsAsync(tenantId, cancellationToken);

        if (request.Amount is { } amount)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
            if (!Enum.IsDefined(request.Currency))
                errors["currency"] = [Messages.CostCurrencyInvalid.Text];
            var scale = request.Currency == CurrencyUnit.Usd ? AreaMath.UsdDecimals : settings.MoneyScale;
            if (amount < 0)
                errors["amount"] = [Messages.RevenueNegative.Text];
            else if (amount > CostEntryService.MaxAmount)
                errors["amount"] = [Messages.CostTooLarge.Text];
            else if (!Num.HasAtMostDecimals(amount, scale))
                errors["amount"] = [Messages.CostDecimals.Format(("currency", CostEntryService.CurrencyLabel(request.Currency)), ("n", scale))];

            decimal? fx = null;
            if (request.Currency == CurrencyUnit.Usd)
            {
                if (request.Fx is not { } rate || rate <= 0)
                    errors["fx"] = [Messages.CostFxRequired.Text];
                else if (rate > CostEntryService.MaxFx || !Num.HasAtMostDecimals(rate, 6))
                    errors["fx"] = [Messages.CostTooLarge.Text];
                else
                    fx = rate;
            }

            if (errors.Count > 0)
                throw new AppValidationException(Messages.ValidationFailed.Text, errors);

            var baseAmount = CostAllocator.BaseOfTotal(amount, request.Currency, settings.Currency, fx, settings.MoneyScale);
            if (baseAmount > CostEntryService.MaxAmount)
                throw AppValidationException.ForField("amount", Messages.CostTooLarge.Text);

            shipment.RevenueAmount = amount;
            shipment.RevenueCurrency = request.Currency;
            shipment.RevenueFx = fx;
        }
        else
        {
            shipment.RevenueAmount = null;
            shipment.RevenueCurrency = null;
            shipment.RevenueFx = null;
        }

        // نسخه همیشه سنجیده شود، حتی وقتی چیزی عوض نشده
        shipment.UpdatedAt = _time.GetUtcNow();
        audit.Record(AuditAction.RevenueChanged, "Shipment", shipment.Id,
            $"فروش محمولهٔ {PersianText.ToPersianDigits(shipment.Number.ToString(CultureInfo.InvariantCulture))} ثبت شد.",
            shipment.Title, new { number = shipment.Number });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(tenantId, shipmentId, cancellationToken);
    }

    /// <summary>حساب محموله از ردیف‌ها (خالص؛ همان که آزمون‌ها می‌سنجند).</summary>
    public static ShipmentProfitDto Build(ShipmentMoneyData data, CostTenantSettings settings)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(settings);
        var lines = data.Lines;
        var total = lines.Sum(l => l.Total);
        var area = lines.Sum(l => l.LineAreaM2);
        var revenue = RevenueBase(data, settings);
        return new ShipmentProfitDto(
            lines.Sum(l => l.Quantity),
            area,
            lines.Sum(l => l.LineUsd ?? 0m),
            lines.Sum(l => l.Purchase),
            lines.Sum(l => l.Steps),
            lines.Sum(l => l.Expenses),
            total,
            RugCostMath.CostPerM2(total, area, settings.MoneyScale),
            revenue,
            revenue - total,
            data.Status == ShipmentStatus.Finalized,
            data.RevenueAmount,
            data.RevenueCurrency,
            data.RevenueFx,
            data.Version,
            data.UnallocatedEntries);
    }

    /// <summary>درآمد به ارز کارگاه؛ null بی درآمد (یا دلار بی نرخ).</summary>
    public static decimal? RevenueBase(ShipmentMoneyData data, CostTenantSettings settings)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(settings);
        return CostAllocator.RevenueBase(data.RevenueAmount, data.RevenueCurrency, data.RevenueFx, settings.Currency, settings.MoneyScale);
    }

    private static IReadOnlyDictionary<Guid, decimal>? Shares(ShipmentMoneyData data, CostTenantSettings settings)
    {
        var revenue = RevenueBase(data, settings);
        if (revenue is not { } total) return null;
        return CostAllocator.RevenueShares(total, settings.MoneyScale,
            data.Lines.Select(l => new RevenueLine(l.RugId, l.LineUsd, l.SerialSort)).ToList());
    }
}

/// <summary>
/// کارت پولی خانه (A.4، درز فاز سکو <see cref="IHomeMoneySource"/>): «قیمت تمام‌شدهٔ هر متر» اولین محمولهٔ باز و شمار
/// هزینه‌های «تقسیم‌نشده». فقط برای Business صدا زده می‌شود.
/// </summary>
public sealed class CostHomeMoneySource(ICostEntryRepository entries) : IHomeMoneySource
{
    public async Task<HomeMoney> GetAsync(Guid tenantId, Guid? firstOpenShipmentId, CancellationToken cancellationToken = default)
    {
        decimal? perM2 = null;
        if (firstOpenShipmentId is { } shipmentId
            && await entries.ShipmentMoneyAsync(tenantId, shipmentId, cancellationToken) is { } data)
        {
            var settings = await entries.TenantCostSettingsAsync(tenantId, cancellationToken);
            perM2 = ProfitService.Build(data, settings).CostPerM2;
        }

        var unallocated = await entries.CountNotFullyAllocatedAsync(tenantId, cancellationToken);
        return new HomeMoney(perM2, unallocated);
    }
}
