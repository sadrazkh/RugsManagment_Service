using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// یک «محموله» = یک اظهار صادراتی (spec B.9، C.1). نگله‌ها (<see cref="Bale"/>) زیر آن‌اند و هر فرش با یک
/// <see cref="ShipmentLine"/> در یک نگله می‌نشیند.
///
/// <para><b>چرا عکس (snapshot) متن است و نه jsonb:</b> بعد از «قطعی» خروجی‌ها از <see cref="SnapshotJson"/> ساخته می‌شوند و
/// <see cref="SnapshotHash"/> (SHA-256 همان بایت‌ها) در هر خروجی ثبت می‌شود؛ jsonb کلیدها را بازچینی می‌کند و دیگر همان
/// بایت‌ها نمی‌ماند، پس ستون <c>text</c> است تا هش دوبارهٔ مقدار ذخیره‌شده همان هش باشد.</para>
///
/// <para><b>قفل:</b> وقتی <see cref="Status"/> قطعی است، عنوان، تاریخ، مقصد، خریدار، شمارهٔ اظهار، گمرک و نرخ دلار عوض
/// نمی‌شوند (<c>ShipmentLockGuard</c> در پاس سازگاری، پس هر مسیر نوشتنی پوشیده است)؛ یادداشت و درآمد آزادند.</para>
/// xmin دارد (پیکربندی در Infrastructure).
/// </summary>
public class Shipment : BaseEntity, ITenantScoped
{
    public const int TitleMaxLength = 200;
    public const int DestinationMaxLength = 200;
    public const int BuyerNameMaxLength = 200;
    public const int DeclarationNoMaxLength = 60;
    public const int CustomsOfficeMaxLength = 200;
    public const int NotesMaxLength = 2000;

    /// <summary>طول هش SHA-256 به‌صورت hex (char(64)).</summary>
    public const int SnapshotHashLength = 64;

    /// <summary>کلید شمارندهٔ کارگاه برای شمارهٔ محموله (B.2).</summary>
    public const string CounterKey = "shipment";

    public Guid TenantId { get; set; }

    /// <summary>شمارهٔ محموله در کارگاه (شمارندهٔ <see cref="CounterKey"/>، یکتا).</summary>
    public int Number { get; set; }

    /// <summary>عنوان؛ پیش‌فرض «اظهار {شماره}».</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>تاریخ اظهار (روز ایران)؛ پیش‌فرض امروز.</summary>
    public DateOnly DocumentDate { get; set; }

    public string? Destination { get; set; }
    public string? BuyerName { get; set; }
    public string? DeclarationNo { get; set; }
    public string? CustomsOffice { get; set; }

    public ShipmentStatus Status { get; set; } = ShipmentStatus.Open;

    /// <summary>چند بار قطعی شده (هر «قطعی» یکی بالا می‌برد).</summary>
    public int FinalizeCount { get; set; }

    public DateTimeOffset? FinalizedAt { get; set; }

    /// <summary>کاربری که آخرین بار قطعی کرد (بدون کلید خارجی، مثل <c>Rug.DeletedByUserId</c>).</summary>
    public Guid? FinalizedBy { get; set; }

    /// <summary>عکس متعارف آخرین «قطعی» (JSON با کلیدهای مرتب، عددهای با رقم ثابت، متن NFC)؛ null وقتی باز است.</summary>
    public string? SnapshotJson { get; set; }

    /// <summary>SHA-256 بایت‌های UTF-8 همان <see cref="SnapshotJson"/> (hex کوچک، ۶۴ نویسه).</summary>
    public string? SnapshotHash { get; set; }

    /// <summary>عکس قبلی؛ «باز کردن دوباره» عکس جاری را این‌جا می‌برد.</summary>
    public string? LastSnapshotJson { get; set; }

    /// <summary>متراژ هدف هر نگله (numeric(6,2))؛ null یعنی ترجیح کارگاه.</summary>
    public decimal? TargetBaleAreaM2 { get; set; }

    /// <summary>نرخ دلار به ارز کارگاه (numeric(18,6))؛ برای درآمد و قالب‌هایی که دلار را به تومان می‌برند.</summary>
    public decimal? UsdRate { get; set; }

    /// <summary>درآمد فروش محموله (فاز هزینه‌ها؛ numeric(18,2)).</summary>
    public decimal? RevenueAmount { get; set; }

    /// <summary>واحد درآمد (معمولاً دلار).</summary>
    public CurrencyUnit? RevenueCurrency { get; set; }

    /// <summary>نرخ تبدیل درآمد به ارز کارگاه (numeric(18,6)).</summary>
    public decimal? RevenueFx { get; set; }

    public string? Notes { get; set; }

    public ICollection<Bale> Bales { get; set; } = [];

    /// <summary>قطعی است (قفل).</summary>
    public bool IsFinalized => Status == ShipmentStatus.Finalized;
}
