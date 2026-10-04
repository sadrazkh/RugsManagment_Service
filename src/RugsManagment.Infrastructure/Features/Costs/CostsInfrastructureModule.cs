using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RugsManagment.Infrastructure.Features.Costs;

/// <summary>
/// ثبت‌های زیرساختی دفتر هزینه و تقسیم (فاز هزینه). فعلاً خالی است تا <c>DependencyInjection.cs</c> (که یخ زده)
/// دوباره ویرایش نشود.
/// </summary>
public static class CostsInfrastructureModule
{
    public static IServiceCollection AddCostsModule(this IServiceCollection services, IConfiguration configuration)
        => services;
}
