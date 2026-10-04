namespace RugsManagment.Domain.Entities;

/// <summary>
/// شمارندهٔ اتمیک هر کارگاه (SKU ماهانه، شمارهٔ محموله، شمارهٔ ورودی، …).
///
/// جای «بزرگ‌ترین مقدار موجود + ۱» را می‌گیرد که در ثبت هم‌زمان یا ورود گروهی تکراری می‌ساخت.
/// افزایش با یک UPSERT اتمیک در دیتابیس انجام می‌شود، پس به تلاش دوباره نیازی نیست.
/// کلیدها: <c>sku:{yyyyMM}</c>، <c>shipment</c>، <c>batch</c>، <c>cost</c>، <c>import</c>.
/// </summary>
public class TenantCounter
{
    public Guid TenantId { get; set; }

    /// <summary>نام شمارنده (حداکثر ۴۰ نویسه)</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>آخرین مقدار صادرشده</summary>
    public long Value { get; set; }
}
