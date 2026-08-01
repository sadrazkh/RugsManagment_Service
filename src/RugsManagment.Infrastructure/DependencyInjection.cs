using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Services;
using RugsManagment.Infrastructure.Identity;
using RugsManagment.Infrastructure.Persistence;
using RugsManagment.Infrastructure.Persistence.Repositories;
using RugsManagment.Infrastructure.Storage;

namespace RugsManagment.Infrastructure;

/// <summary>
/// لایهٔ زیرساخت — اتصال PostgreSQL و پیاده‌سازی Repositoryها.
/// Application فقط interface می‌بیند؛ اینجا جایگزین واقعی ثبت می‌شود.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=rugs_management;Username=postgres;Password=postgres";

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>)); // Repository عمومی برای موجودیت‌های ساده
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRugRepository, RugRepository>();
        services.AddScoped<IRugBatchRepository, RugBatchRepository>();
        services.AddScoped<IWorkflowTemplateRepository, WorkflowTemplateRepository>();
        services.AddScoped<IProcessStepTypeRepository, ProcessStepTypeRepository>();
        services.AddScoped<IServiceProviderRepository, ServiceProviderRepository>();
        services.AddScoped<IProcessStepTypeLookup, ProcessStepTypeLookup>();

        // ذخیره‌سازی تصاویر روی دیسک — مسیر از Storage:ImagePath خوانده می‌شود
        services.AddSingleton<IImageStorage, LocalImageStorage>();

        // تاریخچهٔ فعالیت — در همان DbContext و همان تراکنش عملیات اصلی می‌نویسد
        services.AddScoped<IAuditLog, AuditLog>();

        return services;
    }
}
