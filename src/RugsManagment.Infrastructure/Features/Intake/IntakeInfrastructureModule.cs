using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RugsManagment.Infrastructure.Features.Intake;

/// <summary>
/// ثبت‌های زیرساختی ورود فرش و منبع‌های جدول (فاز جدول). فعلاً خالی است تا <c>DependencyInjection.cs</c>
/// (که یخ زده) دوباره ویرایش نشود.
/// </summary>
public static class IntakeInfrastructureModule
{
    public static IServiceCollection AddIntakeModule(this IServiceCollection services, IConfiguration configuration)
        => services;
}
