using RugsManagment.Application.Common;

namespace RugsManagment.Web.Home;

/// <summary>یک گام کار روزانه، برای راهنمای شروع خانه و «کارت راهنما».</summary>
/// <param name="Key">شناسهٔ پایدار گام</param>
/// <param name="Title">نام دکمه‌ای که بازرگان روی صفحه می‌بیند</param>
/// <param name="Text">یک جملهٔ ساده دربارهٔ این‌که آن‌جا چه می‌کند</param>
/// <param name="Icon">نماد همان دکمه در <c>icons.svg</c></param>
public sealed record MerchantStep(string Key, string Title, string Text, string Icon);

/// <summary>
/// پنج گام کار بازرگان (A.6): ثبت فرش، انبار، نگله‌بندی، هزینه، اکسل و چاپ — یک متن برای راهنمای شروعِ
/// خانه و «کارت راهنما»ی چاپی، تا دو جا دو حرف نزنند.
///
/// متن از سیاست بیننده پیروی می‌کند (H.3): انباردار گام هزینه را نمی‌بیند و به جای «محمولهٔ تازه بسازید»
/// می‌خواند که محموله را بازرگان می‌سازد. واژهٔ نگله از ترجیح کارگاه می‌آید («عدل‌بندی» برای کارگاهی که
/// «عدل» می‌گوید). متن‌ها این‌جا در C# است نه در view، چون آزمون فرهنگ واژه «محموله» را در viewهای بیرون
/// از بخش محموله ممنوع می‌کند و این گام‌ها عمداً دربارهٔ محموله‌اند.
/// </summary>
public static class MerchantSteps
{
    /// <summary>جملهٔ پایانی هر دو راهنما.</summary>
    public const string HelpTip = "هر جا گیر کردید، دکمهٔ «؟ راهنما» بالای صفحه را بزنید.";

    public static IReadOnlyList<MerchantStep> For(bool canBusiness, BaleTermWords terms)
    {
        var steps = new List<MerchantStep>
        {
            new("intake", "ثبت فرش تازه",
                "فرش‌هایی را که از فروشنده رسید بنویسید: شماره، اندازه و نام فروشنده.", "plus"),
            new("stock", "انبار",
                "فرشی که در هیچ محموله‌ای نیست در انبار است؛ با زدن شماره‌اش پیدا می‌شود.", "rug"),
            new("baling", $"{terms.Bale}‌بندی",
                canBusiness
                    ? $"محمولهٔ تازه بسازید، شمارهٔ هر فرش را بزنید تا در {terms.BaleOf} جاری برود، و اندازهٔ نهایی را بنویسید."
                    : $"شمارهٔ هر فرش را بزنید تا در {terms.BaleOf} جاری برود، و اندازهٔ نهایی را بنویسید. محمولهٔ تازه را بازرگان می‌سازد.",
                "bale")
        };

        if (canBusiness)
        {
            steps.Add(new("costs", "ثبت هزینه",
                "رسید هر هزینه را بزنید: نوع، مبلغ و این‌که برای کدام فرش‌هاست؛ تقسیمش بین فرش‌ها خودکار است.", "coins"));
        }

        steps.Add(new("output", "اکسل و چاپ",
            canBusiness
                ? "از هر محموله اکسل یا چاپ بگیرید؛ وقتی همه‌چیز درست بود «قطعی کن» را بزنید."
                : "از هر محموله اکسل یا چاپ بگیرید.",
            "printer"));

        return steps;
    }
}
