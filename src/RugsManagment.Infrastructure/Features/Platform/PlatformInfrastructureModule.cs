using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Features.Platform;
using RugsManagment.Infrastructure.Persistence.Repositories;
using RugsManagment.Infrastructure.Persistence.Seed;

namespace RugsManagment.Infrastructure.Features.Platform;

/// <summary>
/// ثبت‌های زیرساختی سکو: repositoryهای ترجیحات و تاریخچه، منبع «انبار» خانه و اجراکنندهٔ seed کارگاه‌ها
/// (هم <see cref="ITenantSeedRunner"/> و هم کار استارت با ترتیب ۱۰۰).
/// </summary>
public static class PlatformInfrastructureModule
{
    /// <summary>
    /// هم‌نام ماژول Application است ولی پیکربندی هم می‌گیرد؛ پس دو متد هرگز در یک فراخوانی با هم اشتباه
    /// نمی‌شوند، حتی اگر هر دو namespace در یک فایل using شده باشند.
    /// </summary>
    public static IServiceCollection AddPlatformModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ITenantPreferencesRepository, TenantPreferencesRepository>();
        services.AddScoped<IAuditEntryRepository, AuditEntryRepository>();

        // فاز محموله «انبار» را دقیق‌تر می‌کند (بدون فرش‌های داخل نگله) و این را Replace می‌کند
        services.TryAddScoped<IHomeStockSource, RugHomeStockSource>();

        // یک نمونه در هر scope هم seed کارگاه تازه را انجام می‌دهد و هم کار استارت است
        services.AddScoped<TenantSeedRunner>();
        services.AddScoped<ITenantSeedRunner>(sp => sp.GetRequiredService<TenantSeedRunner>());
        services.AddScoped<IStartupTask>(sp => sp.GetRequiredService<TenantSeedRunner>());

        return services;
    }
}
