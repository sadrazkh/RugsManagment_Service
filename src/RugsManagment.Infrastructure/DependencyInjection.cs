using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Services;
using RugsManagment.Infrastructure.Features.Costs;
using RugsManagment.Infrastructure.Features.Documents;
using RugsManagment.Infrastructure.Features.Import;
using RugsManagment.Infrastructure.Features.Intake;
using RugsManagment.Infrastructure.Features.Ledger;
using RugsManagment.Infrastructure.Features.Platform;
using RugsManagment.Infrastructure.Features.Shipments;
using RugsManagment.Infrastructure.Persistence;
using RugsManagment.Infrastructure.Persistence.Repositories;
using RugsManagment.Infrastructure.Storage;

namespace RugsManagment.Infrastructure;

/// <summary>
/// لایهٔ زیرساخت — اتصال PostgreSQL، پیاده‌سازی Repositoryها، شمارنده‌ها و ذخیرهٔ تصویر.
/// Application فقط interface می‌بیند؛ اینجا جایگزین واقعی ثبت می‌شود.
///
/// این فایل از این پس یخ زده است: ثبت‌های هر فاز در ماژول زیرساختی همان فاز
/// (<c>Features/{X}/{X}InfrastructureModule.cs</c>) است و این‌جا فقط ماژول‌ها صدا زده می‌شوند.
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
        // scoped: فقط نوع مرحلهٔ سیستمی یا مال کارگاه کاربر جاری را برمی‌گرداند
        services.AddScoped<IProcessStepTypeLookup, ProcessStepTypeLookup>();

        // شمارندهٔ اتمیک کارگاه (SKU، شمارهٔ محموله، …) — یک UPSERT، بدون تلاش دوباره
        services.AddScoped<ITenantCounters, TenantCounterRepository>();

        // ذخیره‌سازی تصاویر روی دیسک — مسیر از Storage:ImagePath خوانده می‌شود
        services.AddSingleton<IImageStorage, LocalImageStorage>();

        // تاریخچهٔ فعالیت — در همان DbContext و همان تراکنش عملیات اصلی می‌نویسد
        services.AddScoped<IAuditLog, AuditLog>();

        // ماژول‌های فازها — هر فاز فقط فایل ماژول زیرساختی خودش را پر می‌کند
        services.AddPlatformModule(configuration);
        services.AddLedgerModule(configuration);
        services.AddIntakeModule(configuration);
        services.AddShipmentsModule(configuration);
        services.AddCostsModule(configuration);
        services.AddDocumentsModule(configuration);
        services.AddImportModule(configuration);

        return services;
    }
}
