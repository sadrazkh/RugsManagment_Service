using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RugsManagment.Application.Abstractions;

namespace RugsManagment.Application.Features.Platform;

/// <summary>
/// ثبت سرویس‌های سکو (ترجیحات کارگاه و کاربر، خلاصهٔ خانه، seed کارگاه) در لایهٔ Application.
///
/// چرا فایل ماژول: <c>DependencyInjection.cs</c> بعد از این فاز یخ می‌زند و هر فاز فقط فایل ماژول خودش را
/// پر می‌کند. درزهایی که فازهای بعد جایگزین می‌کنند (<see cref="IHomeShipmentSource"/>،
/// <see cref="IHomeMoneySource"/>) با <c>TryAdd</c> ثبت می‌شوند تا ثبتِ <c>Replace</c> فاز بعد، هر ترتیبی که
/// ماژول‌ها صدا زده شوند، برنده باشد. <c>IHomeStockSource</c> و repositoryها در ماژول Infrastructure‌اند.
///
/// <c>IMoneyVisibility</c> این‌جا ثبت نمی‌شود: پاسخش به سیاست Business کاربرِ درخواست بستگی دارد و فقط
/// میزبان وب آن را می‌داند. میزبانی که ثبتش نکند در ساخت سرویس‌ها خطا می‌گیرد، نه اینکه بی‌صدا مبلغ نشان دهد.
/// </summary>
public static class PlatformModule
{
    public static IServiceCollection AddPlatformModule(this IServiceCollection services)
    {
        services.AddScoped<ITenantPreferencesService, TenantPreferencesService>();
        services.AddScoped<IUserPreferencesService, UserPreferencesService>();
        services.AddScoped<IHomeSummaryService, HomeSummaryService>();

        // درزهای فاز محموله و فاز هزینه — تا آن فازها خالی
        services.TryAddScoped<IHomeShipmentSource, EmptyHomeShipmentSource>();
        services.TryAddScoped<IHomeMoneySource, UnknownHomeMoneySource>();

        // پیش‌فرض‌های هر کارگاه؛ اجراکننده (TenantSeedRunner) در Infrastructure ثبت می‌شود
        services.AddScoped<ITenantSeedContributor, TenantPreferencesSeedContributor>();

        return services;
    }
}
