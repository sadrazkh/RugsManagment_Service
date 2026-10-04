namespace RugsManagment.Tests;

/// <summary>
/// واژه‌نامهٔ بازرگان (spec A.2، J.2): هر واژه فقط یک معنا دارد.
/// <list type="bullet">
/// <item>«محموله» فقط یعنی اظهار صادراتی؛ صفحه‌های ورودی (گروه‌های قدیمی)، انبار و تنظیمات نباید آن را داشته
/// باشند — پیش از این «گروه / محموله» همان RugBatch بود و بازرگان دو چیز متفاوت را یکی می‌دید.</item>
/// <item>«پیش‌نویس» فقط برای محموله و خروجیِ قطعی‌نشده است؛ فرش تازه «ثبت‌شده» است، نه «پیش‌نویس».</item>
/// </list>
/// فقط فایل‌های <c>.cshtml</c> و <c>.vue</c> وب سنجیده می‌شوند (متن‌هایی که مستقیم به کاربر می‌رسند). استثناها
/// پوشه‌های همان ویژگی‌اند و این‌جا فهرست شده‌اند؛ ویژگی تازه‌ای که حق این واژه را دارد پوشه‌اش را این‌جا
/// اضافه می‌کند، نه اینکه آزمون را خاموش کند.
/// </summary>
public class GlossaryTests
{
    private const string Shipment = "محموله";
    private const string Draft = "پیش‌نویس";

    /// <summary>
    /// جاهایی که «محموله» معنای درستش را دارد: محموله و نگله، خروجی، هزینه‌های محموله، ورود از اکسل (که محموله
    /// می‌سازد)، خانه و «بیشتر» (کاشی‌ها و فهرست صفحه‌ها) و کارت راهنما. مسیرها نسبت به <c>src/RugsManagment.Web</c>.
    /// </summary>
    private static readonly string[] ShipmentContexts =
    [
        "Views/Shipments/",
        "Views/Documents/",
        "Views/Costs/",
        "Views/Import/",
        "Views/Home/",
        "Views/More/",
        "Views/Help/",
        "Views/Shared/Components/",
        "ClientApp/src/islands/shipments/",
        "ClientApp/src/islands/documents/",
        "ClientApp/src/islands/costs/",
        "ClientApp/src/islands/import/",
        "ClientApp/src/islands/platform/",
        "ClientApp/src/panels/",
        "ClientApp/src/components/scan/",
        // ترجیحات کارگاه: بخش «نگله و محموله» (هدف نگله، نرخ دلار محمولهٔ تازه، اندازهٔ نهایی پیش از قطعی)
        "Views/Settings/Preferences.cshtml",
        "ClientApp/src/islands/settings/PreferencesForm.island.vue"
    ];

    /// <summary>«پیش‌نویس» فقط روی محموله و خروجی‌اش.</summary>
    private static readonly string[] DraftContexts =
    [
        "Views/Shipments/",
        "Views/Documents/",
        "ClientApp/src/islands/shipments/",
        "ClientApp/src/islands/documents/"
    ];

    [Fact]
    public void Shipment_AppearsOnlyInShipmentAndDocumentScreens()
        => Assert.Empty(Offenders(Shipment, ShipmentContexts));

    [Fact]
    public void Draft_AppearsOnlyOnShipmentsAndOutputs()
        => Assert.Empty(Offenders(Draft, DraftContexts));

    /// <summary>«پرونده:سطر» هر جایی که واژه بیرون از پوشه‌های مجاز آمده.</summary>
    private static List<string> Offenders(string word, string[] allowedPrefixes)
    {
        var web = WebRoot();
        if (web is null) return []; // بیرون از مخزن (بستهٔ تست جدا)؛ چیزی برای سنجیدن نیست

        var offenders = new List<string>();
        foreach (var file in UiFiles(web))
        {
            var relative = Path.GetRelativePath(web.FullName, file).Replace('\\', '/');
            if (allowedPrefixes.Any(p => relative.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                continue;

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(word, StringComparison.Ordinal) && !IsComment(lines[i]))
                    offenders.Add($"{relative}:{i + 1}");
            }
        }

        return offenders;
    }

    /// <summary>
    /// سطر توضیح کد (به کاربر نمی‌رسد): <c>//</c>، <c>/*</c>، <c>*</c>، <c>&lt;!--</c> و <c>@*</c> در آغاز سطر.
    /// </summary>
    private static bool IsComment(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("//", StringComparison.Ordinal)
               || trimmed.StartsWith("/*", StringComparison.Ordinal)
               || trimmed.StartsWith('*')
               || trimmed.StartsWith("<!--", StringComparison.Ordinal)
               || trimmed.StartsWith("@*", StringComparison.Ordinal);
    }

    private static IEnumerable<string> UiFiles(DirectoryInfo web)
    {
        var views = Path.Combine(web.FullName, "Views");
        var client = Path.Combine(web.FullName, "ClientApp", "src");

        var cshtml = Directory.Exists(views)
            ? Directory.EnumerateFiles(views, "*.cshtml", SearchOption.AllDirectories)
            : [];
        var vue = Directory.Exists(client)
            ? Directory.EnumerateFiles(client, "*.vue", SearchOption.AllDirectories)
            : [];

        return cshtml.Concat(vue);
    }

    private static DirectoryInfo? WebRoot()
    {
        var root = Fixtures.RepositoryRoot();
        var web = root is null ? null : new DirectoryInfo(Path.Combine(root.FullName, "src", "RugsManagment.Web"));
        return web is { Exists: true } ? web : null;
    }
}
