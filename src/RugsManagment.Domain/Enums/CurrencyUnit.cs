namespace RugsManagment.Domain.Enums;

/// <summary>
/// واحد پول نمایشی کارگاه.
///
/// توجه: این فقط برچسب نمایش است. مبالغ در دیتابیس بدون واحد ذخیره می‌شوند و
/// تغییر این مقدار هیچ عددی را تبدیل نمی‌کند — کارگاه باید از ابتدا یکی را انتخاب کند.
/// </summary>
public enum CurrencyUnit
{
    Toman = 0,
    Rial = 1
}
