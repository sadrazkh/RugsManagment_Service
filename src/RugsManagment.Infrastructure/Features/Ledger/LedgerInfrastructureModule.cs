using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Ledger;
using RugsManagment.Infrastructure.Persistence;
using RugsManagment.Infrastructure.Persistence.Ledger;
using RugsManagment.Infrastructure.Persistence.Repositories;

namespace RugsManagment.Infrastructure.Features.Ledger;

/// <summary>
/// ثبت‌های زیرساختی حساب‌ها (فاز ۲، spec D.4): جلسهٔ EFیِ پاس (تفاوت‌سنجی، تراکنش، قفل)، repository پاس با CostSql، خوانندهٔ
/// جمع هزینه، نسخهٔ هم‌زمانی، پرسش «مبلغ ذخیره‌شده» جدول‌های فاز ۲ و بررسی حساب‌ها در استارت (ترتیب ۲۰۰).
///
/// فازهای بعد نوع ردیف‌های مشتق‌شدهٔ خودشان را با <c>services.Configure&lt;LedgerOptions&gt;</c> و پرسش «مبلغ ذخیره‌شده»ی
/// خودشان را با <c>TryAddEnumerable</c> در ماژول خودشان اضافه می‌کنند؛ این فایل دوباره ویرایش نمی‌شود.
/// </summary>
public static class LedgerInfrastructureModule
{
    public static IServiceCollection AddLedgerModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<LedgerOptions>();

        services.TryAddScoped<LedgerTracking>();
        services.TryAddScoped<ChangeSetBuilder>();
        services.TryAddScoped<LedgerSession>();
        services.TryAddScoped<ILedgerSession>(sp => sp.GetRequiredService<LedgerSession>());
        services.TryAddScoped<ILedgerLock>(sp => sp.GetRequiredService<LedgerSession>());

        services.TryAddScoped<ILedgerRepository, LedgerRepository>();
        services.TryAddScoped<IRugCostTotalReader, RugCostTotalReader>();
        services.TryAddScoped<IConcurrencyGuard, ConcurrencyGuard>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IStoredMoneyProbe, CoreStoredMoneyProbe>());

        // پرسش‌هایی که فرش‌های سطل زباله را هم می‌بینند (حذف نوع مرحلهٔ در حال استفاده، وصل سطرهای قدیمی M2)
        services.TryAddScoped<ITrashAwareQueries, TrashAwareQueries>();

        // بعد از seed کارگاه‌ها (۱۰۰)
        services.AddScoped<IStartupTask, LedgerStartupCheck>();

        return services;
    }
}
