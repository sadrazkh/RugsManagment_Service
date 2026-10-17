using RugsManagment.Domain.Common;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// یک «نگله» (یا «عدل»، بسته به ترجیح کارگاه) در یک محموله (spec B.9، C.2).
///
/// <para><b>شماره یکتا در محموله، ولی با قید «تعویق‌پذیر»:</b> شماره‌گذاری دوباره (۱، ۳، ۴ ← ۱، ۲، ۳) یا جابه‌جایی دو شماره
/// در یک flush انجام می‌شود؛ قید یکتای معمولی وسط کار (وقتی دو ردیف موقتاً یک شماره دارند) خطا می‌داد. پس migration
/// قید <c>UX_Bales_Shipment_Number UNIQUE … DEFERRABLE INITIALLY DEFERRED</c> را با SQL دستی می‌سازد و یکتایی در پایان
/// تراکنش سنجیده می‌شود.</para>
///
/// نگلهٔ خالی تا محموله باز است مجاز است ولی جلوی قطعی شدن را می‌گیرد (<c>E_EMPTY_BALE</c>). معمولاً نگله را اولین فرشش
/// می‌سازد (عمل «افزودن» با شمارهٔ نگله‌ای که هنوز نیست)، پس «نگلهٔ بعد» هرگز نگلهٔ خالی جا نمی‌گذارد. xmin دارد.
/// </summary>
public class Bale : BaseEntity, ITenantScoped
{
    public const int NotesMaxLength = 1000;

    /// <summary>بزرگ‌ترین شمارهٔ نگله که پذیرفته می‌شود (جلوی اشتباه تایپی مثل ۳۷۷۸ به‌جای شمارهٔ فرش).</summary>
    public const int MaxNumber = 9999;

    public Guid TenantId { get; set; }

    public Guid ShipmentId { get; set; }

    /// <summary>شمارهٔ نگله در محموله (۱ به بعد).</summary>
    public int Number { get; set; }

    /// <summary>وزن (کیلوگرم، numeric(8,2)).</summary>
    public decimal? WeightKg { get; set; }

    public string? Notes { get; set; }

    public Shipment Shipment { get; set; } = null!;

    public ICollection<ShipmentLine> Lines { get; set; } = [];
}
