using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// ارزش گمرکی مشتق‌شدهٔ یک خط (spec B.9، C.5) — یک ردیف برای هر <see cref="ShipmentLine"/>، کلید همان شناسهٔ خط. فقط
/// مرحلهٔ <c>ShipmentLineValueStage</c> پاس سازگاری می‌نویسد؛ xmin ندارد و نوشتنش نسخهٔ فرش یا خط را عوض نمی‌کند.
///
/// <para>قاعدهٔ دسته (G.4، D.3): <see cref="PieceUsd"/> = ROUND(مساحت یک تخته × نرخ، ۲) و <see cref="LineUsd"/> =
/// دلار تخته × تعداد؛ <see cref="LineAreaM2"/> = مساحت تخته × تعداد. پس دستهٔ ۵۰ تختهٔ ۰٫۱۶ با ۴۵٫۵۵ دلار ۳۶۴٫۵۰ می‌شود، نه
/// ۳۶۴٫۴۰، و جمع نگله با اکسل بازرگان می‌خواند.</para>
///
/// محمولهٔ باز با هر تغییر ورودی (اندازه، تعداد، مقدار فیلد کلید، جدول، نرخ پیش‌فرض، نرخ دستی) دوباره حساب می‌شود؛
/// محمولهٔ قطعی منجمد است تا «باز کردن دوباره» که از منبع‌های فعلی از نو حساب می‌کند (C.9).
/// </summary>
public class ShipmentLineValue : ITenantScoped
{
    public Guid LineId { get; set; }

    public Guid TenantId { get; set; }

    public Guid ShipmentId { get; set; }

    /// <summary>نرخ به کار رفته (دلار هر متر، numeric(14,2))؛ null وقتی <see cref="RateSource"/> «ندارد» است.</summary>
    public decimal? UsdPerM2 { get; set; }

    public DeclaredRateSource RateSource { get; set; } = DeclaredRateSource.Missing;

    /// <summary>دلار یک تخته (numeric(14,2)).</summary>
    public decimal? PieceUsd { get; set; }

    /// <summary>متراژ خط = مساحت مؤثر یک تخته × تعداد (numeric(12,2)).</summary>
    public decimal LineAreaM2 { get; set; }

    /// <summary>دلار خط = دلار تخته × تعداد (numeric(14,2)).</summary>
    public decimal? LineUsd { get; set; }

    public DateTimeOffset ComputedAt { get; set; }

    public ShipmentLine Line { get; set; } = null!;
}
