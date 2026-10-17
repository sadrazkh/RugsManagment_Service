namespace RugsManagment.Domain.Enums;

/// <summary>
/// واژه‌ای که کارگاه برای بستهٔ فرش در محموله به کار می‌برد. همهٔ متن‌ها، ستون‌ها و خروجی‌ها
/// واژه را از همین ترجیح می‌گیرند (<c>terms</c> در app-context)، نه از متن ثابت.
/// </summary>
public enum BaleTerm
{
    /// <summary>«نگله» / «نگلهٔ ۳» / «نگله شماره»</summary>
    Negleh = 0,

    /// <summary>«عدل» / «عدل ۳» (کسرهٔ نوشته‌شده ندارد) / «عدل شماره»</summary>
    Adl = 1
}
