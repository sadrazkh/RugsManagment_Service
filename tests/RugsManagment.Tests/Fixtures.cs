using System.Text.Json;

namespace RugsManagment.Tests;

/// <summary>
/// دسترسی به داده‌های مشترک <c>tests/fixtures</c> (کنار خروجی تست در زیرپوشهٔ <c>fixtures</c> کپی می‌شوند)
/// و به ریشهٔ مخزن (برای آزمون‌هایی که فایل‌های view و جزیره را می‌خوانند).
/// </summary>
internal static class Fixtures
{
    public static string PathOf(string fileName) => Path.Combine(AppContext.BaseDirectory, "fixtures", fileName);

    public static JsonDocument Load(string fileName) => JsonDocument.Parse(File.ReadAllText(PathOf(fileName)));

    public static JsonDocument MathVectors() => Load("math-vectors.json");

    public static JsonDocument SampleShipment() => Load("sample-shipment.json");

    /// <summary>
    /// ریشهٔ مخزن: از پوشهٔ خروجی تست بالا می‌رود تا فایل slnx را پیدا کند. اگر پیدا نشد (مثلاً تست از بستهٔ
    /// جدا اجرا شده) null.
    /// </summary>
    public static DirectoryInfo? RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (dir.EnumerateFiles("RugsManagment_Service.slnx").Any())
                return dir;
        }

        return null;
    }
}
