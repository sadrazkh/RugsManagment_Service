using System.Text.RegularExpressions;
using RugsManagment.ArchitectureTests.Support;

namespace RugsManagment.ArchitectureTests;

/// <summary>
/// هر عضو هر enum عدد صریح دارد (spec B.14): بی استثنا در <c>Domain/Enums</c> و <c>Application/DTOs</c>، و در بقیهٔ
/// <c>src</c> برای هر enumی که <c>private</c> نیست.
///
/// چرا: enumها به‌صورت عدد ذخیره و در JSON فرستاده می‌شوند؛ عضوی که بی عدد وسط فهرست اضافه شود عدد همهٔ عضوهای بعدی را
/// بی‌صدا یکی جلو می‌برد و دادهٔ ذخیره‌شده معنای دیگری پیدا می‌کند. Reflection صریح بودن را نمی‌بیند، پس متن منبع خوانده
/// می‌شود. enumِ تودرتوی <c>private</c> فقط کمکیِ درون یک کلاس است (نه ذخیره می‌شود و نه از مرز می‌گذرد)، پس بیرون از دو
/// پوشهٔ اصلی کنار گذاشته می‌شود.
/// </summary>
public partial class ExplicitEnumValuesTests
{
    [GeneratedRegex(@"(?<mods>(?:\b(?:public|internal|private|protected|file|new)\s+)*)\benum\s+(?<name>\w+)\s*(?::\s*\w+\s*)?\{(?<body>[^{}]*)\}", RegexOptions.Singleline)]
    private static partial Regex EnumDeclaration();

    [GeneratedRegex(@"//[^\n]*|/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comments();

    [GeneratedRegex(@"\[[^\]]*\]")]
    private static partial Regex Attributes();

    public static TheoryData<string> Folders() => new() { "src/RugsManagment.Domain/Enums", "src/RugsManagment.Application/DTOs" };

    [Theory]
    [MemberData(nameof(Folders))]
    public void EveryEnumMember_HasAnExplicitValue(string folder)
    {
        var (enums, offenders) = Scan(SourceTree.CSharpFiles(folder).ToList(), includePrivate: true);

        Assert.True(enums > 0, $"در {folder} هیچ enumی پیدا نشد؛ الگوی جست‌وجو را بررسی کنید.");
        Assert.True(offenders.Count == 0, "عضو enum بی عدد صریح:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void EveryNonPrivateEnum_InTheWholeSource_HasExplicitValues()
    {
        var files = SourceTree.CSharpFiles("src")
            .Where(f => !SourceTree.Relative(f).Contains("/Migrations/", StringComparison.Ordinal))
            .ToList();
        var (enums, offenders) = Scan(files, includePrivate: false);

        // Domain/Enums به‌تنهایی بیش از ده enum دارد؛ کمتر یعنی الگوی جست‌وجو خراب شده
        Assert.True(enums > 10, $"فقط {enums} enum پیدا شد؛ الگوی جست‌وجو را بررسی کنید.");
        Assert.True(offenders.Count == 0, "عضو enum بی عدد صریح:\n" + string.Join("\n", offenders));
    }

    private static (int Enums, List<string> Offenders) Scan(IReadOnlyList<string> files, bool includePrivate)
    {
        Assert.NotEmpty(files);
        var offenders = new List<string>();
        var enums = 0;
        foreach (var file in files)
        {
            var text = Comments().Replace(File.ReadAllText(file), string.Empty);
            foreach (Match declaration in EnumDeclaration().Matches(text))
            {
                if (!includePrivate && declaration.Groups["mods"].Value.Contains("private", StringComparison.Ordinal))
                    continue;

                enums++;
                var members = Attributes().Replace(declaration.Groups["body"].Value, string.Empty)
                    .Split(',')
                    .Select(m => m.Trim())
                    .Where(m => m.Length > 0);
                foreach (var member in members)
                {
                    if (!member.Contains('='))
                        offenders.Add($"{SourceTree.Relative(file)}: {declaration.Groups["name"].Value}.{member}");
                }
            }
        }

        return (enums, offenders);
    }
}
