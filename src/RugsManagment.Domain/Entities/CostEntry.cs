using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// یک «ثبت هزینه» در دفتر «هزینه‌ها» (spec B.10، D.2): کرایه، شستشو، گمرک، خرید یکجا، … برای چند فرش.
///
/// <para><b>سه پرسش هر ثبت:</b> چه و کی (نوع، تاریخ، رسید، طرف خدمات)؛ چقدر (<see cref="Basis"/>: کل، هر متر یا هر تخته؛
/// <see cref="Rate"/>/<see cref="Amount"/> در <see cref="Currency"/> و <see cref="BaseAmount"/> در ارز کارگاه)؛ و برای کدام فرش‌ها
/// و به چه نسبت (<see cref="Scope"/>، <see cref="Method"/>، <see cref="AreaBasis"/>). خودِ سهم هر فرش ذخیرهٔ کاربر نیست؛
/// پاس سازگاری آن را در <see cref="CostAllocation"/> و خلاصه را در <see cref="CostEntryResult"/> می‌نویسد.</para>
///
/// <para><b>رسید منجمد (D.3):</b> ثبت «هر متر» یا «هر تخته» تا وقتی طرف خدمات و شمارهٔ رسید ندارد زنده است (کل = Σ روی
/// فرش‌های فعلی). با گذاشتن یکی از این دو، ذخیره آن را منجمد می‌کند: <see cref="Amount"/> و <see cref="BaseAmount"/> همان کل
/// لحظه می‌شوند و <see cref="FrozenAt"/> پر می‌شود؛ از آن به بعد مثل «کل» تقسیم می‌شود، پس رسیدِ پرداخت‌شده با حذف یک فرش
/// کوچک نمی‌شود و صورت‌حساب طرف خدمات تکرارپذیر است.</para>
///
/// <para><b>کلیدهای خارجی:</b> محموله و ورودی SET NULL (ثبت «تقسیم‌نشده» با <c>W_TARGET_DELETED</c> می‌ماند)، نوع هزینه
/// Restrict، طرف خدمات NO ACTION. حذف نرم (<see cref="DeletedAt"/>) با «برگردان» برمی‌گردد. <see cref="ClientKey"/> ساخت
/// را تکرارناپذیر می‌کند (تلاش دوبارهٔ پنل بعد از قطع اینترنت ثبت دوم نمی‌سازد). xmin دارد و هرگز روی نمونهٔ ردیابی‌نشده
/// <c>Update()</c> نمی‌شود (B.1).</para>
/// </summary>
public class CostEntry : BaseEntity, ITenantScoped
{
    /// <summary>کلید شمارندهٔ کارگاه برای شمارهٔ ثبت (B.2).</summary>
    public const string CounterKey = "cost";

    public const int DescriptionMaxLength = 500;
    public const int ReceiptNoMaxLength = 60;
    public const int ScopeNoteMaxLength = 200;

    public Guid TenantId { get; set; }

    /// <summary>شمارهٔ ثبت در کارگاه (شمارندهٔ <see cref="CounterKey"/>)؛ «هزینهٔ شمارهٔ ۱۲».</summary>
    public int Number { get; set; }

    // ── چه و کی ──────────────────────────────────────────────────────────

    public Guid CategoryId { get; set; }

    /// <summary>تاریخ هزینه (روز ایران).</summary>
    public DateOnly Date { get; set; }

    public string? Description { get; set; }

    /// <summary>شمارهٔ رسید؛ با پر شدنش ثبتِ متری یا دانه‌ای منجمد می‌شود (D.3).</summary>
    public string? ReceiptNo { get; set; }

    /// <summary>کارگاه / طرف حساب؛ با پر شدنش ثبتِ متری یا دانه‌ای منجمد می‌شود و در صورت‌حساب او یک ردیف «رسید» است.</summary>
    public Guid? ServiceProviderId { get; set; }

    // ── چقدر ─────────────────────────────────────────────────────────────

    /// <summary>کل، هر متر یا هر تخته (درصد فقط برای سطر خرید است، نه ثبت هزینه).</summary>
    public CostBasis Basis { get; set; } = CostBasis.Total;

    /// <summary>نرخ برای «هر متر» / «هر تخته» در <see cref="Currency"/> (numeric(18,4))؛ null برای «کل».</summary>
    public decimal? Rate { get; set; }

    /// <summary>
    /// مبلغ در <see cref="Currency"/> (numeric(18,2)): برای «کل» همان مبلغ کاربر؛ برای رسید منجمد کلِ لحظهٔ انجماد؛ برای
    /// ثبت زنده null.
    /// </summary>
    public decimal? Amount { get; set; }

    public CurrencyUnit Currency { get; set; } = CurrencyUnit.Toman;

    /// <summary>نرخ دلار (numeric(18,6)) برای مبلغ دلاری؛ ریال↔تومان نرخ ثابت ۱۰ / ۰٫۱ دارد و این‌جا نمی‌نشیند.</summary>
    public decimal? FxRate { get; set; }

    /// <summary>
    /// مبلغ به ارز کارگاه (numeric(18,2)، در MoneyScale): «کل» = ROUND(Amount × fx، s) یک بار؛ رسید منجمد = کلِ لحظهٔ انجماد؛
    /// ثبت زنده = کلِ آخرین ذخیره (فقط برای نمایش؛ کلِ زندهٔ هر اجرا در <see cref="CostEntryResult.BaseAmount"/> است).
    /// </summary>
    public decimal BaseAmount { get; set; }

    /// <summary>زمان انجماد رسید «هر متر» / «هر تخته» (D.3)؛ null یعنی زنده یا «کل».</summary>
    public DateTimeOffset? FrozenAt { get; set; }

    // ── چطور تقسیم شود ───────────────────────────────────────────────────

    /// <summary>کدام اندازه پایهٔ متراژ است (اندازهٔ اول یا مؤثر).</summary>
    public AreaBasis AreaBasis { get; set; } = AreaBasis.Effective;

    public AllocationMethod Method { get; set; } = AllocationMethod.ByArea;

    public CostScope Scope { get; set; } = CostScope.Selection;

    // ── برای کدام فرش‌ها ─────────────────────────────────────────────────

    /// <summary>محمولهٔ ثبتِ «این محموله» (SET NULL با حذف محموله).</summary>
    public Guid? ShipmentId { get; set; }

    /// <summary>ورودیِ ثبتِ «این ورودی» (SET NULL با حذف ورودی).</summary>
    public Guid? BatchId { get; set; }

    /// <summary>این فهرست چطور ساخته شد: «۲۴۲ فرش انبار در ۱۴۰۵/۰۶/۱۵»، «۱۲ فرش ورودی ۳».</summary>
    public string? ScopeNote { get; set; }

    /// <summary>کلید یکتای کلاینت برای ساخت تکرارناپذیر (یکتا در کارگاه وقتی پر است).</summary>
    public Guid? ClientKey { get; set; }

    /// <summary>حذف نرم؛ «برگردان» آن را null می‌کند.</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    public CostCategory Category { get; set; } = null!;

    /// <summary>هدف‌های صریح: فرش‌ها (دامنهٔ «انتخاب» و عکس خرید یکجای ورودی) یا نگله‌ها.</summary>
    public ICollection<CostEntryTarget> Targets { get; set; } = [];

    /// <summary>مبلغ‌های ثابت هر فرش (به ارز کارگاه).</summary>
    public ICollection<CostEntryPin> Pins { get; set; } = [];

    /// <summary>ثبت «هر متر» یا «هر تخته» که هنوز منجمد نشده: کلش با فرش‌های فعلی زنده است.</summary>
    public bool IsLive => Basis is CostBasis.PerM2 or CostBasis.PerPiece && FrozenAt is null;

    /// <summary>ثبت «هر متر» یا «هر تخته» که با رسید یا طرف خدمات منجمد شده.</summary>
    public bool IsFrozen => Basis is CostBasis.PerM2 or CostBasis.PerPiece && FrozenAt is not null;

    /// <summary>رسید است (طرف خدمات یا شمارهٔ رسید دارد) — قاعدهٔ انجماد D.3.</summary>
    public bool IsReceipt => ServiceProviderId is not null || !string.IsNullOrWhiteSpace(ReceiptNo);
}
