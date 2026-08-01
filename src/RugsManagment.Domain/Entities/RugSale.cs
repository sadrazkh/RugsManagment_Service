using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// ثبت فروش یک فرش.
///
/// هر فرش یک کالای یکتاست، پس حداکثر یک فروش دارد (ایندکس یکتا روی RugId).
/// با ثبت فروش، وضعیت فرش به «فروخته‌شده» می‌رود و با لغو فروش به «آمادهٔ فروش» برمی‌گردد.
///
/// وجود این رکورد است که «سود واقعی» را ممکن می‌کند؛ پیش از آن فقط «سود تخمینی»
/// بر اساس قیمت هدف در دسترس بود.
/// </summary>
public class RugSale : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid RugId { get; set; }

    public string BuyerName { get; set; } = string.Empty;
    public string? BuyerPhone { get; set; }

    /// <summary>مبلغ فروش پیش از تخفیف (تومان)</summary>
    public decimal SalePrice { get; set; }

    /// <summary>تخفیف داده‌شده (تومان) — هرگز بزرگ‌تر از SalePrice نیست</summary>
    public decimal Discount { get; set; }

    /// <summary>
    /// مبلغی که تا الان واقعاً دریافت شده. در فروش نقدی برابر NetAmount است؛
    /// در فروش اقساطی/چکی کمتر و تفاوتش «طلب از مشتری» می‌شود.
    /// </summary>
    public decimal ReceivedAmount { get; set; }

    public SalePaymentMethod PaymentMethod { get; set; } = SalePaymentMethod.Cash;

    /// <summary>زمان فروش (UTC) — ممکن است با زمان ثبت در سامانه فرق کند</summary>
    public DateTimeOffset SoldAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>شمارهٔ فاکتور، چک یا ارجاع بانکی</summary>
    public string? Reference { get; set; }

    public string? Notes { get; set; }

    public Rug Rug { get; set; } = null!;
    public Tenant Tenant { get; set; } = null!;

    /// <summary>مبلغ خالص فروش پس از تخفیف — پایهٔ محاسبهٔ سود واقعی</summary>
    public decimal NetAmount => SalePrice - Discount;

    /// <summary>باقی‌ماندهٔ طلب از خریدار (فروش اقساطی)</summary>
    public decimal OutstandingAmount => Math.Max(0, NetAmount - ReceivedAmount);
}
