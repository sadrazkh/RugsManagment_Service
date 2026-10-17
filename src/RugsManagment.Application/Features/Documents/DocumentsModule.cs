using Microsoft.Extensions.DependencyInjection;

namespace RugsManagment.Application.Features.Documents;

/// <summary>
/// ثبت سرویس‌های قالب و خروجی «اکسل و چاپ» (فاز اسناد). فعلاً خالی است؛ آن فاز ثبت‌هایش را همین‌جا می‌گذارد تا
/// <c>DependencyInjection.cs</c> (که یخ زده) دوباره ویرایش نشود.
/// </summary>
public static class DocumentsModule
{
    public static IServiceCollection AddDocumentsModule(this IServiceCollection services)
    {
        services.AddScoped<DocumentTemplateService>();
        services.AddScoped<ExportService>();
        services.AddScoped<RugsManagment.Application.Features.Shipments.IShipmentReadinessContributor, DocumentReadinessContributor>();
        services.AddScoped<PresetService>();
        services.AddScoped<RugsManagment.Application.Features.Platform.SetupStatusService>();
        services.AddScoped<RugsManagment.Application.Seed.DocumentTemplateSeedContributor>();
        services.AddScoped<RugsManagment.Application.Fields.IFieldCatalogContributor, DocumentFieldContributor>();
        services.AddScoped<RugsManagment.Application.Abstractions.ITenantSeedContributor, RugsManagment.Application.Seed.DocumentTemplateSeedContributor>();
        return services;
    }
}
