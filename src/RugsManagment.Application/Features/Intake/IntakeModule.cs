using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RugsManagment.Application.Features.Intake.Grid;
using RugsManagment.Application.Fields;

namespace RugsManagment.Application.Features.Intake;

/// <summary>
/// ثبت سرویس‌های ورود فرش و جدول‌ها (فاز ۳): منبع‌های جدول (<see cref="IGridSource"/>: <c>intake</c> و <c>rugs</c>)،
/// چیدمان ستون‌ها، ذخیرهٔ دسته‌ای و خانه‌های خرید در کاتالوگ فیلدها. <c>DependencyInjection.cs</c> یخ زده و فقط همین متد
/// را صدا می‌زند.
///
/// منبع جدول با <c>TryAddEnumerable</c> ثبت می‌شود تا فاز نگله‌بندی <c>BalesGridSource</c> خودش را با یک فایل تازه کنار
/// این‌ها بگذارد (درز فاز ۳، K.1). دسترسی دیتابیس (<see cref="IIntakeGridRepository"/>) در ماژول زیرساخت هم‌نام است.
/// </summary>
public static class IntakeModule
{
    public static IServiceCollection AddIntakeModule(this IServiceCollection services)
    {
        services.TryAddScoped<RugGridReader>();
        services.TryAddScoped<GridViewerFactory>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IGridSource, IntakeGridSource>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IGridSource, RugsGridSource>());
        services.TryAddScoped<IGridLayoutService, GridLayoutService>();
        services.TryAddScoped<IRugGridService, RugGridService>();

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IFieldCatalogContributor, PurchaseFieldContributor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IFieldCatalogContributor, IntakeFieldContributor>());

        return services;
    }
}
