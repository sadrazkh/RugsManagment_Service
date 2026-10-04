using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RugsManagment.Infrastructure.Features.Shipments;

/// <summary>
/// ثبت‌های زیرساختی محموله و نگله (فاز محموله) — از جمله جایگزینی <c>IHomeStockSource</c> با «انبارِ بدون فرش‌های
/// داخل نگله». فعلاً خالی است تا <c>DependencyInjection.cs</c> (که یخ زده) دوباره ویرایش نشود.
/// </summary>
public static class ShipmentsInfrastructureModule
{
    public static IServiceCollection AddShipmentsModule(this IServiceCollection services, IConfiguration configuration)
        => services;
}
