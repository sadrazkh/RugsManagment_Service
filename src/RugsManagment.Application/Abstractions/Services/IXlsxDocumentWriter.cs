using RugsManagment.Application.Features.Documents.Composer;

namespace RugsManagment.Application.Abstractions.Services;

/// <summary>
/// نویسندهٔ فایل اکسلِ سند (spec E.7): مدل چیده‌شده (<see cref="RenderModel"/>) را با فرمول‌های زنده و مقدار ذخیره‌شدهٔ
/// دقیق (decimal) در هر خانهٔ فرمول می‌نویسد، تا اکسل و LibreOffice پیش از محاسبهٔ دوباره همان عددها را نشان دهند.
///
/// پیاده‌سازی (<c>ClosedXmlDocumentWriter</c> در Infrastructure) پشت همین رابط است تا اگر روزی ClosedXML کنار رفت، نویسندهٔ
/// OpenXml خالص جایش بنشیند و Application چیزی نفهمد (E.6).
/// </summary>
public interface IXlsxDocumentWriter
{
    /// <summary>فایل xlsx را در <paramref name="output"/> می‌نویسد (جریان باز می‌ماند).</summary>
    Task WriteAsync(RenderModel model, Stream output, CancellationToken cancellationToken = default);
}
