using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Services;
using RugsManagment.Infrastructure.Identity;
using RugsManagment.Infrastructure.Persistence;
using RugsManagment.Infrastructure.Persistence.Repositories;

namespace RugsManagment.Infrastructure;

/// <summary>
/// لایهٔ زیرساخت — اتصال PostgreSQL، پیاده‌سازی Repositoryها و JWT.
/// Application فقط interface می‌بیند؛ اینجا جایگزین واقعی ثبت می‌شود.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=rugs_management;Username=postgres;Password=postgres";

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRugRepository, RugRepository>();
        services.AddScoped<IRugBatchRepository, RugBatchRepository>();
        services.AddScoped<IWorkflowTemplateRepository, WorkflowTemplateRepository>();
        services.AddScoped<IProcessStepTypeRepository, ProcessStepTypeRepository>();
        services.AddScoped<IServiceProviderRepository, ServiceProviderRepository>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IProcessStepTypeLookup, ProcessStepTypeLookup>();

        return services;
    }
}
