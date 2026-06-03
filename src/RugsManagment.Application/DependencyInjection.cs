using Microsoft.Extensions.DependencyInjection;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Services;

namespace RugsManagment.Application;

/// <summary>
/// ثبت سرویس‌های لایه Application در DI — Api فقط AddApplication() را صدا می‌زند.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICostCalculationService, CostCalculationService>();
        services.AddScoped<IWorkflowEngine, WorkflowEngine>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRugManagementService, RugManagementService>();
        services.AddScoped<ITenantManagementService, TenantManagementService>();
        services.AddScoped<IWorkflowManagementService, WorkflowManagementService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IRugBatchService, RugBatchService>();
        return services;
    }
}
