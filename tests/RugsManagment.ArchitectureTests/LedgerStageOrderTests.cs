using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RugsManagment.Application;
using RugsManagment.Application.Ledger;
using RugsManagment.ArchitectureTests.Support;
using RugsManagment.Infrastructure;

namespace RugsManagment.ArchitectureTests;

/// <summary>
/// ترتیب مرحله‌های پاس سازگاری (spec D.4 «LedgerStageOrder»): عددها یکتا، <c>RugCostTotal</c> بیشینه (جمع هزینهٔ فرش
/// همیشه آخر، چون همهٔ بقیه را جمع می‌زند)، و هر مرحلهٔ ثبت‌شده یکی از همین ثابت‌ها را برمی‌گرداند؛ پس فازی که مرحلهٔ تازه
/// اضافه می‌کند نمی‌تواند بی‌صدا جای خالی یا عدد تکراری بگیرد.
/// </summary>
public class LedgerStageOrderTests
{
    private static IReadOnlyDictionary<string, int> Constants()
        => typeof(LedgerStageOrder).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(int))
            .ToDictionary(f => f.Name, f => (int)f.GetRawConstantValue()!);

    private static IReadOnlyList<Type> StageTypes()
        => SourceTree.ProductionAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ILedgerStage).IsAssignableFrom(t))
            .ToList();

    /// <summary>
    /// <c>Order</c> هر مرحله بی ساختن آن (وابستگی‌هایش را نمی‌خواهیم): نمونهٔ بی‌سازنده؛ getter فقط یک ثابت برمی‌گرداند.
    /// </summary>
    private static int OrderOf(Type stageType) => ((ILedgerStage)RuntimeHelpers.GetUninitializedObject(stageType)).Order;

    [Fact]
    public void Constants_AreUnique_AndTheCostTotalIsTheMaximum()
    {
        var constants = Constants();
        Assert.Equal(constants.Count, constants.Values.Distinct().Count());
        Assert.Equal(LedgerStageOrder.RugCostTotal, constants.Values.Max());
    }

    [Fact]
    public void EveryStage_UsesALedgerStageOrderConstant_AndNoTwoShareAnOrder()
    {
        var constants = Constants().Values.ToHashSet();
        var stages = StageTypes();
        Assert.NotEmpty(stages);

        var orders = stages.Select(t => (Type: t, Order: OrderOf(t))).ToList();
        Assert.All(orders, s => Assert.True(constants.Contains(s.Order),
            $"{s.Type.Name}.Order = {s.Order} یکی از ثابت‌های LedgerStageOrder نیست."));

        var duplicates = orders.GroupBy(s => s.Order).Where(g => g.Count() > 1).Select(g => string.Join(", ", g.Select(s => s.Type.Name)));
        Assert.Empty(duplicates);

        Assert.Equal(LedgerStageOrder.RugCostTotal, orders.Max(s => s.Order));
    }

    [Fact]
    public void EveryRegisteredStage_IsAKnownStageType_AndThePhase2StagesAreRegistered()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=architecture_tests_never_opened"
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        services.AddInfrastructure(configuration);

        var registered = services
            .Where(d => d.ServiceType == typeof(ILedgerStage))
            .Select(d => d.ImplementationType)
            .ToList();

        Assert.DoesNotContain(null, registered); // فقط ثبت با نوع، تا ترتیبش خواندنی باشد
        var known = StageTypes().ToHashSet();
        Assert.All(registered, t => Assert.Contains(t!, known));

        var orders = registered.Select(t => OrderOf(t!)).ToList();
        Assert.Contains(LedgerStageOrder.SerialOverlap, orders);
        Assert.Contains(LedgerStageOrder.PurchaseLine, orders);
        Assert.Contains(LedgerStageOrder.PurchaseRollup, orders);
        Assert.Contains(LedgerStageOrder.RugCostTotal, orders);
        Assert.Equal(orders.Count, orders.Distinct().Count());
    }
}
