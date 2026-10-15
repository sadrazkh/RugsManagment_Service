using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Features.Shipments.Ledger;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Entities;
using RugsManagment.Infrastructure.Persistence.Ledger;
using RugsManagment.Infrastructure.Persistence.Repositories;

namespace RugsManagment.Infrastructure.Features.Shipments;

/// <summary>
/// ثبت‌های زیرساختی محموله و نگله (فاز محموله): repository محموله، SQL پاس محموله (<see cref="ShipmentLedgerRepository"/>)،
/// نوع ردیف مشتق‌شدهٔ <see cref="ShipmentLineValue"/> برای پاس (بیرون از تفاوت‌سنجی و جداشده بعد از هر پاس، D.4 گام ۸)، و پرسش
/// «مبلغ ذخیره‌شده»ی درآمد محموله برای قفل رقم اعشار (D.5 #22). «انبار» خانه (بی فرش‌های داخل نگله) را ماژول Application
/// همین فاز جایگزین می‌کند؛ ثبت <c>TryAdd</c> فاز سکو بعد از آن کاری نمی‌کند.
/// </summary>
public static class ShipmentsInfrastructureModule
{
    public static IServiceCollection AddShipmentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddScoped<IShipmentRepository, ShipmentRepository>();
        services.TryAddScoped<IShipmentLedgerRepository, ShipmentLedgerRepository>();

        services.Configure<LedgerOptions>(o => o.DerivedTypes.Add(typeof(ShipmentLineValue)));
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IStoredMoneyProbe, ShipmentStoredMoneyProbe>());

        return services;
    }
}
