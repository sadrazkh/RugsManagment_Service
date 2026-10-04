using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Fields;
using RugsManagment.Application.Seed;

namespace RugsManagment.Application.Features.Rugs;

/// <summary>
/// ثبت سرویس‌های فرش فاز ۲ (بستهٔ WP2-C): نویسندهٔ فیلدها، شمارهٔ بعدی، کد انبار، برش دسته، کاتالوگ فیلدها و
/// seed نوع‌های خرید.
///
/// چرا یک متد جدا و نه مستقیم در یک ماژول: <c>DependencyInjection.cs</c> یخ زده و ماژول فاز ۲
/// (<c>AddLedgerModule()</c>) مال بستهٔ حساب‌هاست؛ آن ماژول فقط همین یک خط را صدا می‌زند. همه با <c>TryAdd</c> ثبت
/// می‌شوند، پس صدا زدن دوباره (مثلاً از میزبان آزمون) چیزی را دوبار ثبت نمی‌کند.
/// </summary>
public static class RugFeaturesRegistration
{
    public static IServiceCollection AddRugFeatures(this IServiceCollection services)
    {
        services.TryAddScoped<IRugFieldWriter, RugFieldWriter>();
        services.TryAddScoped<ISerialSuggester, SerialSuggester>();
        services.TryAddScoped<IRugSkuGenerator, RugSkuGenerator>();
        services.TryAddScoped<IRugSplitService, RugSplitService>();
        services.TryAddScoped<IRugDtoBuilder, RugDtoBuilder>();
        services.TryAddScoped<IRugPurchaseService, RugPurchaseService>();

        // کاتالوگ فیلدها و دو منبع فاز ۲؛ فازهای بعد منبع خودشان را با TryAddEnumerable اضافه می‌کنند
        services.TryAddScoped<IFieldCatalog, FieldCatalog>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IFieldCatalogContributor, RugFieldContributor>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IFieldCatalogContributor, RugMoneyFieldContributor>());

        // سه نوع خرید پایهٔ هر کارگاه (و وصل کردن سطرهای قدیمی M2 به «قیمت خرید»)
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITenantSeedContributor, CostCategorySeedContributor>());

        return services;
    }
}
