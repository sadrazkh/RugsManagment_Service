using Microsoft.Extensions.DependencyInjection;

namespace RugsManagment.Application.Features.Intake;

/// <summary>
/// ثبت سرویس‌های ورود فرش و جدول ورود (فاز جدول). فعلاً خالی است؛ آن فاز ثبت‌هایش را همین‌جا می‌گذارد تا
/// <c>DependencyInjection.cs</c> (که یخ زده) دوباره ویرایش نشود.
/// </summary>
public static class IntakeModule
{
    public static IServiceCollection AddIntakeModule(this IServiceCollection services) => services;
}
