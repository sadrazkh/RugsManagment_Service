using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using RugsManagment.Application;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.Infrastructure;
using RugsManagment.Infrastructure.Persistence.Seed;
using RugsManagment.IntegrationTests.Support;

namespace RugsManagment.IntegrationTests.Ledger;

/// <summary>
/// seed از راه پاس سازگاری (spec D.4 جای ۷): سرور خالی با DI کامل برنامه استارت می‌شود؛ فرش‌های دمو سطر خرید «کل» دارند (نه
/// ستون منسوخ)، ردیف <c>RugCostTotal</c> هر فرش را پاس نوشته، فرش «فروخته‌شده» فروش واقعی با عکس بها دارد، «ورودی» دمو
/// شمارهٔ ۱ از شمارندهٔ کارگاه گرفته، و سنجش حساب‌ها هیچ ناهمخوانی نمی‌بیند. استارت دوم چیزی تکرار نمی‌کند.
/// </summary>
public sealed class SeedThroughLedgerTests(NonExistentTestDatabase database) : IClassFixture<NonExistentTestDatabase>
{
    [PostgresFact]
    public async Task DemoSeed_GoesThroughThePass_AndTheLedgerIsHealthy()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = database.ConnectionString,
                ["Database:MigrateOnStartup"] = "true",
                ["Storage:ImagePath"] = Path.Combine(Path.GetTempPath(), "rugs-it-images")
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment, TestHostEnvironment>();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddScoped<FakeCurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<FakeCurrentUser>());
        services.AddSingleton<IMoneyVisibility>(FullMoneyVisibility.Instance);
        await using var provider = services.BuildServiceProvider();

        await StartupPipeline.RunAsync(provider, NullLogger.Instance);
        await StartupPipeline.RunAsync(provider, NullLogger.Instance);

        await using var db = database.CreateContext();
        var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.Slug == "demo-weaver");
        var rugs = await db.Rugs.AsNoTracking().Where(r => r.TenantId == tenant.Id).ToListAsync();
        Assert.Equal(5, rugs.Count);

        var totals = await db.Set<RugCostTotal>().AsNoTracking().Where(t => t.TenantId == tenant.Id).ToListAsync();
        Assert.Equal(5, totals.Count);

        var lines = await db.Set<RugPurchaseLine>().AsNoTracking().Where(l => l.TenantId == tenant.Id).ToListAsync();
        Assert.Equal(5, lines.Count);
        Assert.All(lines, l => Assert.Equal(l.Rate, l.Amount));

        // «تبریز لاکی» ۲×۳ در حال انجام: شستشو (۸۵٬۰۰۰ × ۶) در جریان + قیمت خرید ۴۵٬۰۰۰٬۰۰۰
        var tabriz = rugs.Single(r => r.Sku.EndsWith("-0001", StringComparison.Ordinal));
        Assert.Equal(45_510_000m, totals.Single(t => t.RugId == tabriz.Id).Total);

        var sale = await db.RugSales.AsNoTracking().SingleAsync(s => s.TenantId == tenant.Id);
        var soldTotal = totals.Single(t => t.RugId == sale.RugId).Total;
        Assert.Equal(soldTotal, sale.CostAtSale);
        Assert.Equal(RugStatus.Sold, rugs.Single(r => r.Id == sale.RugId).Status);

        var batch = await db.RugBatches.AsNoTracking().SingleAsync(b => b.TenantId == tenant.Id);
        Assert.Equal(1, batch.Number);

        await using var scope = provider.CreateAsyncScope();
        var report = await scope.ServiceProvider.GetRequiredService<ILedgerVerifier>().VerifyAsync(tenant.Id);
        Assert.Equal(5, report.Checked);
        Assert.Empty(report.Mismatches);
    }
}
