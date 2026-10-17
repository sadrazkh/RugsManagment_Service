using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RugsManagment.Application;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Activity;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.DTOs.Tenants;
using RugsManagment.Application.Features.Platform;
using RugsManagment.Application.Ledger;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.Infrastructure;
using RugsManagment.Infrastructure.Persistence;
using RugsManagment.Infrastructure.Persistence.Seed;
using RugsManagment.IntegrationTests.Support;
using DiServiceProvider = Microsoft.Extensions.DependencyInjection.ServiceProvider;

namespace RugsManagment.IntegrationTests;

/// <summary>بیننده‌ای که آزمون تعیین می‌کند Business است یا نه (در وب: سیاست Business).</summary>
public sealed class SwitchableMoneyVisibility : IMoneyVisibility
{
    public bool CanSeeMoney { get; set; } = true;
}

/// <summary>
/// سرویس‌های سکو روی پستگرس واقعی و با همان گراف DI برنامه (AddApplication + AddInfrastructure، با
/// ValidateOnBuild): ترجیحات کارگاه با xmin و قفل رقم اعشار، seed کارگاه‌ها، تاریخچه با صفحه‌بندی SQL و
/// جزئیات jsonb، انبار خانه با قاعدهٔ دسته، و خلاصهٔ خانهٔ امن برای انباردار.
/// </summary>
public sealed class PlatformServicesIntegrationTests(MigratedP1CoreDatabase database)
    : IClassFixture<MigratedP1CoreDatabase>, IAsyncLifetime
{
    private DiServiceProvider? _services;

    public Task InitializeAsync()
    {
        if (!PostgresTestEnvironment.ShouldSkip)
            _services = BuildServices(database.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_services is not null) await _services.DisposeAsync();
    }

    [PostgresFact]
    public async Task DiGraph_ResolvesEveryPlatformService_AndRegistersTheSeedTaskAt100()
    {
        await using var scope = Scope(null, canSeeMoney: true);
        var sp = scope.ServiceProvider;

        Assert.NotNull(sp.GetRequiredService<ITenantPreferencesService>());
        Assert.NotNull(sp.GetRequiredService<IUserPreferencesService>());
        Assert.NotNull(sp.GetRequiredService<IHomeSummaryService>());
        // erratum E3: P4a جای منبع خالی P1 را با منبع واقعی محموله‌ها گرفت
        Assert.IsType<RugsManagment.Application.Features.Shipments.HomeShipmentSource>(sp.GetRequiredService<IHomeShipmentSource>());
        // P5 دفتر واقعی هزینه را جایگزین منبع خالی پلتفرم می‌کند.
        Assert.IsType<RugsManagment.Application.Features.Costs.CostHomeMoneySource>(sp.GetRequiredService<IHomeMoneySource>());
        Assert.IsType<TenantSeedRunner>(sp.GetRequiredService<ITenantSeedRunner>());
        var task = Assert.Single(sp.GetServices<IStartupTask>().OfType<TenantSeedRunner>());
        Assert.Equal(100, task.Order);
        Assert.Contains(sp.GetServices<ITenantSeedContributor>(), c => c is TenantPreferencesSeedContributor);
    }

    [PostgresFact]
    public async Task TenantCreate_SeedsPreferences_AndTheAdminGetsFullMode()
    {
        TenantDto created;
        await using (var scope = Scope(null, canSeeMoney: true))
        {
            created = await scope.ServiceProvider.GetRequiredService<ITenantManagementService>().CreateAsync(
                new CreateTenantRequest("IT new tenant", "it-new-" + Guid.NewGuid().ToString("N")[..8],
                    $"owner-{Guid.NewGuid():N}@it.test", "Passw0rd-new", "Owner", null, null));
        }

        await using var db = database.CreateContext();
        Assert.True(await db.Set<TenantPreferences>().AnyAsync(p => p.TenantId == created.Id));
        var admin = await db.Users.AsNoTracking().SingleAsync(u => u.TenantId == created.Id);
        Assert.Equal((UiMode.Full, TextScale.Normal), (admin.UiMode, admin.TextScale));
    }

    [PostgresFact]
    public async Task SeedRunner_FillsTenantsWithoutARow_AndASecondRunChangesNothing()
    {
        var tenantId = await AddTenantAsync(withPreferences: false);

        await using (var scope = Scope(null, canSeeMoney: true))
        {
            // همان مسیری که StartupPipeline برای کار استارت می‌رود
            var task = scope.ServiceProvider.GetServices<IStartupTask>().OfType<TenantSeedRunner>().Single();
            await task.RunAsync(scope.ServiceProvider, CancellationToken.None);
        }

        await using (var scope = Scope(null, canSeeMoney: true))
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<TenantSeedRunner>().SeedAllTenantsAsync());

        await using var db = database.CreateContext();
        Assert.Equal(1, await db.Set<TenantPreferences>().CountAsync(p => p.TenantId == tenantId));
        Assert.Equal(await db.Tenants.CountAsync(), await db.Set<TenantPreferences>().CountAsync());
    }

    [PostgresFact]
    public async Task TenantPreferences_SaveWithVersion_StaleVersionIs409_AndAuditHasDetails()
    {
        var tenantId = await AddTenantAsync(withPreferences: true);

        uint version;
        await using (var scope = Scope(tenantId, canSeeMoney: true))
        {
            var service = scope.ServiceProvider.GetRequiredService<ITenantPreferencesService>();
            var current = await service.GetAsync(tenantId);
            Assert.NotEqual(0u, current.Version);
            Assert.False(current.MoneyScaleLocked);

            var saved = await service.UpdateAsync(tenantId, current with { BaleTerm = BaleTerm.Adl, SerialStart = 3778, MoneyScale = 2 });
            Assert.NotEqual(current.Version, saved.Version);
            Assert.Equal((BaleTerm.Adl, 3778, (short)2), (saved.BaleTerm, saved.SerialStart, saved.MoneyScale));
            version = current.Version;
        }

        await using (var scope = Scope(tenantId, canSeeMoney: true))
        {
            var service = scope.ServiceProvider.GetRequiredService<ITenantPreferencesService>();
            var fresh = await service.GetAsync(tenantId);
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
                service.UpdateAsync(tenantId, fresh with { Version = version, SkuPrefix = "OLD" }));
        }

        await using var db = database.CreateContext();
        var entry = await db.AuditEntries.AsNoTracking()
            .SingleAsync(e => e.TenantId == tenantId && e.Action == AuditAction.PreferencesChanged);
        using var details = JsonDocument.Parse(entry.DetailsJson!);
        var changes = details.RootElement.GetProperty("changes");
        Assert.Equal("Negleh", changes.GetProperty("baleTerm").GetProperty("from").GetString());
        Assert.Equal("Adl", changes.GetProperty("baleTerm").GetProperty("to").GetString());
        Assert.Equal(3778, changes.GetProperty("serialStart").GetProperty("to").GetInt32());
        Assert.Equal("RUG", (await db.Set<TenantPreferences>().SingleAsync(p => p.TenantId == tenantId)).SkuPrefix);
    }

    [PostgresFact]
    public async Task MoneyScale_IsLocked_OnceAnyMoneyExists_EvenOnATrashedRug()
    {
        var tenantId = await AddTenantAsync(withPreferences: true);

        await using (var db = database.CreateContext())
        {
            // از فاز ۲ قیمت خرید یک سطر خرید است (نه ستون منسوخ Rug.PurchaseCost)؛ فرش در سطل زباله هم پول دارد
            var rug = new Rug
            {
                TenantId = tenantId,
                Sku = "RUG-IT-" + Guid.NewGuid().ToString("N")[..6],
                DeletedAt = DateTimeOffset.UtcNow
            };
            rug.SetIntakeSize(1m, 1m);
            db.Rugs.Add(rug);
            db.Set<RugPurchaseLine>().Add(new RugPurchaseLine
            {
                TenantId = tenantId,
                RugId = rug.Id,
                Label = RugPurchaseLine.LegacyPurchaseLabel,
                Basis = CostBasis.Total,
                Rate = 100m,
                Amount = 100m
            });
            await db.SaveChangesAsync();
        }

        await using var scope = Scope(tenantId, canSeeMoney: true);
        var service = scope.ServiceProvider.GetRequiredService<ITenantPreferencesService>();
        var current = await service.GetAsync(tenantId);
        Assert.True(current.MoneyScaleLocked);

        var ex = await Assert.ThrowsAsync<DomainConflictException>(() => service.UpdateAsync(tenantId, current with { MoneyScale = 2 }));
        Assert.Equal("moneyScale", ex.Field);
    }

    [PostgresFact]
    public async Task MoneyScale_IsFree_ForATenantWithoutMoney_AndLocksOnAProviderRate()
    {
        var tenantId = await AddTenantAsync(withPreferences: true);

        await using (var scope = Scope(tenantId, canSeeMoney: true))
            Assert.False(await scope.ServiceProvider.GetRequiredService<ITenantPreferencesRepository>().HasStoredMoneyAsync(tenantId));

        await using (var db = database.CreateContext())
        {
            var stepType = new ProcessStepType { TenantId = tenantId, Code = "it_lock", NameFa = "x", NameEn = "x" };
            var provider = new RugsManagment.Domain.Entities.ServiceProvider { TenantId = tenantId, Name = "p" };
            db.ProcessStepTypes.Add(stepType);
            db.ServiceProviders.Add(provider);
            db.ServiceProviderRates.Add(new ServiceProviderRate
            {
                ServiceProviderId = provider.Id, ProcessStepTypeId = stepType.Id, PricingModel = StepPricingModel.Fixed, UnitRate = 5m
            });
            await db.SaveChangesAsync();
        }

        await using (var scope = Scope(tenantId, canSeeMoney: true))
            Assert.True(await scope.ServiceProvider.GetRequiredService<ITenantPreferencesRepository>().HasStoredMoneyAsync(tenantId));
    }

    [PostgresFact]
    public async Task ActivityLog_PagesInSql_NewestFirst_AndFiltersByAction()
    {
        var tenantId = await AddTenantAsync(withPreferences: true);
        var other = await AddTenantAsync(withPreferences: true);

        await using (var db = database.CreateContext())
        {
            var start = DateTimeOffset.UtcNow.AddHours(-1);
            for (var i = 0; i < 25; i++)
            {
                db.AuditEntries.Add(new AuditEntry
                {
                    TenantId = tenantId,
                    Action = i % 5 == 0 ? AuditAction.SaleRecorded : AuditAction.Updated,
                    EntityType = "Rug",
                    Summary = $"e{i:00}",
                    CreatedAt = start.AddMinutes(i)
                });
            }
            for (var i = 0; i < 3; i++)
                db.AuditEntries.Add(new AuditEntry { TenantId = other, Action = AuditAction.Updated, EntityType = "Rug", Summary = "other" });
            await db.SaveChangesAsync();
        }

        await using var scope = Scope(tenantId, canSeeMoney: true);
        var service = scope.ServiceProvider.GetRequiredService<IActivityLogService>();

        var page2 = await service.ListAsync(tenantId, new ActivityQuery { Page = 2, PageSize = 10 });
        Assert.Equal(25, page2.TotalCount);
        Assert.Equal(3, page2.TotalPages);
        // تازه‌ترین اول: صفحهٔ ۱ = e24..e15، صفحهٔ ۲ = e14..e05
        Assert.Equal(Enumerable.Range(10, 10).Select(i => $"e{24 - i:00}"), page2.Items.Select(e => e.Summary));

        var sales = await service.ListAsync(tenantId, new ActivityQuery { Action = AuditAction.SaleRecorded, PageSize = 50 });
        Assert.Equal(5, sales.TotalCount);
        Assert.All(sales.Items, e => Assert.Equal("ثبت فروش", e.ActionLabel));

        // offset غیر صفر هم پذیرفته می‌شود (به UTC برده می‌شود)
        var tehran = TimeSpan.FromHours(3.5);
        var recent = await service.ListAsync(tenantId, new ActivityQuery
        {
            From = DateTimeOffset.UtcNow.AddMinutes(-40).ToOffset(tehran), PageSize = 50
        });
        Assert.InRange(recent.TotalCount, 1, 24);
    }

    [PostgresFact]
    public async Task AuditDetails_AreStoredAsJsonb_WithoutBreakingTheMainSave()
    {
        var tenantId = await AddTenantAsync(withPreferences: true);

        await using (var scope = Scope(tenantId, canSeeMoney: true))
        {
            var audit = scope.ServiceProvider.GetRequiredService<IAuditLog>();
            audit.Record(AuditAction.GridLayoutChanged, "TenantGridLayout", null, "ستون‌ها عوض شد", "intake",
                new { gridId = "intake", columns = new[] { "serial", "seller" } });
            audit.Record(AuditAction.Updated, "Rug", null, "بدون جزئیات");
            audit.Record(AuditAction.Updated, "Rug", null, "جزئیات خراب", details: "{not json");
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        await using var db = database.CreateContext();
        var entries = await db.AuditEntries.AsNoTracking().Where(e => e.TenantId == tenantId).ToListAsync();
        Assert.Equal(3, entries.Count);

        var withDetails = entries.Single(e => e.Action == AuditAction.GridLayoutChanged);
        using var json = JsonDocument.Parse(withDetails.DetailsJson!);
        Assert.Equal("intake", json.RootElement.GetProperty("gridId").GetString());
        Assert.Equal(2, json.RootElement.GetProperty("columns").GetArrayLength());
        Assert.All(entries.Where(e => e.Action == AuditAction.Updated), e => Assert.Null(e.DetailsJson));
    }

    [PostgresFact]
    public async Task HomeStock_UsesTheLotRuleInSql_AndSkipsSoldAndTrashed()
    {
        var tenantId = await AddTenantAsync(withPreferences: true);

        await using (var db = database.CreateContext())
        {
            Rug NewRug(decimal l, decimal w, RugStatus status = RugStatus.Draft, bool trashed = false) => new()
            {
                TenantId = tenantId,
                Sku = "RUG-IT-" + Guid.NewGuid().ToString("N")[..8],
                LengthMeters = l,
                WidthMeters = w,
                Status = status,
                DeletedAt = trashed ? DateTimeOffset.UtcNow : null
            };

            db.Rugs.AddRange(
                NewRug(2.30m, 1.65m),                          // ۳٫۷۹۵ → ۳٫۸۰
                NewRug(0.77m, 0.50m, RugStatus.InProgress),    // ۰٫۳۸۵ → ۰٫۳۹ (ToEven می‌داد ۰٫۳۸)
                NewRug(1.005m, 1m),                            // ضلع سه‌رقمیِ قدیمی: ۱٫۰۰۵ → ۱٫۰۱ → ۱٫۰۱
                NewRug(3m, 3m, RugStatus.Sold),
                NewRug(4m, 4m, trashed: true));
            await db.SaveChangesAsync();
        }

        await using var scope = Scope(tenantId, canSeeMoney: false);
        var stock = await scope.ServiceProvider.GetRequiredService<IHomeStockSource>().GetAsync(tenantId);

        var expected = AreaMath.PieceArea(2.30m, 1.65m) + AreaMath.PieceArea(0.77m, 0.50m) + AreaMath.PieceArea(1.005m, 1m);
        Assert.Equal(5.20m, expected);
        Assert.Equal(new HomeStock(3, expected), stock);
    }

    [PostgresFact]
    public async Task HomeSummary_ForOperator_HasNoMoney_AndNoMoneyEvents()
    {
        var tenantId = await AddTenantAsync(withPreferences: true);
        await using (var db = database.CreateContext())
        {
            db.AuditEntries.Add(new AuditEntry { TenantId = tenantId, Action = AuditAction.SaleRecorded, EntityType = "Rug", Summary = "sale" });
            db.AuditEntries.Add(new AuditEntry { TenantId = tenantId, Action = AuditAction.ProviderPaid, EntityType = "ServiceProvider", Summary = "paid" });
            db.AuditEntries.Add(new AuditEntry { TenantId = tenantId, Action = AuditAction.StepAdvanced, EntityType = "Rug", Summary = "step" });
            await db.SaveChangesAsync();
        }

        await using (var scope = Scope(tenantId, canSeeMoney: false))
        {
            var summary = await scope.ServiceProvider.GetRequiredService<IHomeSummaryService>().GetAsync(tenantId);
            Assert.Empty(summary.OpenShipments);
            Assert.Empty(summary.Readiness);
            Assert.Null(summary.OpenShipmentCostPerM2);
            Assert.Null(summary.UnallocatedEntries);
            Assert.Equal(new[] { "step" }, summary.Recent.Select(r => r.Summary));
        }

        await using (var scope = Scope(tenantId, canSeeMoney: true))
        {
            var summary = await scope.ServiceProvider.GetRequiredService<IHomeSummaryService>().GetAsync(tenantId);
            Assert.Equal(3, summary.Recent.Count);
        }
    }

    [PostgresFact]
    public async Task RugSearch_ForOperator_HidesInvestment_AndCostSortFallsBackToCreatedAt()
    {
        var tenantId = await AddTenantAsync(withPreferences: true);
        await using (var db = database.CreateContext())
        {
            var start = DateTimeOffset.UtcNow.AddHours(-2);
            for (var i = 0; i < 3; i++)
            {
                var rug = new Rug
                {
                    TenantId = tenantId,
                    Sku = $"RUG-IT-S{i}",
                    WidthMeters = 1m,
                    LengthMeters = 1m,
                    CreatedAt = start.AddMinutes(-i)
                };
                db.Rugs.Add(rug);
                // ارزان‌ترین تازه‌ترین است، تا ترتیب «هزینه» و «تاریخ» با هم فرق کنند (از فاز ۲ قیمت خرید سطر خرید است)
                db.Set<RugPurchaseLine>().Add(new RugPurchaseLine
                {
                    TenantId = tenantId, RugId = rug.Id, Label = RugPurchaseLine.LegacyPurchaseLabel,
                    Basis = CostBasis.Total, Rate = (i + 1) * 1000m
                });
            }
            await db.SaveChangesAsync();
        }

        // ذخیرهٔ مستقیم DbContext از پاس سازگاری نمی‌گذرد؛ ستون «سرمایهٔ کل» فهرست (RugCostTotal) را بازسازی می‌سازد
        await using (var scope = Scope(tenantId, canSeeMoney: true))
            await scope.ServiceProvider.GetRequiredService<ILedgerMaintenance>().RebuildAsync(tenantId);

        var query = new RugQuery { SortBy = RugSortBy.TotalCost, Descending = true };

        await using (var scope = Scope(tenantId, canSeeMoney: true))
        {
            var page = await scope.ServiceProvider.GetRequiredService<IRugManagementService>().SearchAsync(tenantId, query);
            Assert.Equal(new decimal?[] { 3000m, 2000m, 1000m }, page.Items.Select(i => i.TotalInvestment));
        }

        await using (var scope = Scope(tenantId, canSeeMoney: false))
        {
            var page = await scope.ServiceProvider.GetRequiredService<IRugManagementService>().SearchAsync(tenantId, query);
            Assert.All(page.Items, i => Assert.Null(i.TotalInvestment));
            Assert.Equal(new[] { "RUG-IT-S0", "RUG-IT-S1", "RUG-IT-S2" }, page.Items.Select(i => i.Sku)); // تازه‌ترین اول
        }
    }

    [PostgresFact]
    public async Task OperatorRugEdit_KeepsStoredPrices_AndDashboardHasNoMoney()
    {
        var tenantId = await AddTenantAsync(withPreferences: true);
        var rug = new Rug
        {
            TenantId = tenantId, Sku = "RUG-IT-E5", Title = "old", WidthMeters = 2m, LengthMeters = 3m,
            TargetSalePrice = 9000m
        };
        await using (var db = database.CreateContext())
        {
            db.Rugs.Add(rug);
            // از فاز ۲ قیمت خرید سطر خرید است (ستون قدیمی منسوخ است)
            db.Set<RugPurchaseLine>().Add(new RugPurchaseLine
            {
                TenantId = tenantId, RugId = rug.Id, Label = RugPurchaseLine.LegacyPurchaseLabel,
                Basis = CostBasis.Total, Rate = 5000m, Amount = 5000m
            });
            await db.SaveChangesAsync();
        }

        await using (var scope = Scope(tenantId, canSeeMoney: false))
        {
            var service = scope.ServiceProvider.GetRequiredService<IRugManagementService>();
            var dto = await service.UpdateAsync(tenantId, rug.Id,
                new UpdateRugRequest("new", null, null, null, null, 2m, 3m, null, null, null, null, null));
            Assert.Null(dto.PurchaseCost);
            Assert.Null(dto.Costs);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdateAsync(tenantId, rug.Id,
                new UpdateRugRequest("newer", null, null, null, null, 2m, 3m, 1m, null, null, null, null)));

            var dashboard = await scope.ServiceProvider.GetRequiredService<IDashboardService>().GetTenantDashboardAsync(tenantId);
            Assert.Equal(1, dashboard.TotalRugs);
            Assert.Null(dashboard.TotalInvestment);
            Assert.Null(dashboard.ProfitEstimate);
            Assert.All(dashboard.RecentRugs, r => Assert.Null(r.TotalInvestment));
        }

        await using var check = database.CreateContext();
        var stored = await check.Rugs.AsNoTracking().SingleAsync(r => r.Id == rug.Id);
        var line = await check.Set<RugPurchaseLine>().AsNoTracking().SingleAsync(l => l.RugId == rug.Id);
        Assert.Equal(("new", 5000m, 9000m), (stored.Title, line.Rate, stored.TargetSalePrice!.Value));
    }

    // ── کمکی‌ها ──────────────────────────────────────────────────

    private AsyncServiceScope Scope(Guid? tenantId, bool canSeeMoney)
    {
        var services = _services ?? throw new InvalidOperationException("سرویس‌ها ساخته نشده‌اند.");
        var scope = services.CreateAsyncScope();
        var user = scope.ServiceProvider.GetRequiredService<FakeCurrentUser>();
        user.TenantId = tenantId;
        user.UserId = tenantId is null ? null : Guid.NewGuid();
        user.DisplayName = "IT user";
        scope.ServiceProvider.GetRequiredService<SwitchableMoneyVisibility>().CanSeeMoney = canSeeMoney;
        return scope;
    }

    private async Task<Guid> AddTenantAsync(bool withPreferences)
    {
        await using var db = database.CreateContext();
        var tenant = new Tenant { Name = "IT platform", Slug = "it-p1-" + Guid.NewGuid().ToString("N")[..10] };
        db.Tenants.Add(tenant);
        if (withPreferences)
            db.Set<TenantPreferences>().Add(TenantPreferences.CreateDefault(tenant.Id));
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    /// <summary>همان ثبت‌های Program.cs برای Application و Infrastructure؛ فقط کاربر و بیننده ساختگی‌اند.</summary>
    private static DiServiceProvider BuildServices(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["Database:MigrateOnStartup"] = "false",
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
        services.AddScoped<SwitchableMoneyVisibility>();
        services.AddScoped<IMoneyVisibility>(sp => sp.GetRequiredService<SwitchableMoneyVisibility>());

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
}
