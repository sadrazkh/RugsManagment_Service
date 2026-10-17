using Microsoft.Extensions.Configuration;
using RugsManagment.Application.Abstractions.Services;
namespace RugsManagment.Infrastructure.Storage;

/// <summary>نام و مسیر فایل صرفاً از شناسه‌های سرور ساخته می‌شود، نه نامی که مرورگر فرستاده است.</summary>
public sealed class ImportFileStorage(IConfiguration configuration) : IImportFileStorage
{
    private string PathFor(Guid tenantId, Guid id)
    {
        var configuredRoot = configuration["Storage:DataRoot"];
        var root = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(AppContext.BaseDirectory, "App_Data") : configuredRoot;
        return Path.Combine(Path.GetFullPath(root), "imports", tenantId.ToString("N"), id.ToString("N") + ".xlsx");
    }
    public async Task<string> SaveAsync(Guid tenantId, Guid jobId, Stream input, CancellationToken ct = default)
    {
        var path = PathFor(tenantId, jobId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var created = false;
        try
        {
            await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            created = true;
            await input.CopyToAsync(output, ct);
        }
        catch { if (created) File.Delete(path); throw; }
        return path;
    }
    public Task<Stream> OpenAsync(Guid tenantId, Guid jobId, CancellationToken ct = default)
        => Task.FromResult<Stream>(new FileStream(PathFor(tenantId, jobId), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true));
    public Task DeleteAsync(Guid tenantId, Guid jobId, CancellationToken ct = default)
    { File.Delete(PathFor(tenantId, jobId)); return Task.CompletedTask; }
}
