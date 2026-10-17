using Microsoft.Extensions.DependencyInjection;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Features.Costs;
using RugsManagment.Application.Features.Documents;
using RugsManagment.Application.Features.Import;
using RugsManagment.Application.Features.Intake;
using RugsManagment.Application.Features.Ledger;
using RugsManagment.Application.Features.Platform;
using RugsManagment.Application.Features.Shipments;
using RugsManagment.Application.Services;

namespace RugsManagment.Application;

/// <summary>
/// ثبت سرویس‌های لایه Application در DI — میزبان (Web) فقط AddApplication() را صدا می‌زند.
///
/// این فایل از این پس یخ زده است: سرویس‌های هر فاز در فایل ماژول همان فاز
/// (<c>Features/{X}/{X}Module.cs</c>) ثبت می‌شوند و این‌جا فقط ماژول‌ها صدا زده می‌شوند.
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
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<ICustomFieldService, CustomFieldService>();
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<ILabelTemplateService, LabelTemplateService>();
        services.AddScoped<IWorkflowManagementService, WorkflowManagementService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IRugBatchService, RugBatchService>();
        services.AddScoped<IRugImageService, RugImageService>();
        services.AddScoped<IServiceProviderService, ServiceProviderService>();
        services.AddScoped<IRugSaleService, RugSaleService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<ITenantSettingsService, TenantSettingsService>();
        services.AddScoped<IActivityLogService, ActivityLogService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IStepTypeService, StepTypeService>();

        // ماژول‌های فازها — هر فاز فقط فایل ماژول خودش را پر می‌کند
        services.AddPlatformModule();
        services.AddLedgerModule();
        services.AddIntakeModule();
        services.AddShipmentsModule();
        services.AddCostsModule();
        services.AddDocumentsModule();
        services.AddImportModule();

        return services;
    }
}
