namespace RugsManagment.Application.Fields;

/// <summary>درخواست کاتالوگ: کدام کارگاه، برای چه هدفی (<see cref="FieldPurpose"/>) و چه نوع سندی.</summary>
/// <param name="TenantId">کارگاه (فیلدهای سفارشی و نوع‌های خرید مال همین کارگاه‌اند).</param>
/// <param name="Purpose"><see cref="FieldPurpose.Grid"/> یا <see cref="FieldPurpose.Document"/>.</param>
/// <param name="Kind">نوع سند یا شناسهٔ جدول (اختیاری؛ فازهای سند و جدول معنایش را تعیین می‌کنند).</param>
public sealed record FieldCatalogRequest(Guid TenantId, string Purpose, string? Kind);

/// <summary>
/// یک منبع فیلد برای کاتالوگ (درز فاز ۲، spec K.1): فرش (<see cref="RugFieldContributor"/>)، مبلغ‌های فرش
/// (<see cref="RugMoneyFieldContributor"/>)، و در فازهای بعد خانه‌های خرید جدول ورود، خط و نگله و محموله، کارگاه و
/// واژه‌ها. هر فاز فقط یک کلاس contributor اضافه می‌کند و کاتالوگ را ویرایش نمی‌کند.
///
/// قرارداد: فیلدهای پولی را هم برمی‌گرداند (<see cref="FieldDefDto.Money"/> = true)؛ پنهان کردنشان برای کسی که Business
/// نیست کارِ خودِ <see cref="IFieldCatalog"/> است تا هیچ contributorی آن را فراموش نکند.
/// </summary>
public interface IFieldCatalogContributor
{
    /// <summary>ترتیب در فهرست (کوچک‌تر جلوتر)؛ کلید تکراری را اولین contributor نگه می‌دارد.</summary>
    int Order { get; }

    /// <summary>فیلدهای این منبع برای یک درخواست.</summary>
    Task<IReadOnlyList<FieldDefDto>> FieldsAsync(FieldCatalogRequest request, CancellationToken cancellationToken);
}

/// <summary>کاتالوگ فیلدها برای بینندهٔ جاری (<c>GET /api/fields</c>).</summary>
public interface IFieldCatalog
{
    /// <summary>
    /// همهٔ فیلدها به ترتیب contributorها، بی کلید تکراری؛ فیلدهای پولی فقط برای Business.
    /// </summary>
    Task<IReadOnlyList<FieldDefDto>> ListAsync(
        Guid tenantId, string purpose, string? kind, CancellationToken cancellationToken = default);
}
