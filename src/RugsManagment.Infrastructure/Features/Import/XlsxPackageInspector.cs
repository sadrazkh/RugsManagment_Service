using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using RugsManagment.Application.Common;
using RugsManagment.Domain.Common;
namespace RugsManagment.Infrastructure.Features.Import;

/// <summary>حدود فشردگی پیش از بارگذاری XML سنجیده می‌شوند؛ فرمول و پیوند بیرونی هرگز اجرا نمی‌شوند.</summary>
public static class XlsxPackageInspector
{
    public static void Validate(ZipArchive archive)
    {
        if (archive.Entries.Count > 200 || archive.Entries.Sum(e => e.Length) > 100L * 1024 * 1024
            || archive.Entries.Any(e => e.Length > 0 && (e.CompressedLength == 0 || e.Length / (decimal)e.CompressedLength > 100m)))
            throw new DomainValidationException(ImportMessages.Text("E_ZIP_BOMB"), "file");
        if (archive.GetEntry("[Content_Types].xml") is null || archive.GetEntry("xl/workbook.xml") is null
            || archive.Entries.Any(e => e.FullName.Contains("vbaProject", StringComparison.OrdinalIgnoreCase)
                || e.FullName.StartsWith("xl/externalLinks/", StringComparison.OrdinalIgnoreCase))) Invalid();
        var contentTypes = Xml(archive.GetEntry("[Content_Types].xml")!);
        if (contentTypes.Descendants().Attributes("ContentType").Any(a => a.Value.Contains("macroEnabled", StringComparison.OrdinalIgnoreCase))) Invalid();
    }
    public static XDocument Xml(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null, MaxCharactersInDocument = 100L * 1024 * 1024 });
        return XDocument.Load(reader);
    }
    public static void Invalid() => throw new DomainValidationException(ImportMessages.Text("E_NOT_XLSX"), "file");
}
