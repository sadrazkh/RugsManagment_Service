using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// یک سطر از قیمت خرید فرش (spec B.6): «قیمت خرید» هر متر، «دلالی» درصد، «کرایه تا انبار» هر تخته، یا هر مبلغ کل.
///
/// چرا سطر و نه یک ستون: قیمت خرید فرش در بازار از چند جزء ساخته می‌شود که هر کدام مبنای خودش را دارد و با
/// عوض شدن اندازه یا تعداد باید خودبه‌خود دوباره حساب شوند؛ یک عدد تنها این را نگه نمی‌داشت. جمع سطرها
/// <c>RugCostTotal.PurchaseCost</c> را می‌سازد (به‌اضافهٔ سهم خرید یکجا).
///
/// <see cref="Amount"/> را فقط پاس سازگاری (<c>PurchaseLineStage</c>) با قاعدهٔ دسته می‌نویسد
/// (<see cref="Costing.RugCostMath.PurchaseLineAmounts(IReadOnlyList{Costing.PurchaseLineInput}, decimal, int, int)"/>):
/// هر متر همیشه روی اندازهٔ اول، اول برای یک تخته و بعد × تعداد؛ درصد یک بار روی جمع سطرهای غیردرصدی.
/// xmin ندارد: ویرایش سطرها نسخهٔ خودِ فرش را بالا می‌برد و تعارض از همان‌جا گرفته می‌شود.
/// </summary>
public class RugPurchaseLine : BaseEntity, ITenantScoped
{
    /// <summary>طول ستون برچسب.</summary>
    public const int LabelMaxLength = 120;

    /// <summary>برچسب سطری که M2 از قیمت خرید قدیمی هر فرش ساخت (مبنای «کل»).</summary>
    public const string LegacyPurchaseLabel = "قیمت خرید";

    public Guid TenantId { get; set; }

    public Guid RugId { get; set; }

    /// <summary>نوع هزینه (گروه خرید)؛ null برای سطر آزاد یا سطر قدیمیِ M2. نوعِ به‌کاررفته حذف نمی‌شود.</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>برچسب سطر، مثل «قیمت خرید» یا «دلالی».</summary>
    public string Label { get; set; } = string.Empty;

    public CostBasis Basis { get; set; } = CostBasis.Total;

    /// <summary>نرخ (numeric(18,4)): مبلغ برای «کل»، نرخ برای «هر متر»/«هر تخته»، درصد برای «درصد» (۲ یعنی ۲٪).</summary>
    public decimal Rate { get; set; }

    /// <summary>مبلغ حساب‌شده (numeric(18,2)، در MoneyScale) — فقط پاس سازگاری می‌نویسد.</summary>
    public decimal Amount { get; set; }

    public int SortOrder { get; set; }

    public string? Notes { get; set; }

    public Rug Rug { get; set; } = null!;
    public CostCategory? Category { get; set; }
}
