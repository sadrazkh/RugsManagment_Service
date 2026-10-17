using System.Data.Common;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RugsManagment.ArchitectureTests.Support;

namespace RugsManagment.ArchitectureTests;

/// <summary>
/// «هیچ نوشتنی از پاس سازگاری رد نمی‌شود» (spec D.4 «Bypass rule»، M.1): هر نوشتن داده باید از
/// <c>IUnitOfWork.SaveChangesAsync</c> بگذرد تا contributorها، قفل‌ها و مرحله‌ها آن را ببینند. پس این‌ها بیرون از فهرست سفید
/// ممنوع‌اند: <c>ExecuteUpdate</c>/<c>ExecuteDelete</c>، <c>ExecuteSql*</c>، <c>DbCommand.ExecuteNonQuery</c>، SQL خامِ نوشتنی
/// در <c>SqlQuery</c>/<c>FromSql</c>، و <c>DbContext.SaveChanges</c> مستقیم (که پاس را دور می‌زند).
///
/// فهرست سفید: <c>Persistence/Ledger</c> (خودِ پاس و مرحله‌هایش)، کلاس‌های <c>*LedgerRepository</c> فازهای بعد که مرحله‌ها صدا
/// می‌زنند، <c>TenantCounterRepository</c> (شمارندهٔ اتمیک؛ دادهٔ حساب نیست)، و کارهای استارت که schema و قفل استارت را
/// می‌سازند (<c>Migrator</c>، <c>StartupPipeline</c>) و دادهٔ پایهٔ seed (<c>DatabaseSeeder</c>: انواع مرحله، کاربران، کارگاه دمو؛
/// محتوای پولی دمو از UnitOfWork می‌گذرد).
/// </summary>
public partial class NoBypassTests
{
    private static readonly HashSet<string> RawWriteMethods = new(StringComparer.Ordinal)
    {
        "ExecuteUpdate", "ExecuteUpdateAsync", "ExecuteDelete", "ExecuteDeleteAsync",
        "ExecuteSql", "ExecuteSqlAsync", "ExecuteSqlRaw", "ExecuteSqlRawAsync",
        "ExecuteSqlInterpolated", "ExecuteSqlInterpolatedAsync"
    };

    private static readonly HashSet<string> StartupTypes = new(StringComparer.Ordinal)
    {
        "RugsManagment.Infrastructure.Persistence.Seed.Migrator",
        "RugsManagment.Infrastructure.Persistence.Seed.StartupPipeline"
    };

    private const string LedgerNamespace = "RugsManagment.Infrastructure.Persistence.Ledger";
    private const string CounterRepository = "RugsManagment.Infrastructure.Persistence.Repositories.TenantCounterRepository";
    private const string Seeder = "RugsManagment.Infrastructure.Persistence.Seed.DatabaseSeeder";

    private static bool IsLedger(Type caller)
        => caller.Namespace?.StartsWith(LedgerNamespace, StringComparison.Ordinal) == true
           || caller.Name.EndsWith("LedgerRepository", StringComparison.Ordinal);

    private static bool IsRawWrite(MethodBase callee)
        => (callee.DeclaringType?.Namespace?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true
            && RawWriteMethods.Contains(callee.Name))
           || (typeof(DbCommand).IsAssignableFrom(callee.DeclaringType)
               && callee.Name is "ExecuteNonQuery" or "ExecuteNonQueryAsync");

    private static bool IsDirectSave(MethodBase callee)
        => typeof(DbContext).IsAssignableFrom(callee.DeclaringType) && callee.Name is "SaveChanges" or "SaveChangesAsync";

    /// <summary>
    /// خودِ خوانندهٔ IL درست کار می‌کند (وگرنه دو آزمون بعدی بی‌صدا سبز بودند): نوشتن‌های مجازِ شناخته‌شده را می‌بیند.
    /// </summary>
    [Fact]
    public void TheScanner_SeesTheKnownWhitelistedWrites()
    {
        var calls = IlScanner.Calls(SourceTree.ProductionAssemblies);

        Assert.Contains(calls, c => c.CallerType.Name == "LedgerSession" && IsDirectSave(c.Callee));
        Assert.Contains(calls, c => c.CallerType.Name == "LedgerSession" && IsRawWrite(c.Callee));
        Assert.Contains(calls, c => c.CallerType.Name == "LedgerRepository" && IsRawWrite(c.Callee));
        Assert.Contains(calls, c => c.CallerType.Name == "UnitOfWork" && c.LogicalMethod == "SaveChangesAsync"
                                    && c.Callee.Name == "SaveChangesAsync" && c.Callee.DeclaringType?.Name == "ILedgerPass");
    }

    [Fact]
    public void RawSqlWrites_OnlyInTheLedgerAndTheWhitelist()
    {
        var offenders = IlScanner.Calls(SourceTree.ProductionAssemblies)
            .Where(c => IsRawWrite(c.Callee))
            .Where(c => !IsLedger(c.CallerType)
                        && c.CallerType.FullName != CounterRepository
                        && !StartupTypes.Contains(c.CallerType.FullName!))
            .Select(c => c.ToString())
            .Distinct()
            .ToList();

        Assert.True(offenders.Count == 0, "نوشتن خام بیرون از پاس سازگاری:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void DirectSaveChanges_OnlyInTheLedgerSessionAndTheSeeder()
    {
        var offenders = IlScanner.Calls(SourceTree.ProductionAssemblies)
            .Where(c => IsDirectSave(c.Callee))
            .Where(c => !IsLedger(c.CallerType) && c.CallerType.FullName != Seeder)
            .Select(c => c.ToString())
            .Distinct()
            .ToList();

        Assert.True(offenders.Count == 0,
            "DbContext.SaveChanges مستقیم (پاس سازگاری را دور می‌زند؛ از IUnitOfWork استفاده کنید):\n" + string.Join("\n", offenders));
    }

    [GeneratedRegex(@"^\s*//[^\n]*", RegexOptions.Multiline)]
    private static partial Regex Comments();

    [GeneratedRegex(@"\b(SqlQuery|SqlQueryRaw|FromSql|FromSqlRaw|FromSqlInterpolated)\b")]
    private static partial Regex RawQueryCall();

    [GeneratedRegex(@"\bINSERT\s+INTO\b|\bUPDATE\s+[""\\]*\w|\bDELETE\s+FROM\b|\bMERGE\s+INTO\b|\bTRUNCATE\b")]
    private static partial Regex WriteStatement();

    /// <summary>
    /// <c>SqlQuery</c> و <c>FromSql</c> برای خواندن‌اند؛ اگر فایلی بیرون از فهرست سفید با آن‌ها INSERT/UPDATE/DELETE بفرستد،
    /// همان دور زدن پاس است (IL متن SQL را نشان نمی‌دهد، پس متن منبع خوانده می‌شود).
    /// </summary>
    [Fact]
    public void RawQueries_DoNotWrite_OutsideTheWhitelist()
    {
        var allowed = new[]
        {
            "src/RugsManagment.Infrastructure/Persistence/Ledger/",
            "src/RugsManagment.Infrastructure/Persistence/Repositories/TenantCounterRepository.cs",
            "src/RugsManagment.Infrastructure/Persistence/Migrations/",
            "src/RugsManagment.Infrastructure/Persistence/Seed/Migrator.cs",
            "src/RugsManagment.Infrastructure/Persistence/Seed/StartupPipeline.cs"
        };

        var offenders = new List<string>();
        foreach (var file in SourceTree.CSharpFiles("src"))
        {
            var relative = SourceTree.Relative(file);
            if (allowed.Any(a => relative.StartsWith(a, StringComparison.Ordinal))
                || relative.EndsWith("LedgerRepository.cs", StringComparison.Ordinal))
                continue;

            // توضیح‌ها کنار گذاشته می‌شوند (توضیح فارسی ممکن است واژهٔ UPDATE را بیاورد)
            var text = Comments().Replace(File.ReadAllText(file), string.Empty);
            if (RawQueryCall().IsMatch(text) && WriteStatement().IsMatch(text))
                offenders.Add(relative);
        }

        Assert.True(offenders.Count == 0, "SQL خامِ نوشتنی بیرون از پاس سازگاری:\n" + string.Join("\n", offenders));
    }
}
