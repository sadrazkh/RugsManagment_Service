using System.Reflection;

namespace RugsManagment.ArchitectureTests.Support;

/// <summary>
/// متن منبع مخزن برای آزمون‌هایی که روی کد نوشته‌شده‌اند (عدد صریح enum، جای فرمول). ریشه از روی
/// <c>RugsManagment_Service.slnx</c> بالای پوشهٔ خروجی پیدا می‌شود؛ پوشه‌های bin/obj کنار گذاشته می‌شوند.
/// </summary>
public static class SourceTree
{
    private static readonly Lazy<string> RootPath = new(FindRoot);

    public static string Root => RootPath.Value;

    /// <summary>اسمبلی‌های تولیدی (نه آزمون).</summary>
    public static Assembly[] ProductionAssemblies =>
    [
        typeof(Domain.Entities.Rug).Assembly,
        typeof(Application.Ledger.LedgerPass).Assembly,
        typeof(Infrastructure.Persistence.AppDbContext).Assembly,
        typeof(Web.Auth.Policies).Assembly
    ];

    /// <summary>همهٔ فایل‌های .cs زیر یک مسیر نسبی (از ریشهٔ مخزن)، بی bin/obj.</summary>
    public static IEnumerable<string> CSharpFiles(string relativeDirectory)
    {
        var directory = Path.Combine(Root, relativeDirectory);
        if (!Directory.Exists(directory))
            return [];

        return Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsBuildOutput(f))
            .Order(StringComparer.Ordinal);
    }

    /// <summary>مسیر نسبیِ یک فایل با «/».</summary>
    public static string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');

    private static bool IsBuildOutput(string path)
    {
        var parts = Path.GetRelativePath(Root, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(p => p is "bin" or "obj" or "node_modules");
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RugsManagment_Service.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("ریشهٔ مخزن (RugsManagment_Service.slnx) پیدا نشد.");
    }
}
