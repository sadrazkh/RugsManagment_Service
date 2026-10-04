using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.DTOs.Rugs;

/// <summary>
/// یک سطر قیمت خرید فرش (B.6) — فقط برای Business.
///
/// در پاسخ: <see cref="Amount"/> مبلغی است که پاس سازگاری با قاعدهٔ دسته حساب کرده (هر متر روی اندازهٔ اول، اول برای یک
/// تخته گرد و بعد × تعداد؛ درصد یک بار روی جمع سطرهای غیردرصدی). در درخواست (<see cref="ReplacePurchaseLinesRequest"/>):
/// <see cref="Id"/> = null یعنی سطر تازه، و <see cref="Amount"/> نادیده گرفته می‌شود چون همیشه سرور حسابش می‌کند —
/// اگر از کاربر پذیرفته می‌شد، دو منبع برای یک عدد داشتیم.
/// </summary>
/// <param name="Id">شناسهٔ سطر؛ null برای سطر تازه.</param>
/// <param name="CategoryId">نوع هزینهٔ گروه «خرید»؛ null برای سطر آزاد یا سطر قدیمیِ M2 («قیمت خرید»).</param>
/// <param name="Label">برچسب، مثل «قیمت خرید» یا «دلالی» (حداکثر ۱۲۰ حرف).</param>
/// <param name="Basis">کل، هر متر، هر تخته یا درصد.</param>
/// <param name="Rate">مبلغ برای «کل»، نرخ برای «هر متر»/«هر تخته»، درصد برای «درصد» (۲ یعنی ۲٪).</param>
/// <param name="Amount">مبلغ حساب‌شده در MoneyScale (فقط خواندنی).</param>
/// <param name="SortOrder">ترتیب نمایش.</param>
/// <param name="Notes">یادداشت آزاد.</param>
public record PurchaseLineDto(
    Guid? Id,
    Guid? CategoryId,
    string Label,
    CostBasis Basis,
    decimal Rate,
    decimal Amount,
    int SortOrder,
    string? Notes);

/// <summary>
/// <c>PUT /api/rugs/{id}/purchase-lines</c> (Business): همهٔ سطرها یک‌جا جایگزین می‌شوند (سطری که نیامده حذف می‌شود).
///
/// <see cref="Version"/> نسخهٔ خودِ فرش است، نه سطرها (سطر xmin ندارد): ذخیره <c>Rug.UpdatedAt</c> را هم عوض می‌کند تا
/// xmin فرش جلو برود و دو PUT با یک نسخه دقیقاً یک ۴۰۹ بگیرند (I.2، L.2).
/// </summary>
public record ReplacePurchaseLinesRequest(
    IReadOnlyList<PurchaseLineDto> Lines,
    uint Version);

/// <summary>
/// پاسخ <c>PUT /api/rugs/{id}/purchase-lines</c>: سطرها با مبلغ تازه، همان پاس و همان پاسخ (D.4 «یک درخواست، عددهای تازه»).
/// </summary>
/// <param name="Lines">سطرها به ترتیب <see cref="PurchaseLineDto.SortOrder"/> با <see cref="PurchaseLineDto.Amount"/> حساب‌شده.</param>
/// <param name="PurchaseCost">جمع مبلغ سطرها (همان <see cref="RugDto.PurchaseCost"/>؛ بی سهم «خرید یکجا»).</param>
/// <param name="Version">نسخهٔ تازهٔ فرش برای ذخیرهٔ بعدی.</param>
/// <param name="Costs">خلاصهٔ تازهٔ بهای تمام‌شده تا کارت هزینه‌های صفحهٔ جزئیات بی‌درخواست دوم به‌روز شود.</param>
public record PurchaseLinesDto(
    IReadOnlyList<PurchaseLineDto> Lines,
    decimal PurchaseCost,
    uint Version,
    RugCostSummaryDto? Costs = null);

/// <summary>
/// یک گزینهٔ «نوع» برای سطر خرید در ویرایشگر سطرها و ستون‌های خرید جدول ورود: نوع‌های فعال گروه «خرید».
///
/// جدا از <c>CostCategoryDto</c> (فاز هزینه‌ها) است چون ویرایشگر خرید از فاز ۲ کار می‌کند و فقط همین چند عضو را لازم دارد:
/// نام برای برچسب و مبنا/نرخ پیش‌فرض برای سطر تازه.
/// </summary>
/// <param name="Id">شناسهٔ نوع هزینه.</param>
/// <param name="Name">نام، که برچسب سطر تازه هم می‌شود.</param>
/// <param name="SeedKey">کلید نوع‌های پیش‌ساخته (<c>purchase.price</c>، <c>purchase.broker</c>، <c>purchase.freightIn</c>)؛ null برای نوع ساخت کاربر.</param>
/// <param name="DefaultBasis">مبنای پیش‌فرض سطر تازه.</param>
/// <param name="DefaultRate">نرخ پیش‌فرض (مثلاً ۲ برای دلالی)؛ null یعنی ندارد.</param>
/// <param name="ShowInIntake">در جدول ورود ستون دارد.</param>
/// <param name="SortOrder">ترتیب نمایش، که ترتیب سطر تازه هم می‌شود.</param>
public record PurchaseCategoryOptionDto(
    Guid Id,
    string Name,
    string? SeedKey,
    CostBasis DefaultBasis,
    decimal? DefaultRate,
    bool ShowInIntake,
    int SortOrder);
