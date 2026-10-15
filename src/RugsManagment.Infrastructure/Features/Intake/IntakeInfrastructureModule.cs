using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RugsManagment.Application.Features.Intake;
using RugsManagment.Infrastructure.Persistence.Repositories;

namespace RugsManagment.Infrastructure.Features.Intake;

/// <summary>
/// ثبت‌های زیرساختی ورود فرش و جدول‌ها (فاز ۳): دسترسی دیتابیس ردیف‌های جدول، ورودی‌های (روز، فروشنده) و چیدمان
/// ستون‌ها. سرویس‌ها و منبع‌های جدول در ماژول Application (<c>AddIntakeModule()</c> همان‌نام، بی پیکربندی) ثبت می‌شوند.
/// </summary>
public static class IntakeInfrastructureModule
{
    public static IServiceCollection AddIntakeModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddScoped<IIntakeGridRepository, IntakeGridRepository>();
        return services;
    }
}
