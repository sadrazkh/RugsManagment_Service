using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RugsManagment.Infrastructure.Features.Documents;

/// <summary>
/// ثبت‌های زیرساختی «اکسل و چاپ» (نویسندهٔ xlsx، فاز اسناد). فعلاً خالی است تا <c>DependencyInjection.cs</c>
/// (که یخ زده) دوباره ویرایش نشود.
/// </summary>
public static class DocumentsInfrastructureModule
{
    public static IServiceCollection AddDocumentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<RugsManagment.Application.Abstractions.Persistence.IDocumentTemplateRepository, RugsManagment.Infrastructure.Persistence.Repositories.DocumentTemplateRepository>();
        services.AddScoped<RugsManagment.Application.Abstractions.Persistence.IDocumentDataRepository, RugsManagment.Infrastructure.Persistence.Repositories.DocumentDataRepository>();
        services.AddScoped<RugsManagment.Application.Abstractions.Persistence.IExportSetupRepository, RugsManagment.Infrastructure.Persistence.Repositories.ExportSetupRepository>();
        services.AddScoped<RugsManagment.Application.Abstractions.Services.IXlsxDocumentWriter, RugsManagment.Infrastructure.Documents.ClosedXmlDocumentWriter>();
        return services;
    }
}
