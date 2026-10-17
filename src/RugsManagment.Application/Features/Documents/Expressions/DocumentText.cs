using System.Text;
using System.Xml;

namespace RugsManagment.Application.Features.Documents.Expressions;

/// <summary>متن واردشده از اکسل یا اسکنر باید XML معتبر بماند؛ حروف جفتی مثل شکلک حفظ می‌شوند.</summary>
public static class DocumentText
{
    public static string XmlSafe(string text)
    {
        var result = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (XmlConvert.IsXmlChar(c)) result.Append(c);
            else if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                result.Append(c).Append(text[++i]);
        }
        return result.ToString();
    }
}
