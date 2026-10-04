using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RugsManagment.Infrastructure.Features.Import;

/// <summary>
/// ثبت‌های زیرساختی ورود از اکسل (خوانندهٔ xlsx، فاز ورود). فعلاً خالی است تا <c>DependencyInjection.cs</c>
/// (که یخ زده) دوباره ویرایش نشود.
/// </summary>
public static class ImportInfrastructureModule
{
    public static IServiceCollection AddImportModule(this IServiceCollection services, IConfiguration configuration)
        => services;
}
