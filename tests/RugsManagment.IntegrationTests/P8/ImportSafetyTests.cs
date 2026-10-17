using System.IO.Compression;
using System.Text;
using RugsManagment.Application.Common;
using RugsManagment.Domain.Common;
using RugsManagment.Infrastructure.Features.Import;
using Microsoft.Extensions.Configuration;
using RugsManagment.Infrastructure.Storage;
namespace RugsManagment.IntegrationTests.P8;
public sealed class ImportSafetyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UnconfiguredStorageRoot_RoundTripsFileUnderApplicationData(string? root)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Storage:DataRoot"] = root }).Build();
        var storage = new ImportFileStorage(configuration);
        var tenantId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var payload = Encoding.UTF8.GetBytes("storage round trip");
        string? path = null;
        try
        {
            path = await storage.SaveAsync(tenantId, jobId, new MemoryStream(payload));
            Assert.Equal(Path.Combine(AppContext.BaseDirectory, "App_Data", "imports",
                tenantId.ToString("N"), jobId.ToString("N") + ".xlsx"), path);
            await using var opened = await storage.OpenAsync(tenantId, jobId);
            using var result = new MemoryStream();
            await opened.CopyToAsync(result);
            Assert.Equal(payload, result.ToArray());
        }
        finally
        {
            await storage.DeleteAsync(tenantId, jobId);
            if (path is not null) Directory.Delete(Path.GetDirectoryName(path)!);
        }
        Assert.False(File.Exists(path));
    }
    [Fact]
    public async Task NonZip_IsRejectedBeforeXml()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not excel"));
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => new ClosedXmlWorkbookReader().ReadAsync(stream));
        Assert.Equal(ImportMessages.Text("E_NOT_XLSX"), ex.Message);
    }
    [Fact]
    public async Task MoreThan200ZipEntries_IsRejectedBeforeParsing()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            for (var i = 0; i < 201; i++) zip.CreateEntry($"{i}.xml");
        stream.Position = 0;
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => new ClosedXmlWorkbookReader().ReadAsync(stream));
        Assert.Equal(ImportMessages.Text("E_ZIP_BOMB"), ex.Message);
    }
    [Fact]
    public async Task CompressionBomb_IsRejectedBeforeParsing()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        { await using var data = zip.CreateEntry("large.xml", CompressionLevel.SmallestSize).Open(); await data.WriteAsync(new byte[1024 * 1024]); }
        stream.Position = 0;
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => new ClosedXmlWorkbookReader().ReadAsync(stream));
        Assert.Equal(ImportMessages.Text("E_ZIP_BOMB"), ex.Message);
    }
    [Theory]
    [InlineData("xl/vbaProject.bin")]
    [InlineData("xl/externalLinks/externalLink1.xml")]
    public async Task MacroAndExternalLinks_AreNeverOpened(string forbidden)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            foreach (var name in new[] { "[Content_Types].xml", "xl/workbook.xml", forbidden })
            { await using var data = zip.CreateEntry(name).Open(); await data.WriteAsync(Encoding.UTF8.GetBytes("<root/>")); }
        }
        stream.Position = 0;
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => new ClosedXmlWorkbookReader().ReadAsync(stream));
        Assert.Equal(ImportMessages.Text("E_NOT_XLSX"), ex.Message);
    }
}
