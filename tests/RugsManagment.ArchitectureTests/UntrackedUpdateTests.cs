using System.Reflection;
using Microsoft.EntityFrameworkCore;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.ArchitectureTests.Support;
using RugsManagment.Domain.Entities;
using RugsManagment.Infrastructure.Persistence.Repositories;

namespace RugsManagment.ArchitectureTests;

/// <summary>
/// هیچ <c>Update()</c>ی روی نمونهٔ ردیابی‌نشدهٔ موجودیت‌های حساب (فرش، مرحله، فروش؛ و در فازهای بعد خط محموله و ثبت هزینه)
/// نیست (spec B.1، D.4). چرا: <c>Update()</c> روی نمونهٔ جدا مقدار «اصلی» را برابر مقدار فعلی می‌گذارد و تفاوت‌سنجیِ پاس
/// سازگاری تغییر را نمی‌بیند؛ پس هزینه‌ها بی‌صدا کهنه می‌مانند.
///
/// چون «ردیابی‌شده یا نه» را IL نمی‌گوید، قاعده سه لایه دارد:
/// <list type="number">
/// <item><c>DbSet&lt;T&gt;.Update/Attach</c> و <c>DbContext.Update/Attach</c> فقط درون کلاس‌های Repository (هیچ سرویسی
/// مستقیم وصل نمی‌کند)؛</item>
/// <item>فرش: <c>RugRepository.Update</c> خودش فرشِ ردیابی‌نشده را رد می‌کند (نگهبان زمان اجرا) — آزمون بررسی می‌کند که
/// override هست؛</item>
/// <item>مرحله و فروش (و نوع‌های فازهای بعد): <c>IRepository&lt;T&gt;.Update</c> فقط از جاهای فهرست‌شده که موجودیت را از یک
/// بارگذاری ردیابی‌شده گرفته‌اند؛ جای تازه باید با دلیلش این‌جا اضافه شود.</item>
/// </list>
/// </summary>
public class UntrackedUpdateTests
{
    /// <summary>نوع‌های حساب که Update روی نمونهٔ جدایشان ممنوع است (نام، تا نوع‌های فازهای بعد هم پوشش داده شوند).</summary>
    private static readonly HashSet<string> LedgerTypes = new(StringComparer.Ordinal)
    {
        nameof(Rug), nameof(RugWorkflowStep), nameof(RugSale), "ShipmentLine", "CostEntry"
    };

    /// <summary>
    /// جاهای مجاز <c>IRepository&lt;مرحله|فروش&gt;.Update</c>: (نوع، روش منطقی) → دلیل ردیابی‌شده بودن.
    /// </summary>
    private static readonly Dictionary<(string Type, string Method), string> AllowedRepositoryUpdates = new()
    {
        [("RugsManagment.Application.Services.RugSaleService", "SaveAsync")] =
            "فروش از rug.Sale می‌آید که GetWithWorkflowAsync ردیابی‌شده بارگذاری کرده است"
    };

    private static readonly HashSet<string> UpdateNames = new(StringComparer.Ordinal) { "Update", "UpdateRange", "Attach", "AttachRange" };

    private static Type? EntityTypeOf(MethodBase callee)
    {
        if (callee is MethodInfo { IsGenericMethod: true } generic)
            return generic.GetGenericArguments()[0];
        if (callee.DeclaringType is { IsGenericType: true } declaring)
            return declaring.GetGenericArguments()[0];
        var parameters = callee.GetParameters();
        return parameters.Length > 0 ? parameters[0].ParameterType : null;
    }

    private static bool IsRepositoryClass(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Repository<>))
                return true;
        }

        return false;
    }

    [Fact]
    public void DbSetAndDbContextAttach_OnlyInsideRepositories()
    {
        var offenders = IlScanner.Calls(SourceTree.ProductionAssemblies)
            .Where(c => UpdateNames.Contains(c.Callee.Name)
                        && (c.Callee.DeclaringType is { IsGenericType: true } d && d.GetGenericTypeDefinition() == typeof(DbSet<>)
                            || typeof(DbContext).IsAssignableFrom(c.Callee.DeclaringType)))
            .Where(c => !IsRepositoryClass(c.CallerType))
            .Select(c => c.ToString())
            .Distinct()
            .ToList();

        Assert.True(offenders.Count == 0, "وصل کردن موجودیت بیرون از Repository:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void RugRepository_RefusesAnUntrackedRug()
    {
        var update = typeof(RugRepository).GetMethod(nameof(RugRepository.Update), [typeof(Rug)]);
        Assert.NotNull(update);
        Assert.Equal(typeof(RugRepository), update!.DeclaringType);
    }

    [Fact]
    public void RepositoryUpdate_OnStepsAndSales_OnlyFromTheWhitelist()
    {
        var offenders = IlScanner.Calls(SourceTree.ProductionAssemblies)
            .Where(c => UpdateNames.Contains(c.Callee.Name)
                        && c.Callee.DeclaringType is { IsGenericType: true } d
                        && (d.GetGenericTypeDefinition() == typeof(IRepository<>) || d.GetGenericTypeDefinition() == typeof(Repository<>)))
            .Where(c => EntityTypeOf(c.Callee) is { } t && LedgerTypes.Contains(t.Name) && t != typeof(Rug))
            .Where(c => !IsRepositoryClass(c.CallerType)
                        && !AllowedRepositoryUpdates.ContainsKey((c.CallerType.FullName!, c.LogicalMethod)))
            .Select(c => c.ToString())
            .Distinct()
            .ToList();

        Assert.True(offenders.Count == 0,
            "Update روی مرحله/فروش از جایی که ردیابی‌شده بودنش ثابت نشده (به فهرست این آزمون با دلیل اضافه کنید):\n"
            + string.Join("\n", offenders));
    }
}
