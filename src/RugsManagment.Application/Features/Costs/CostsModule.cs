using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RugsManagment.Application.Features.Costs.Ledger;
using RugsManagment.Application.Features.Platform;
using RugsManagment.Application.Features.Rugs;
using RugsManagment.Application.Fields;
using RugsManagment.Application.Ledger;

namespace RugsManagment.Application.Features.Costs;

/// <summary>
/// ثبت سرویس‌های دفتر هزینه و تقسیم (فاز هزینه‌ها، spec D، K.2/P5).
///
/// <list type="bullet">
/// <item>سرویس‌ها: ثبت هزینه، نوع‌های هزینه، تبدیل «فرش‌های دیگر…» به فهرست صریح، و «حساب محموله».</item>
/// <item>پاس سازگاری (D.4، D.5): contributorهای ثبت و ورودی، قفل گروه نوع هزینه، و مرحله‌های ۴۰۰ (خرید)، ۶۰۰ (هزینه، با
/// انتشار «قیمت خرید») و ۷۰۰ (سهم درآمد) — همه با <c>TryAddEnumerable</c> و با نوع (آزمون معماری ترتیبشان را می‌خواند).</item>
/// <item>درزها، با <c>Replace</c> تا ترتیب صدا زدن ماژول‌ها مهم نباشد: کارت پولی خانه (<see cref="IHomeMoneySource"/>)؛ سهم ثبت‌ها
/// در ریز هزینهٔ فرش (<see cref="IRugPurchaseService"/>)؛ هدف‌ها و مبلغ‌های ثابت در برش دسته (<see cref="IRugSplitService"/>)؛ و
/// سنجش سهم‌ها در «سلامت حساب‌ها» (<see cref="ILedgerVerifier"/>). سرویس‌های اصلی فاز ۲ با <c>ActivatorUtilities</c> ساخته و
/// پیچیده می‌شوند، پس خودشان دست نمی‌خورند.</item>
/// </list>
/// منبع سهم‌ها (<see cref="ICostSourceProvider"/>)، پرسش «مبلغ ذخیره‌شده» و repositoryها در ماژول زیرساختی همین فاز‌اند.
/// </summary>
public static class CostsModule
{
    public static IServiceCollection AddCostsModule(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.TryAddScoped<ICostEntryService, CostEntryService>();
        services.TryAddScoped<ICostCategoryService, CostCategoryService>();
        services.TryAddScoped<ITargetResolver, TargetResolver>();
        services.TryAddScoped<IProfitService, ProfitService>();

        // پاس سازگاری: contributorها (D.5 #6، #12، #17، #18، #24، #25b) و قفل گروه نوع (#25)
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IChangeImpactContributor, CostEntryImpactContributor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IChangeImpactContributor, BatchImpactContributor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILedgerGuard, CostCategoryGuard>());

        // مرحله‌ها (LedgerStageOrder: ۴۰۰، ۶۰۰، ۷۰۰)
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILedgerStage, PurchaseEntryStage>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILedgerStage, ExpenseEntryStage>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ILedgerStage, RevenueShareStage>());

        // کاتالوگ فیلدها: پول «حساب محموله»
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IFieldCatalogContributor, CostFieldContributor>());

        // درزهای فاز سکو و فاز ۲
        services.Replace(ServiceDescriptor.Scoped<IHomeMoneySource, CostHomeMoneySource>());
        services.Replace(ServiceDescriptor.Scoped<IRugPurchaseService>(sp => new CostBreakdownDecorator(
            ActivatorUtilities.CreateInstance<RugPurchaseService>(sp), sp.GetRequiredService<ICostEntryRepository>())));
        services.Replace(ServiceDescriptor.Scoped<IRugSplitService>(sp => new CostSplitDecorator(
            ActivatorUtilities.CreateInstance<RugSplitService>(sp), sp.GetRequiredService<ICostSplitRepository>())));
        services.Replace(ServiceDescriptor.Scoped<ILedgerVerifier>(sp => new CostLedgerVerifier(
            ActivatorUtilities.CreateInstance<LedgerVerifier>(sp),
            sp.GetRequiredService<ILedgerSession>(),
            sp.GetRequiredService<ILedgerRepository>(),
            sp.GetRequiredService<ICostLedgerRepository>())));

        return services;
    }
}
