using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// قالب برچسب/لیبل هر کارگاه — یا طراحی بصری (ElementsJson) یا HTML خام (HtmlContent).
/// هنگام چاپ، placeholderها با دادهٔ واقعی فرش جایگزین می‌شوند.
/// </summary>
public class LabelTemplate : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>ابعاد برچسب به میلی‌متر (برای چاپ)</summary>
    public int WidthMm { get; set; } = 90;
    public int HeightMm { get; set; } = 50;

    public LabelMode Mode { get; set; } = LabelMode.Visual;

    /// <summary>طراحی بصری به‌صورت JSON (ستون‌بندی + عناصر) — ستون jsonb</summary>
    public string? ElementsJson { get; set; }

    /// <summary>قالب HTML خام با placeholder مثل {{sku}} — برای حالت پیشرفته</summary>
    public string? HtmlContent { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
