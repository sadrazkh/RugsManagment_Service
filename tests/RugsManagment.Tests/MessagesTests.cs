using System.Text.RegularExpressions;
using RugsManagment.Application.Common;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Tests;

/// <summary>
/// پیام‌ها (spec H.3b): هر کد یک پیام، هیچ کد تکراری، و هیچ حرف لاتینی بیرون از نشانه‌ها — بازرگان نباید
/// «SKU» یا «null» ببیند. فهرست با reflection از همهٔ فایل‌های partial ساخته می‌شود، پس پیام‌های فازهای بعد هم
/// خودبه‌خود همین‌جا سنجیده می‌شوند.
/// </summary>
public partial class MessagesTests
{
    [Fact]
    public void Registry_HasNoDuplicateCodes()
        => Assert.Empty(Messages.DuplicateCodes);

    [Fact]
    public void Registry_HasTheCoreCodes()
    {
        Assert.True(Messages.All.Count >= 31);
        foreach (var code in new[] { "E_FORBIDDEN", "E_MONEY_FORBIDDEN", "E_MONEY_LOCKED", "E_LAST_ADMIN", "E_SELF_DEMOTE", "E_INVALID_ROLE" })
            Assert.NotNull(Messages.Find(code));
    }

    [Fact]
    public void EveryCode_FollowsTheNamingRule()
        => Assert.All(Messages.All.Keys, code => Assert.Matches(CodePattern(), code));

    [Fact]
    public void EveryMessage_IsPersian_WithNoLatinOutsideTokens()
    {
        var offenders = Messages.All.Values
            .Where(t => string.IsNullOrWhiteSpace(t.Text) || LatinLetter().IsMatch(TokenPattern().Replace(t.Text, "")))
            .Select(t => $"{t.Code}: {t.Text}")
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void EveryMessage_UsesOnlyWellFormedTokens()
    {
        // «{» بدون نشانهٔ درست (مثل «{ n}» یا «{n») بی‌صدا به کاربر می‌رسید
        var offenders = Messages.All.Values
            .Where(t => TokenPattern().Replace(t.Text, "").IndexOfAny(['{', '}']) >= 0)
            .Select(t => t.Code)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Fill_WritesNumbersWithPersianDigitsAndSeparators()
    {
        var text = Messages.OutOfRange.Format(("label", "تلورانس"), ("min", 0), ("max", 10_000_000m));
        Assert.Equal("«تلورانس» باید از ۰ تا ۱۰٬۰۰۰٬۰۰۰ باشد.", text);
        Assert.Equal("۲٫۵", Messages.FormatNumber(2.5m));
        Assert.Equal("-۱۲٬۳۴۵", Messages.FormatNumber(-12_345m)); // علامت منفی ASCII، نه نویسهٔ فرهنگ fa
    }

    [Fact]
    public void Fill_LeavesUnknownTokensVisible()
        => Assert.Equal("«{label}» را بنویسید.", Messages.Required.Format(("other", "x")));

    [Fact]
    public void Text_OfAnUnknownCode_IsTheGenericMessage_NotTheCode()
        => Assert.Equal(Messages.ServerError.Text, Messages.Text("E_DOES_NOT_EXIST"));

    [Fact]
    public void BaleTerms_FillBaleTokens()
    {
        var template = new MessageTemplate("I_TEST_ONLY", "{baleOf} ۳ · {baleFooter} · {bales}");
        Assert.Equal("نگلهٔ ۳ · نگله شماره · نگله‌ها", template.Format(BaleTerms.Negleh));
        Assert.Equal("عدل ۳ · عدل شماره · عدل‌ها", template.Format(BaleTerms.For(BaleTerm.Adl)));
    }

    [GeneratedRegex(@"^[EWIQ]_[A-Z0-9_]+$")]
    private static partial Regex CodePattern();

    [GeneratedRegex(@"\{[A-Za-z][A-Za-z0-9]*\}")]
    private static partial Regex TokenPattern();

    [GeneratedRegex("[A-Za-z]")]
    private static partial Regex LatinLetter();
}
