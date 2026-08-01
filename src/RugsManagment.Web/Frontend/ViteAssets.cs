using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace RugsManagment.Web.Frontend;

/// <summary>
/// پل بین Razor و خروجی build فرانت (Vite islands).
///
/// دو حالت:
///   • تولید (پیش‌فرض): فایل‌های هش‌دار را از wwwroot/dist/.vite/manifest.json می‌خواند.
///     هیچ Node یا سرور جداگانه‌ای لازم نیست.
///   • توسعه: اگر در appsettings کلید "Vite:DevServer" ست شود (مثلاً http://localhost:5174)
///     تگ‌ها به سرور Vite با HMR اشاره می‌کنند.
///
/// در layout فقط یک بار صدا زده می‌شود: @ViteAssets.RenderHead(...)
/// </summary>
public sealed class ViteAssets(IWebHostEnvironment env, IConfiguration config)
{
    private const string EntryKey = "src/main.ts";
    private readonly string _manifestPath = Path.Combine(env.WebRootPath, "dist", ".vite", "manifest.json");
    private Dictionary<string, ManifestEntry>? _cache;

    private string? DevServer => config["Vite:DevServer"]?.TrimEnd('/');

    /// <summary>تگ‌های &lt;link&gt; و &lt;script type=module&gt; لازم برای بارگذاری جزیره‌ها.</summary>
    public IHtmlContent RenderHead(IHtmlHelper _)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(DevServer))
        {
            // حالت توسعه: HMR از سرور Vite
            sb.AppendLine($"<script type=\"module\" src=\"{DevServer}/dist/@vite/client\"></script>");
            sb.AppendLine($"<script type=\"module\" src=\"{DevServer}/dist/{EntryKey}\"></script>");
            return new HtmlString(sb.ToString());
        }

        var manifest = LoadManifest();
        if (manifest is null || !manifest.TryGetValue(EntryKey, out var entry))
        {
            // build هنوز اجرا نشده — پیام واضح به‌جای شکست بی‌صدا
            return new HtmlString(
                "<!-- فرانت build نشده: در پوشهٔ ClientApp دستور `npm install && npm run build` را اجرا کنید -->");
        }

        foreach (var css in entry.Css ?? [])
            sb.AppendLine($"<link rel=\"stylesheet\" href=\"/dist/{css}\" />");

        sb.AppendLine($"<script type=\"module\" src=\"/dist/{entry.File}\"></script>");
        return new HtmlString(sb.ToString());
    }

    private Dictionary<string, ManifestEntry>? LoadManifest()
    {
        // در تولید یک بار کش می‌شود؛ در توسعه هر بار خوانده می‌شود تا build جدید دیده شود
        if (_cache is not null && env.IsProduction())
            return _cache;

        if (!File.Exists(_manifestPath))
            return null;

        var json = File.ReadAllText(_manifestPath);
        var parsed = JsonSerializer.Deserialize<Dictionary<string, ManifestEntry>>(json, JsonOpts);
        _cache = parsed;
        return parsed;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class ManifestEntry
    {
        public string File { get; set; } = string.Empty;
        public bool IsEntry { get; set; }
        public string[]? Css { get; set; }
    }
}
