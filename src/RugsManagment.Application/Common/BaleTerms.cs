using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Common;

/// <summary>
/// واژه‌های بستهٔ فرش برای یک کارگاه — همان چهار کلید <c>terms</c> در app-context و نشانه‌های پیام‌ها.
/// </summary>
/// <param name="Bale">«نگله» / «عدل»</param>
/// <param name="Bales">«نگله‌ها» / «عدل‌ها»</param>
/// <param name="BaleOf">پیش از شماره: «نگلهٔ ۳» / «عدل ۳» (برای عدل کسره نوشته نمی‌شود)</param>
/// <param name="BaleFooter">پانویس جدول: «نگله شماره» / «عدل شماره»</param>
public sealed record BaleTermWords(string Bale, string Bales, string BaleOf, string BaleFooter)
{
    /// <summary>نشانه‌های پیام: <c>{bale}</c>، <c>{bales}</c>، <c>{baleOf}</c>، <c>{baleFooter}</c>.</summary>
    public (string Key, object? Value)[] AsTokens() =>
    [
        ("bale", Bale),
        ("bales", Bales),
        ("baleOf", BaleOf),
        ("baleFooter", BaleFooter)
    ];
}

/// <summary>
/// تبدیل ترجیح <see cref="BaleTerm"/> به واژه‌ها. هیچ متنی «نگله» را ثابت نمی‌نویسد؛ همه از این‌جا یا از
/// <c>terms</c> در app-context می‌خوانند تا کارگاهی که «عدل» می‌گوید همه‌جا «عدل» ببیند.
/// </summary>
public static class BaleTerms
{
    public static readonly BaleTermWords Negleh = new("نگله", "نگله‌ها", "نگلهٔ", "نگله شماره");

    public static readonly BaleTermWords Adl = new("عدل", "عدل‌ها", "عدل", "عدل شماره");

    public static BaleTermWords For(BaleTerm term) => term == BaleTerm.Adl ? Adl : Negleh;
}
