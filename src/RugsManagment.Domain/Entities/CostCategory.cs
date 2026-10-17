using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// «نوع هزینه» هر کارگاه (spec B.7): قیمت خرید، دلالی، شستشو، کرایهٔ خارجی، گمرک، …
///
/// هر نوع گروه خودش (خرید یا هزینه) و پیش‌فرض‌های ثبت را دارد (مبنا، نرخ، ارز، روش تقسیم، پایهٔ متراژ، دامنه)؛
/// این پیش‌فرض‌ها فقط فرم ثبت را پر می‌کنند و ثبت‌های قبلی را عوض نمی‌کنند. نوعی که به کار رفته حذف نمی‌شود
/// (غیرفعال می‌شود) و گروهش دیگر عوض نمی‌شود، چون مبلغ‌هایش در یکی از دو سطل خرید/هزینه جمع شده‌اند.
/// <see cref="SeedKey"/> نوع‌های ساخته‌شده توسط برنامه را (مثل <c>purchase.price</c>) پیدا می‌کند تا seed تکرار نشود.
/// هم‌زمانی با xmin.
/// </summary>
public class CostCategory : BaseEntity, ITenantScoped
{
    /// <summary>طول ستون نام.</summary>
    public const int NameMaxLength = 120;

    /// <summary>طول ستون کلید seed.</summary>
    public const int SeedKeyMaxLength = 60;

    /// <summary>طول ستون نام آیکون.</summary>
    public const int IconMaxLength = 60;

    public Guid TenantId { get; set; }

    /// <summary>کلید ثابت نوع‌هایی که برنامه می‌سازد (<c>purchase.price</c>، <c>purchase.broker</c>، …)؛ یکتا در کارگاه.</summary>
    public string? SeedKey { get; set; }

    public string Name { get; set; } = string.Empty;

    public CostGroup Group { get; set; } = CostGroup.Expense;

    /// <summary>نام آیکون (باید در <c>icons.svg</c> باشد).</summary>
    public string? Icon { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    // ── پیش‌فرض‌های ثبت ─────────────────────────────────────────────────

    public CostBasis DefaultBasis { get; set; } = CostBasis.Total;

    /// <summary>نرخ پیش‌فرض (numeric(18,4))؛ null یعنی خالی. وجودش «مبلغ ذخیره‌شده» است و تعداد رقم اعشار را قفل می‌کند.</summary>
    public decimal? DefaultRate { get; set; }

    /// <summary>ارز پیش‌فرض؛ null یعنی ارز کارگاه.</summary>
    public CurrencyUnit? DefaultCurrency { get; set; }

    public AllocationMethod DefaultMethod { get; set; } = AllocationMethod.ByArea;

    public AreaBasis DefaultAreaBasis { get; set; } = AreaBasis.Effective;

    public CostScope? DefaultScope { get; set; }

    /// <summary>در جدول ورود فرش ستون خرید دارد (مثل «قیمت خرید» و «دلالی»).</summary>
    public bool ShowInIntake { get; set; }

    /// <summary>
    /// نوع مرحله‌ای که همین هزینه است (مثلاً «شستشو»)؛ اگر فرش آن مرحله را با هزینه هم داشته باشد، هشدار
    /// <c>W_STEP_DOUBLE</c> (دوبار شمردن). با حذف نوع مرحله خالی می‌شود.
    /// </summary>
    public Guid? StepTypeId { get; set; }
}
