using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RugsManagment.Application.Features.Costs;
using RugsManagment.Application.Features.Costs.Ledger;
using RugsManagment.Application.Ledger;
using RugsManagment.Infrastructure.Persistence.Repositories;
using RugsManagment.Infrastructure.Persistence.Ledger;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Infrastructure.Features.Costs;

/// <summary>
/// ثبت‌های زیرساختی دفتر هزینه و تقسیم (فاز هزینه). فعلاً خالی است تا <c>DependencyInjection.cs</c> (که یخ زده)
/// دوباره ویرایش نشود.
/// </summary>
public static class CostsInfrastructureModule
{
    public static IServiceCollection AddCostsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<CostEntryRepository>();
        services.AddScoped<ICostEntryRepository>(sp => sp.GetRequiredService<CostEntryRepository>());
        services.AddScoped<ICostSplitRepository>(sp => sp.GetRequiredService<CostEntryRepository>());
        services.AddScoped<CostLedgerRepository>();
        services.AddScoped<ICostLedgerRepository>(sp => sp.GetRequiredService<CostLedgerRepository>());
        services.Replace(ServiceDescriptor.Scoped<ICostSourceProvider>(sp => sp.GetRequiredService<CostLedgerRepository>()));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IStoredMoneyProbe, CostLedgerRepository>());
        services.Configure<LedgerOptions>(options =>
        {
            options.DerivedTypes.Add(typeof(CostAllocation));
            options.DerivedTypes.Add(typeof(CostEntryResult));
        });
        return services;
    }
}
