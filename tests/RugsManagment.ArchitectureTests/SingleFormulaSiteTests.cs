using System.Text.RegularExpressions;
using RugsManagment.ArchitectureTests.Support;

namespace RugsManagment.ArchitectureTests;

/// <summary>
/// «یک جای فرمول» (spec D.4): هزینهٔ مؤثر مرحله (<c>COALESCE(دستی، محاسبه‌شده، 0) + تعدیل</c>، کف صفر)، مبلغ سطر خرید و
/// ترکیب «سرمایهٔ کل» فقط در <c>Infrastructure/Persistence/Ledger/CostSql.cs</c> (SQL) و <c>Domain/Costing/RugCostMath.cs</c>
/// (C#) نوشته می‌شوند. پیش از فاز ۲ همین فرمول در هفت جا تکرار شده بود و هر منبع هزینهٔ تازه باید هفت جا اضافه می‌شد.
///
/// پوشهٔ Migrations استثناست: migration تاریخی است و نباید با تغییر CostSql عوض شود (بررسی خودکار M2 فرمول قدیمی را عمداً
/// تکرار می‌کند).
/// </summary>
public class SingleFormulaSiteTests
{
    private static readonly string[] Allowed =
    [
        "src/RugsManagment.Domain/Costing/RugCostMath.cs",
        "src/RugsManagment.Infrastructure/Persistence/Ledger/CostSql.cs"
    ];

    /// <summary>الگوهای «فرمول»؛ هر کدام با نامی که در پیام شکست می‌آید.</summary>
    private static readonly (string Name, Regex Pattern)[] Formulas =
    [
        ("step effective cost (C#)", new Regex(@"ManualCostOverride\s*\?\?\s*[\w.?!]*CalculatedCost", RegexOptions.IgnoreCase)),
        ("step effective cost (SQL)", new Regex(@"COALESCE\(\s*[\w.""\\]*ManualCostOverride", RegexOptions.IgnoreCase)),
        ("legacy total investment (C#)", new Regex(@"PurchaseCost\s*\?\?\s*0m?\s*\)\s*\+", RegexOptions.IgnoreCase)),
        ("legacy total investment (SQL)", new Regex(@"COALESCE\(\s*[\w.""\\]*PurchaseCost[""\\]*\s*,\s*0\s*\)\s*\+", RegexOptions.IgnoreCase)),
        ("purchase PerM2 line (SQL)", new Regex(@"round\(\s*[\w.""\\]*Rate[""\\]*\s*\*\s*[\w.""\\]*IntakeAreaM2", RegexOptions.IgnoreCase))
    ];

    private static bool IsMigration(string relative) => relative.Contains("/Migrations/", StringComparison.Ordinal);

    [Fact]
    public void CostFormulas_AppearOnlyInCostSqlAndRugCostMath()
    {
        var offenders = new List<string>();
        foreach (var file in SourceTree.CSharpFiles("src"))
        {
            var relative = SourceTree.Relative(file);
            if (IsMigration(relative) || Allowed.Contains(relative, StringComparer.Ordinal))
                continue;

            var text = File.ReadAllText(file);
            foreach (var (name, pattern) in Formulas)
            {
                if (pattern.IsMatch(text))
                    offenders.Add($"{relative}: {name}");
            }
        }

        Assert.True(offenders.Count == 0,
            "فرمول هزینه بیرون از CostSql/RugCostMath نوشته شده است:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void TheTwoFormulaSites_ActuallyHoldTheFormulas()
    {
        // اگر کسی فرمول را از این دو فایل بیرون ببرد، آزمون بالا بی‌صدا سبز می‌ماند؛ این یکی آن را می‌گیرد
        var sql = File.ReadAllText(Path.Combine(SourceTree.Root, Allowed[1]));
        Assert.Matches(Formulas[1].Pattern, sql.Replace("{step}.", "s.", StringComparison.Ordinal));
        Assert.Contains("IntakeAreaM2", sql, StringComparison.Ordinal);

        var csharp = File.ReadAllText(Path.Combine(SourceTree.Root, Allowed[0]));
        Assert.Matches(new Regex(@"manualCostOverride\s*\?\?\s*calculatedCost", RegexOptions.IgnoreCase), csharp);
    }
}
