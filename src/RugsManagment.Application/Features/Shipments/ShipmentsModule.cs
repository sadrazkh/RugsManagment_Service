using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RugsManagment.Application.Features.Platform;
using RugsManagment.Application.Features.Rugs;
using RugsManagment.Application.Features.Shipments.Ledger;
using RugsManagment.Application.Fields;
using RugsManagment.Application.Ledger;

namespace RugsManagment.Application.Features.Shipments;

/// <summary>
/// ثبت سرویس‌های محموله و نگله (فاز محموله، spec C، K.2/P4a).
///
/// <list type="bullet">
/// <item>سرویس‌ها: محموله، نگله، خط‌ها، جدول ارزش گمرکی، آمادگی، «قطعی» و «باز کردن دوباره».</item>
/// <item>پاس سازگاری (D.4): contributorهای محموله، جدول و ترجیحات؛ قفل «قطعی» پیش از قفل عضویت (پیام قفل اول می‌آید)
/// و قفل فیلد کلید؛ مرحلهٔ ارزش خط (ترتیب ۳۰۰). همه با <c>TryAddEnumerable</c> به فهرست‌های فاز ۲ اضافه می‌شوند.</item>
/// <item>درزها: enricher فرش (محموله، نگله، دلار، جا)، فیلدهای کاتالوگ (خط، نگله، محموله، صادرکننده، واژه‌ها)، و
/// منبع‌های خانه — <see cref="IHomeShipmentSource"/> و <see cref="IHomeStockSource"/> با <c>Replace</c> جای پیش‌فرض‌های فاز سکو را
/// می‌گیرند، هر ترتیبی که ماژول‌ها صدا زده شوند.</item>
/// </list>
/// پیاده‌سازی‌های EFی (repositoryها، نوع ردیف مشتق‌شده، پرسش «مبلغ ذخیره‌شده»ی درآمد محموله) در ماژول زیرساختی‌اند.
/// </summary>
public static class ShipmentsModule
{
    public static IServiceCollection AddShipmentsModule(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.TryAddScoped<IShipmentSettingsReader, ShipmentSettingsReader>();
        services.TryAddScoped<ReadinessService>();
        services.TryAddScoped<IReadinessService>(sp => sp.GetRequiredService<ReadinessService>());
        services.TryAddScoped<IShipmentDtoBuilder, ShipmentDtoBuilder>();
        services.TryAddScoped<IShipmentService, ShipmentService>();
        services.TryAddScoped<IBaleService, BaleService>();
        services.TryAddScoped<ILineService, LineService>();
        services.TryAddScoped<IDeclaredValueService, DeclaredValueService>();
        services.TryAddScoped<IFinalizeService, FinalizeService>();

        // پاس سازگاری: contributorها (D.5 #7، #13–#21)
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IChangeImpactContributor, ShipmentImpactContributor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IChangeImpactContributor, DeclaredValueImpactContributor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IChangeImpactContributor, PreferencesImpactContributor>());

        // قفل‌ها (C.8، D.5 #26) — قفل «قطعی» پیش از عضویت تا فرشِ محمولهٔ قطعی پیام قفل بگیرد
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILedgerGuard, ShipmentLockGuard>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILedgerGuard, BaleMembershipGuard>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILedgerGuard, KeyFieldGuard>());

        // مرحلهٔ ارزش خط (LedgerStageOrder.ShipmentLineValue = ۳۰۰)
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILedgerStage, ShipmentLineValueStage>());

        // درزهای فاز ۲ و سکو
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IRugDtoEnricher, ShipmentRugDtoEnricher>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IFieldCatalogContributor, ShipmentFieldContributor>());
        services.Replace(ServiceDescriptor.Scoped<IHomeShipmentSource, HomeShipmentSource>());
        services.Replace(ServiceDescriptor.Scoped<IHomeStockSource, ShipmentHomeStockSource>());

        // جدول نگله‌ها (فاز ۴b) کنار جدول‌های ورود و انبار (درز IGridSource فاز ۳)
        services.TryAddEnumerable(ServiceDescriptor.Scoped<Intake.Grid.IGridSource, Grid.BalesGridSource>());

        return services;
    }
}
