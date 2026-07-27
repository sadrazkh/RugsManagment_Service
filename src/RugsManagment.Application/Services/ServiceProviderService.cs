using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Providers;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Services;

/// <summary>
/// مدیریت طرف‌های خدمات: مشخصات، نرخ‌های توافقی، صورت‌حساب و تسویه.
/// </summary>
public interface IServiceProviderService
{
    Task<IReadOnlyList<ServiceProviderDetailDto>> ListAsync(Guid tenantId, CancellationToken ct = default);
    Task<ServiceProviderDetailDto?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<ServiceProviderDetailDto> CreateAsync(Guid tenantId, SaveServiceProviderRequest request, CancellationToken ct = default);
    Task<ServiceProviderDetailDto> UpdateAsync(Guid tenantId, Guid id, SaveServiceProviderRequest request, CancellationToken ct = default);

    /// <summary>حذف کامل — فقط اگر هیچ سابقهٔ کار یا پرداختی نداشته باشد.</summary>
    Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ProviderBalanceDto>> ListBalancesAsync(Guid tenantId, CancellationToken ct = default);
    Task<ProviderStatementDto> GetStatementAsync(Guid tenantId, Guid providerId, CancellationToken ct = default);

    Task<ProviderPaymentDto> AddPaymentAsync(Guid tenantId, Guid providerId, CreateProviderPaymentRequest request, CancellationToken ct = default);
    Task DeletePaymentAsync(Guid tenantId, Guid providerId, Guid paymentId, CancellationToken ct = default);

    /// <summary>
    /// نرخ توافقی یک طرف برای یک نوع مرحله — هنگام ثبت مرحله خودکار اعمال می‌شود.
    /// null یعنی برای این ترکیب نرخی تعریف نشده و باید از نرخ پیش‌فرض نوع مرحله استفاده شود.
    /// </summary>
    Task<ProviderRateDto?> FindRateAsync(Guid tenantId, Guid providerId, Guid stepTypeId, CancellationToken ct = default);
}

public sealed class ServiceProviderService(
    IServiceProviderRepository providers,
    IRepository<ServiceProviderRate> rates,
    IRepository<ProviderPayment> payments,
    IUnitOfWork unitOfWork) : IServiceProviderService
{
    public async Task<IReadOnlyList<ServiceProviderDetailDto>> ListAsync(Guid tenantId, CancellationToken ct = default)
        => (await providers.ListAllWithRatesAsync(tenantId, ct)).Select(ToDto).ToList();

    public async Task<ServiceProviderDetailDto?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var provider = await providers.GetWithRatesAsync(id, tenantId, ct);
        return provider is null ? null : ToDto(provider);
    }

    public async Task<ServiceProviderDetailDto> CreateAsync(
        Guid tenantId, SaveServiceProviderRequest request, CancellationToken ct = default)
    {
        Validate(request);

        var provider = new ServiceProvider
        {
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Specialty = Clean(request.Specialty),
            Phone = Clean(request.Phone),
            Address = Clean(request.Address),
            Notes = Clean(request.Notes),
            IsActive = request.IsActive
        };

        await providers.AddAsync(provider, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await ReplaceRatesAsync(provider.Id, [], request.Rates, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return await RequireAsync(tenantId, provider.Id, ct);
    }

    public async Task<ServiceProviderDetailDto> UpdateAsync(
        Guid tenantId, Guid id, SaveServiceProviderRequest request, CancellationToken ct = default)
    {
        Validate(request);

        var provider = await providers.GetWithRatesAsync(id, tenantId, ct)
            ?? throw new KeyNotFoundException("طرف خدمات یافت نشد.");

        provider.Name = request.Name.Trim();
        provider.Specialty = Clean(request.Specialty);
        provider.Phone = Clean(request.Phone);
        provider.Address = Clean(request.Address);
        provider.Notes = Clean(request.Notes);
        provider.IsActive = request.IsActive;
        provider.UpdatedAt = DateTimeOffset.UtcNow;

        providers.Update(provider);
        await ReplaceRatesAsync(provider.Id, provider.Rates.ToList(), request.Rates, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return await RequireAsync(tenantId, id, ct);
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var provider = await providers.GetWithRatesAsync(id, tenantId, ct)
            ?? throw new KeyNotFoundException("طرف خدمات یافت نشد.");

        // حذف طرفی که سابقهٔ کار دارد یعنی از بین بردن تاریخچهٔ مالی — به‌جایش غیرفعالش کنید
        if (await providers.HasWorkHistoryAsync(id, ct))
            throw new InvalidOperationException(
                "این طرف سابقهٔ کار یا پرداخت دارد و حذف نمی‌شود. به‌جای حذف، آن را غیرفعال کنید.");

        providers.Remove(provider);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public Task<IReadOnlyList<ProviderBalanceDto>> ListBalancesAsync(Guid tenantId, CancellationToken ct = default)
        => providers.ListBalancesAsync(tenantId, ct);

    public async Task<ProviderStatementDto> GetStatementAsync(
        Guid tenantId, Guid providerId, CancellationToken ct = default)
    {
        var balances = await providers.ListBalancesAsync(tenantId, ct);
        var balance = balances.FirstOrDefault(b => b.ProviderId == providerId)
            ?? throw new KeyNotFoundException("طرف خدمات یافت نشد.");

        var work = await providers.ListWorkAsync(tenantId, providerId, ct);

        var paymentRows = await payments.ListAsync(
            p => p.TenantId == tenantId && p.ServiceProviderId == providerId, ct);

        var paymentDtos = paymentRows
            .OrderByDescending(p => p.PaidAt)
            .Select(p => new ProviderPaymentDto(p.Id, p.Amount, p.PaidAt, p.Reference, p.Notes))
            .ToList();

        return new ProviderStatementDto(balance, work, paymentDtos);
    }

    public async Task<ProviderPaymentDto> AddPaymentAsync(
        Guid tenantId, Guid providerId, CreateProviderPaymentRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0)
            throw new InvalidOperationException("مبلغ پرداخت باید بزرگ‌تر از صفر باشد.");

        _ = await providers.GetWithRatesAsync(providerId, tenantId, ct)
            ?? throw new KeyNotFoundException("طرف خدمات یافت نشد.");

        var payment = new ProviderPayment
        {
            TenantId = tenantId,
            ServiceProviderId = providerId,
            Amount = request.Amount,
            // تاریخ پرداخت ممکن است با تاریخ ثبت فرق کند (پرداخت دیروز، ثبت امروز)
            PaidAt = (request.PaidAt ?? DateTimeOffset.UtcNow).ToUniversalTime(),
            Reference = Clean(request.Reference),
            Notes = Clean(request.Notes)
        };

        await payments.AddAsync(payment, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new ProviderPaymentDto(payment.Id, payment.Amount, payment.PaidAt, payment.Reference, payment.Notes);
    }

    public async Task DeletePaymentAsync(
        Guid tenantId, Guid providerId, Guid paymentId, CancellationToken ct = default)
    {
        var matches = await payments.ListAsync(
            p => p.Id == paymentId && p.TenantId == tenantId && p.ServiceProviderId == providerId, ct);

        var payment = matches.FirstOrDefault()
            ?? throw new KeyNotFoundException("پرداخت یافت نشد.");

        payments.Remove(payment);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<ProviderRateDto?> FindRateAsync(
        Guid tenantId, Guid providerId, Guid stepTypeId, CancellationToken ct = default)
    {
        var provider = await providers.GetWithRatesAsync(providerId, tenantId, ct);
        var rate = provider?.Rates.FirstOrDefault(r => r.ProcessStepTypeId == stepTypeId);
        return rate is null ? null : ToDto(rate);
    }

    // ─────────────────────────────────────────────────────────

    private static void Validate(SaveServiceProviderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("نام طرف خدمات الزامی است.");

        foreach (var rate in request.Rates ?? [])
        {
            if (rate.UnitRate < 0)
                throw new InvalidOperationException("نرخ نمی‌تواند منفی باشد.");
        }

        var duplicate = (request.Rates ?? [])
            .GroupBy(r => r.ProcessStepTypeId)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)
            throw new InvalidOperationException("برای هر نوع مرحله فقط یک نرخ می‌توان تعریف کرد.");
    }

    /// <summary>
    /// نرخ‌ها را با فهرست جدید هماهنگ می‌کند: موجودها به‌روز، حذف‌شده‌ها پاک، تازه‌ها اضافه.
    /// جایگزینی کامل (حذف همه و درج دوباره) عمداً انجام نمی‌شود تا شناسهٔ ردیف‌ها ثابت بماند.
    /// </summary>
    private async Task ReplaceRatesAsync(
        Guid providerId,
        IReadOnlyList<ServiceProviderRate> existing,
        IReadOnlyList<SaveProviderRateRequest>? requested,
        CancellationToken ct)
    {
        var wanted = requested ?? [];

        foreach (var current in existing)
        {
            var match = wanted.FirstOrDefault(r => r.ProcessStepTypeId == current.ProcessStepTypeId);
            if (match is null)
            {
                rates.Remove(current);
                continue;
            }

            current.PricingModel = match.PricingModel;
            current.UnitRate = match.UnitRate;
            current.Notes = Clean(match.Notes);
            current.UpdatedAt = DateTimeOffset.UtcNow;
            rates.Update(current);
        }

        var existingTypeIds = existing.Select(r => r.ProcessStepTypeId).ToHashSet();

        foreach (var added in wanted.Where(r => !existingTypeIds.Contains(r.ProcessStepTypeId)))
        {
            await rates.AddAsync(new ServiceProviderRate
            {
                ServiceProviderId = providerId,
                ProcessStepTypeId = added.ProcessStepTypeId,
                PricingModel = added.PricingModel,
                UnitRate = added.UnitRate,
                Notes = Clean(added.Notes)
            }, ct);
        }
    }

    private async Task<ServiceProviderDetailDto> RequireAsync(Guid tenantId, Guid id, CancellationToken ct)
        => await GetAsync(tenantId, id, ct)
           ?? throw new InvalidOperationException("بارگذاری طرف خدمات بعد از ذخیره ناموفق بود.");

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : PersianText.Normalize(value);

    private static ServiceProviderDetailDto ToDto(ServiceProvider p) => new(
        p.Id, p.Name, p.Specialty, p.Phone, p.Address, p.Notes, p.IsActive,
        p.Rates.OrderBy(r => r.ProcessStepType?.SortOrder ?? 0).Select(ToDto).ToList());

    private static ProviderRateDto ToDto(ServiceProviderRate r) => new(
        r.Id, r.ProcessStepTypeId, r.ProcessStepType?.NameFa ?? "", r.PricingModel, r.UnitRate, r.Notes);
}
