using Microsoft.Extensions.DependencyInjection;

namespace RugsManagment.Application.Features.Costs;

/// <summary>
/// ثبت سرویس‌های دفتر هزینه و تقسیم (فاز هزینه). فعلاً خالی است؛ آن فاز ثبت‌هایش را — از جمله جایگزینی
/// <c>IHomeMoneySource</c> — همین‌جا می‌گذارد تا <c>DependencyInjection.cs</c> (که یخ زده) دوباره ویرایش نشود.
/// </summary>
public static class CostsModule
{
    public static IServiceCollection AddCostsModule(this IServiceCollection services) => services;
}
