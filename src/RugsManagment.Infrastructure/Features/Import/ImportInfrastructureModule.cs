using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Features.Import;
using RugsManagment.Infrastructure.Persistence.Repositories;
using RugsManagment.Infrastructure.Storage;

namespace RugsManagment.Infrastructure.Features.Import;

/// <summary>
/// ثبت‌های زیرساختی ورود از اکسل (خوانندهٔ xlsx، فاز ورود). فعلاً خالی است تا <c>DependencyInjection.cs</c>
/// (که یخ زده) دوباره ویرایش نشود.
/// </summary>
public static class ImportInfrastructureModule
{
    public static IServiceCollection AddImportModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IImportJobRepository, ImportJobRepository>();
        services.AddSingleton<IWorkbookReader, ClosedXmlWorkbookReader>();
        services.AddSingleton<IImportFileStorage, ImportFileStorage>();
        return services;
    }
}
