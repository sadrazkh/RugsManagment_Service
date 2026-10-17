using RugsManagment.Application.Common;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Tests.P2App;

/// <summary>
/// پیام‌های فرش فاز ۲ (H.3b): هر خطایی که دامنه یا پاس سازگاری خودش متن می‌سازد همان متن را در <c>Messages.fa.Rugs</c>
/// دارد (جزیره همان کد را از <c>lib/messages/rugs.ts</c> می‌خواند)، پس اگر کسی یک طرف را عوض کند همین‌جا می‌شکند.
/// </summary>
public class RugMessagesTests
{
    public static TheoryData<string, string> DomainTexts => new()
    {
        { SerialNumber.FormatErrorCode, SerialNumber.FormatErrorMessage },
        { SerialNumber.ReversedErrorCode, SerialNumber.ReversedErrorMessage },
        { SerialNumber.SpanErrorCode, SerialNumber.SpanErrorMessage },
        { SerialNumber.TooLongErrorCode, SerialNumber.TooLongErrorMessage },
        { SerialNumber.QuantityErrorCode, SerialNumber.QuantityErrorTemplate },
        { Rug.QuantityErrorCode, Rug.QuantityErrorMessage },
        { Rug.FinalSizeSetErrorCode, Rug.FinalSizeSetErrorMessage },
        { SerialOverlapException.Code, SerialOverlapException.OverlapMessage },
        { "E_SERIAL_TAKEN", SerialOverlapException.TakenMessage },
        { Dimension.ErrorCode, Dimension.ErrorMessage }
    };

    [Theory]
    [MemberData(nameof(DomainTexts))]
    public void DomainText_HasTheSameMessage(string code, string text)
    {
        var template = Messages.Find(code);
        Assert.NotNull(template);
        Assert.Equal(text, template.Text);
    }

    [Fact]
    public void QuantityTemplate_FillsLikeTheDomain()
    {
        var domain = SerialNumber.Parse("3911")!.QuantityError(50).Message;
        var app = Messages.SerialQuantity.Format(("serial", "۳۹۱۱"), ("count", 1), ("qty", 50));
        Assert.Equal(domain, app);
    }

    [Fact]
    public void LegacyMultiLine_IsTheSpecText()
        => Assert.StartsWith("قیمت خرید این فرش چندسطری است؛ از بخش «خرید» ویرایش کنید", Messages.PurchaseMultiLine.Text);
}
