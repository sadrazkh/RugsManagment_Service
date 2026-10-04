using Microsoft.Extensions.DependencyInjection;

namespace RugsManagment.Application.Features.Import;

/// <summary>
/// ثبت سرویس‌های ورود از اکسل (فاز ورود). فعلاً خالی است؛ آن فاز ثبت‌هایش را همین‌جا می‌گذارد تا
/// <c>DependencyInjection.cs</c> (که یخ زده) دوباره ویرایش نشود.
/// </summary>
public static class ImportModule
{
    public static IServiceCollection AddImportModule(this IServiceCollection services) => services;
}
