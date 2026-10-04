using Microsoft.Extensions.DependencyInjection;

namespace RugsManagment.Application.Features.Shipments;

/// <summary>
/// ثبت سرویس‌های محموله و نگله (فاز محموله). فعلاً خالی است؛ آن فاز ثبت‌هایش را — از جمله جایگزینی
/// <c>IHomeShipmentSource</c> با <c>services.Replace(...)</c> — همین‌جا می‌گذارد تا
/// <c>DependencyInjection.cs</c> (که یخ زده) دوباره ویرایش نشود.
/// </summary>
public static class ShipmentsModule
{
    public static IServiceCollection AddShipmentsModule(this IServiceCollection services) => services;
}
