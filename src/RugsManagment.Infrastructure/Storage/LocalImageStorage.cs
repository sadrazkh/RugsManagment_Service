using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RugsManagment.Application.Abstractions.Services;

namespace RugsManagment.Infrastructure.Storage;

/// <summary>
/// ذخیره‌سازی تصاویر روی دیسک محلی.
///
/// مسیر ریشه از کلید <c>Storage:ImagePath</c> خوانده می‌شود؛ در کانتینر با متغیر محیطی
/// <c>Storage__ImagePath=/data/uploads</c> به volume ماندگار اشاره می‌کند (در Dockerfile تنظیم شده).
/// پیش‌فرض محلی کنار کلیدهای Data Protection است تا هر دو دادهٔ ماندگار یک جا باشند.
///
/// عمداً بیرون از wwwroot است تا فایل‌ها به‌صورت مستقیم و بدون بررسی مالکیت کارگاه سرو نشوند.
///
/// چیدمان: {ریشه}/{tenantId}/{rugId}/{guid}.{ext}
/// </summary>
public sealed class LocalImageStorage : IImageStorage
{
    private readonly string _root;
    private readonly ILogger<LocalImageStorage> _logger;

    public LocalImageStorage(
        IConfiguration configuration, IHostEnvironment environment, ILogger<LocalImageStorage> logger)
    {
        _logger = logger;
        _root = configuration["Storage:ImagePath"]
            ?? Path.Combine(environment.ContentRootPath, "App_Data", "uploads");

        Directory.CreateDirectory(_root);
        _logger.LogInformation("محل ذخیرهٔ تصاویر: {Path}", _root);
    }

    public async Task<string> SaveAsync(
        Guid tenantId, Guid rugId, Stream content, string extension, CancellationToken ct = default)
    {
        var folder = FolderFor(tenantId, rugId);
        Directory.CreateDirectory(folder);

        // نام فایل را خودمان می‌سازیم؛ هیچ بخشی از ورودی کاربر در مسیر نمی‌آید
        var safeExtension = SanitizeExtension(extension);
        var fileName = $"{Guid.NewGuid():N}.{safeExtension}";

        await using var target = File.Create(Path.Combine(folder, fileName));
        await content.CopyToAsync(target, ct);

        return fileName;
    }

    public Task DeleteAsync(Guid tenantId, Guid rugId, string fileName, CancellationToken ct = default)
    {
        var path = ResolveExisting(tenantId, rugId, fileName);
        if (path is not null)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException ex)
            {
                // فایل قفل یا حذف‌شده — رکورد دیتابیس مهم‌تر است، عملیات نباید شکست بخورد
                _logger.LogWarning(ex, "حذف فایل تصویر ناموفق بود: {Path}", path);
            }
        }

        return Task.CompletedTask;
    }

    public Task DeleteRugFolderAsync(Guid tenantId, Guid rugId, CancellationToken ct = default)
    {
        var folder = FolderFor(tenantId, rugId);
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "حذف پوشهٔ تصاویر فرش ناموفق بود: {Folder}", folder);
        }

        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(
        Guid tenantId, Guid rugId, string fileName, CancellationToken ct = default)
    {
        var path = ResolveExisting(tenantId, rugId, fileName);
        Stream? stream = path is null
            ? null
            : new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);

        return Task.FromResult(stream);
    }

    // ─────────────────────────────────────────────────────────
    private string FolderFor(Guid tenantId, Guid rugId)
        => Path.Combine(_root, tenantId.ToString("N"), rugId.ToString("N"));

    /// <summary>
    /// مسیر کامل فایل، فقط اگر واقعاً داخل پوشهٔ همان فرش باشد.
    /// این بررسی جلوی مسیرپیمایی (مثل «..\..\appsettings.json») را می‌گیرد،
    /// حتی اگر نام فایل از جایی دستکاری‌شده بیاید.
    /// </summary>
    private string? ResolveExisting(Guid tenantId, Guid rugId, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;

        var folder = Path.GetFullPath(FolderFor(tenantId, rugId));
        var candidate = Path.GetFullPath(Path.Combine(folder, fileName));

        if (!candidate.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return null;

        return File.Exists(candidate) ? candidate : null;
    }

    private static string SanitizeExtension(string extension)
    {
        var trimmed = extension.TrimStart('.').ToLowerInvariant();
        return trimmed is "webp" or "jpg" or "jpeg" or "png" ? trimmed : "bin";
    }
}
