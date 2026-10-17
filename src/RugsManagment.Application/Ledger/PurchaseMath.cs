using RugsManagment.Domain.Costing;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Ledger;

/// <summary>کاری که مسیر قدیمی (<c>UpdateRugRequest.PurchaseCost</c>) با سطرهای خرید یک فرش می‌کند (spec I.3).</summary>
public enum LegacyPurchaseAction
{
    /// <summary>فرش سطر خرید ندارد → یک سطر «کل» با برچسب «قیمت خرید» ساخته می‌شود.</summary>
    CreateLine = 0,

    /// <summary>دقیقاً یک سطر دارد و «کل» است → نرخ همان سطر عوض می‌شود.</summary>
    UpdateRate = 1,

    /// <summary>چندسطری است ولی مبلغ فرستاده‌شده همان جمع سطرهاست (رفت‌وبرگشت فرم قدیمی) → دست نمی‌خورد.</summary>
    Ignore = 2,

    /// <summary>چندسطری است و مبلغ دیگری فرستاده شده → ۴۰۹ «قیمت خرید این فرش چندسطری است».</summary>
    Conflict = 3
}

/// <summary>
/// حساب درون‌حافظهٔ سطرهای خرید یک فرش — برای پیش‌نمایش (ویرایشگر سطرها، ستون‌های خرید جدول)، محاسبهٔ جایگزین وقتی
/// ردیف <c>RugCostTotal</c> هنوز ساخته نشده، و قاعده‌های مسیر قدیمی. فرمول‌ها خودشان فقط در <see cref="RugCostMath"/> اند
/// (همتای C#ِ CostSql)؛ این کلاس فقط آن‌ها را روی موجودیت‌ها به کار می‌برد، پس پیش‌نمایش همان عددی است که پاس
/// سازگاری می‌نویسد.
/// </summary>
public static class PurchaseMath
{
    /// <summary>
    /// مبلغ هر سطر، هم‌ترتیب با <paramref name="lines"/>: هر متر روی مساحت یک تخته در اندازهٔ اول، اول برای یک تخته گرد
    /// و بعد × تعداد؛ هر تخته ROUND(rate, s) × تعداد؛ کل ROUND(rate, s)؛ درصد یک بار روی جمع سطرهای غیردرصدی.
    /// </summary>
    public static IReadOnlyList<decimal> Amounts(Rug rug, IReadOnlyList<RugPurchaseLine> lines, int moneyScale)
        => RugCostMath.PurchaseLineAmounts(rug, lines, moneyScale);

    /// <summary>جمع مبلغ سطرها (همان <c>RugDto.PurchaseCost</c>، بی سهم خرید یکجا).</summary>
    public static decimal Total(Rug rug, IReadOnlyList<RugPurchaseLine> lines, int moneyScale)
        => Amounts(rug, lines, moneyScale).Sum();

    /// <summary>
    /// قاعدهٔ <c>UpdateRugRequest.PurchaseCost</c> (I.3) بر اساس سطرهای فعلی (با مبلغ ذخیره‌شده‌شان). هرگز سطرهای
    /// چندگانه را به یک عدد نمی‌کوبد.
    /// </summary>
    public static LegacyPurchaseAction ClassifyLegacyPurchaseCost(IReadOnlyCollection<RugPurchaseLine> lines, decimal purchaseCost)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
            return LegacyPurchaseAction.CreateLine;
        if (lines.Count == 1 && lines.First().Basis == CostBasis.Total)
            return LegacyPurchaseAction.UpdateRate;
        return lines.Sum(l => l.Amount) == purchaseCost ? LegacyPurchaseAction.Ignore : LegacyPurchaseAction.Conflict;
    }
}
