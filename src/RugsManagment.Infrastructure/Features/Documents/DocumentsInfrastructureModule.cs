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
        => services;
}
