using System.Text.RegularExpressions;

namespace RugsManagment.Tests.P1Fix;

/// <summary>
/// یافته‌های بازبینی فاز ۱ که در خودِ فایل‌های منبع دیده می‌شوند (نه در رفتار یک سرویس): صفحهٔ آفلاین و پیش‌کش Service
/// Worker، اندازهٔ دکمه‌های سرصفحه با نوشتهٔ درشت، و واژهٔ «ورودی» برای RugBatch در پیام‌های سرور.
/// </summary>
public partial class P1FixSourceTests
{
    // ── صفحهٔ آفلاین (یافته: offline.html فونت را از نشانی‌ای می‌خواست که هرگز کش نمی‌شد) ──────────

    /// <summary>
    /// offline.html وقتی نشان داده می‌شود که شبکه نیست؛ پس هر نشانی هم‌مبدأ که می‌خواهد (فونت، آیکون، شیوه‌نامه) باید در
    /// PRECACHE همان sw.js باشد. کش با نشانی کامل (همراه پرس‌وجو) کلید می‌خورد، پس «همان فایل با ?v=…» حساب نیست.
    /// </summary>
    [Fact]
    public void OfflinePage_EveryLocalUrl_IsPrecached()
    {
        var web = WebRoot();
        if (web is null) return;

        var offline = File.ReadAllText(Path.Combine(web, "wwwroot", "offline.html"));
        var precache = Precache(File.ReadAllText(Path.Combine(web, "wwwroot", "sw.js")));
        Assert.Contains("/offline.html", precache);

        var urls = LocalUrls().Matches(offline).Select(m => m.Groups["url"].Value).Distinct().ToList();
        Assert.Contains("/fonts/Vazirmatn-Variable.woff2", urls); // صفحه واقعاً فونت خودمیزبان را می‌خواهد

        var missing = urls.Where(u => !precache.Contains(u)).ToList();
        Assert.True(missing.Count == 0, "offline.html نشانی‌هایی می‌خواهد که پیش‌کش نشده‌اند: " + string.Join("، ", missing));
    }

    /// <summary>هر تغییر PRECACHE نسخهٔ کش را بالا می‌برد؛ v4 همان نسخه‌ای است که فونت صفحهٔ آفلاین را نداشت.</summary>
    [Fact]
    public void ServiceWorker_CacheVersion_WasBumped()
    {
        var web = WebRoot();
        if (web is null) return;

        var sw = File.ReadAllText(Path.Combine(web, "wwwroot", "sw.js"));
        var version = CacheVersion().Match(sw);
        Assert.True(version.Success, "const CACHE = 'rugs-static-vN' در sw.js پیدا نشد.");
        Assert.True(int.Parse(version.Groups["n"].Value) >= 5, $"نسخهٔ کش {version.Value} است؛ بعد از تغییر PRECACHE باید بالا برود.");
    }

    // ── دکمه‌های سرصفحه (یافته: «آ++» روی گوشی ۳۷۵ پیکسلی دکمهٔ منو را ۳۹ پیکسل می‌کرد) ─────────────

    [Theory]
    [InlineData("nav-open")]
    [InlineData("user-menu-button")]
    public void HeaderButtons_DoNotShrink_BelowTheTouchTarget(string id)
    {
        var web = WebRoot();
        if (web is null) return;

        var layout = File.ReadAllText(Path.Combine(web, "Views", "Shared", "_Layout.cshtml"));
        var button = Regex.Match(layout, $"""<button[^>]*id="{id}"[^>]*>""", RegexOptions.Singleline);
        Assert.True(button.Success, $"دکمهٔ #{id} در layout پیدا نشد.");

        var classes = ClassAttribute().Match(button.Value).Groups["c"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // در ردیف flex سرصفحه، بی shrink-0 دکمه کوچک‌تر از ۴۴ پیکسل می‌شود وقتی نوشته درشت است
        Assert.Contains("shrink-0", classes);
    }

    // ── «ورودی» نه «گروه» (یافته: پیام‌های سرور هنوز RugBatch را «گروه» می‌نامیدند) ─────────────────

    /// <summary>
    /// A.2: RugBatch همه‌جا «ورودی» است؛ «گروه» قدیمی دیگر به کاربر نمی‌رسد. «گروهی» (ویرایش گروهی، تکمیل گروهی =
    /// چندتایی) واژهٔ دیگری است و مجاز است. رشته‌های C# در Application و Web (پیام خطا، خلاصهٔ تاریخچه) و سطرهای
    /// غیر توضیحِ viewها و جزیره‌ها سنجیده می‌شوند؛ GlossaryTests فقط viewها و جزیره‌ها را می‌سنجید و «گروه یافت نشد.»
    /// سرویس ورودی‌ها و «(گروه)» خط تاریخچهٔ ویرایش گروهی از چشمش افتاده بودند.
    /// </summary>
    [Fact]
    public void RugBatch_IsNeverCalledGroup_InUserFacingText()
    {
        var root = Fixtures.RepositoryRoot();
        if (root is null) return;

        var offenders = new List<string>();
        foreach (var (file, texts) in UserFacingTexts(root.FullName))
        {
            foreach (var (line, text) in texts)
            {
                if (OldBatchWord().IsMatch(text))
                    offenders.Add($"{file}:{line}: {text}");
            }
        }

        Assert.True(offenders.Count == 0, "«گروه» برای ورودی:\n" + string.Join("\n", offenders));
    }

    /// <summary>خودِ قاعده: «گروه» و «گروه‌ها» گرفته می‌شوند، «گروهی» نه.</summary>
    [Theory]
    [InlineData("گروه یافت نشد.", true)]
    [InlineData("ویرایش گروهی ۱ فرش (ورودی).", false)]
    [InlineData("گروه‌ها", true)]
    [InlineData("تکمیل شد (گروهی).", false)]
    public void OldBatchWord_Rule(string text, bool flagged) => Assert.Equal(flagged, OldBatchWord().IsMatch(text));

    // ── کمکی‌ها ────────────────────────────────────────────────────────────

    private static string? WebRoot()
    {
        var root = Fixtures.RepositoryRoot();
        var web = root is null ? null : Path.Combine(root.FullName, "src", "RugsManagment.Web");
        return web is not null && Directory.Exists(web) ? web : null;
    }

    private static HashSet<string> Precache(string sw)
    {
        var block = Regex.Match(sw, @"const\s+PRECACHE\s*=\s*\[(?<items>[^\]]*)\]", RegexOptions.Singleline);
        Assert.True(block.Success, "PRECACHE در sw.js پیدا نشد.");
        return Regex.Matches(block.Groups["items"].Value, @"'(?<u>[^']+)'").Select(m => m.Groups["u"].Value).ToHashSet();
    }

    /// <summary>متن‌هایی که به کاربر می‌رسند: رشته‌های C# (Application، Web) و سطرهای غیر توضیح cshtml/vue.</summary>
    private static IEnumerable<(string File, List<(int Line, string Text)> Texts)> UserFacingTexts(string root)
    {
        var sources = new[] { "src/RugsManagment.Application", "src/RugsManagment.Web" };
        foreach (var source in sources)
        {
            var dir = Path.Combine(root, source);
            if (!Directory.Exists(dir)) continue;

            foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (relative.Contains("/bin/", StringComparison.Ordinal) || relative.Contains("/obj/", StringComparison.Ordinal)
                    || relative.Contains("/node_modules/", StringComparison.Ordinal) || relative.Contains("/wwwroot/", StringComparison.Ordinal))
                    continue;

                var ext = Path.GetExtension(file);
                if (ext is not (".cs" or ".cshtml" or ".vue")) continue;

                var texts = new List<(int, string)>();
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    var trimmed = lines[i].TrimStart();
                    if (IsComment(trimmed)) continue;

                    if (ext == ".cs")
                    {
                        foreach (Match literal in StringLiteral().Matches(lines[i]))
                            texts.Add((i + 1, literal.Value));
                    }
                    else
                    {
                        texts.Add((i + 1, lines[i]));
                    }
                }

                if (texts.Count > 0)
                    yield return (relative, texts);
            }
        }
    }

    private static bool IsComment(string trimmed)
        => trimmed.StartsWith("//", StringComparison.Ordinal)
           || trimmed.StartsWith("/*", StringComparison.Ordinal)
           || trimmed.StartsWith('*')
           || trimmed.StartsWith("<!--", StringComparison.Ordinal)
           || trimmed.StartsWith("@*", StringComparison.Ordinal);

    /// <summary>نشانی هم‌مبدأ در href/src یا url(...): با «/» شروع می‌شود (href خالیِ «تلاش دوباره» نه).</summary>
    [GeneratedRegex("""(?:href|src)="(?<url>/[^"]*)"|url\(\s*'?(?<url>/[^')\s]+)'?\s*\)""")]
    private static partial Regex LocalUrls();

    [GeneratedRegex(@"const\s+CACHE\s*=\s*'rugs-static-v(?<n>\d+)'")]
    private static partial Regex CacheVersion();

    /// <summary>«گروه» که «ی» (گروهی = چندتایی) پشتش نیست.</summary>
    [GeneratedRegex("گروه(?!ی)")]
    private static partial Regex OldBatchWord();

    /// <summary>رشتهٔ C# در یک سطر: «"…"»، «$"…"» یا «@"…"» (رشتهٔ خام چندسطری لازم نیست؛ پیام‌ها یک‌سطری‌اند).</summary>
    [GeneratedRegex(@"\$?@?""(?:[^""\\]|\\.)*""")]
    private static partial Regex StringLiteral();

    [GeneratedRegex(@"class=""(?<c>[^""]*)""")]
    private static partial Regex ClassAttribute();
}
