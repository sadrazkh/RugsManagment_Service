using RugsManagment.Domain.Common;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// یک ردیف جدول ارزش گمرکی (spec B.9، C.5): «گبه» = ۴۵ دلار هر متر. کلید، مقدار «فیلد کلید» کارگاه است
/// (<c>TenantPreferences.DeclaredValueKeyField</c>، پیش‌فرض «محل بافت»).
///
/// <para><see cref="KeyNormalized"/> شکل یکسان‌شدهٔ کلید است (حروف فارسی یکسان، ارقام لاتین، فاصله‌های جمع‌شده) و در کارگاه
/// یکتاست؛ پس «نائین ۹ لا» و «نائين 9 لا» یک ردیف‌اند و فرشی که هر کدام را نوشته همان نرخ را می‌گیرد.
/// <see cref="KeyValue"/> همان شکلی است که بازرگان نوشت و در صفحه نشان داده می‌شود.</para>
///
/// xmin ندارد: کل جدول یکجا با «مهر» (<c>TenantPreferences.DeclaredValuesStamp</c>) جایگزین می‌شود و مهر کهنه ۴۰۹ می‌گیرد.
/// </summary>
public class DeclaredValueRate : BaseEntity, ITenantScoped
{
    public const int KeyMaxLength = 120;

    public Guid TenantId { get; set; }

    /// <summary>کلید به شکلی که نوشته شد («کاشان»).</summary>
    public string KeyValue { get; set; } = string.Empty;

    /// <summary>کلید یکسان‌شده (KeyNormalize)؛ یکتا در کارگاه.</summary>
    public string KeyNormalized { get; set; } = string.Empty;

    /// <summary>دلار هر متر مربع (numeric(14,2)، ≥ ۰).</summary>
    public decimal UsdPerM2 { get; set; }
}
