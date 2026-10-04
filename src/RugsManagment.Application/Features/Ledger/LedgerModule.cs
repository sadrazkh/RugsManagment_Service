using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RugsManagment.Application.Features.Rugs;
using RugsManagment.Application.Ledger;
using RugsManagment.Application.Ledger.Contributors;
using RugsManagment.Application.Ledger.Guards;
using RugsManagment.Application.Ledger.Stages;

namespace RugsManagment.Application.Features.Ledger;

/// <summary>
/// ثبت سرویس‌های «پاس سازگاری» حساب‌ها (فاز ۲، spec D.4) و سرویس‌های فرش نسخهٔ ۲.
///
/// contributorها، guardها و مرحله‌ها با <c>TryAddEnumerable</c> ثبت می‌شوند: فاز محموله و فاز هزینه‌ها contributor/guard/
/// مرحلهٔ خودشان را در ماژول خودشان به همین فهرست‌ها اضافه می‌کنند و این فایل دوباره ویرایش نمی‌شود. منبع سهم ثبت‌ها
/// (<see cref="ICostSourceProvider"/>) با <c>TryAdd</c> است تا فاز هزینه‌ها با <c>Replace</c> جایش را بگیرد. پیاده‌سازی‌های
/// EFی (جلسه، repository، خوانندهٔ جمع، بررسی استارت) در ماژول زیرساختی همین فاز ثبت می‌شوند.
/// </summary>
public static class LedgerModule
{
    public static IServiceCollection AddLedgerModule(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.TryAddScoped<ILedgerPass, LedgerPass>();
        services.TryAddScoped<ILedgerMaintenance, LedgerMaintenance>();
        services.TryAddScoped<ILedgerVerifier, LedgerVerifier>();
        services.TryAddScoped<ICostSourceProvider, NoCostSources>();

        // contributorهای فاز ۲ (D.5 ردیف‌های ۱–۱۱)
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IChangeImpactContributor, RugImpactContributor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IChangeImpactContributor, PurchaseLineImpactContributor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IChangeImpactContributor, StepImpactContributor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IChangeImpactContributor, SaleImpactContributor>());

        // قفل‌های فاز ۲ (D.5 #22)
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILedgerGuard, PreferencesLockGuard>());

        // مرحله‌های فاز ۲ (ترتیب در LedgerStageOrder)
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILedgerStage, SerialOverlapStage>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILedgerStage, PurchaseLineStage>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILedgerStage, PurchaseRollupStage>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILedgerStage, RugCostTotalStage>());

        // سرویس‌های فرش نسخهٔ ۲ (بستهٔ WP2-C: نویسندهٔ فیلدها، شمارهٔ بعدی، برش دسته، کاتالوگ فیلدها، seed نوع‌های خرید)
        services.AddRugFeatures();

        return services;
    }
}
