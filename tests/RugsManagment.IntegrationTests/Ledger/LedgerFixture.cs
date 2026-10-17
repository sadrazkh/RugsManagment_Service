using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RugsManagment.Application;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.Infrastructure;
using RugsManagment.Infrastructure.Persistence;
using RugsManagment.IntegrationTests.Support;
using DiServiceProvider = Microsoft.Extensions.DependencyInjection.ServiceProvider;

namespace RugsManagment.IntegrationTests.Ledger;

/// <summary>
/// دیتابیس دورریختنی با همهٔ migrationها و DI واقعیِ برنامه (AddApplication + AddInfrastructure) برای آزمون‌های پاس سازگاری
/// (spec D.4، L.2). هر «درخواست» یک scope تازه است (DbContext تازه)، مثل یک درخواست HTTP. کارگاه‌ها و فرش‌ها با همان مسیر
/// برنامه (IRugRepository + IUnitOfWork) ساخته می‌شوند تا پاس روی آن‌ها اجرا شود.
/// </summary>
public sealed class LedgerFixture : IAsyncLifetime
{
    private readonly MigratedTestDatabase _database = new();
    private DiServiceProvider? _services;

    /// <summary>نوع مرحلهٔ سیستمیِ «شستشو» (هر متر ۸۵٬۰۰۰).</summary>
    public Guid WashTypeId { get; private set; }

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        if (PostgresTestEnvironment.ShouldSkip)
            return;

        _services = BuildServices(_database.ConnectionString);

        await using var db = CreateContext();
        var wash = new ProcessStepType
        {
            Code = "it_ledger_wash", NameFa = "شستشو", NameEn = "Wash",
            DefaultPricingModel = StepPricingModel.PerSquareMeter, DefaultUnitRate = 85_000m
        };
        db.ProcessStepTypes.Add(wash);
        await db.SaveChangesAsync();
        WashTypeId = wash.Id;
    }

    public async Task DisposeAsync()
    {
        if (_services is not null)
            await _services.DisposeAsync();
        await _database.DisposeAsync();
    }

    public AppDbContext CreateContext() => _database.CreateContext();

    public IServiceProvider Root => _services ?? throw new InvalidOperationException("فیکسچر مقداردهی نشده است.");

    /// <summary>یک «درخواست» از طرف کارگاه.</summary>
    public AsyncServiceScope Scope(Guid tenantId)
    {
        var scope = Root.CreateAsyncScope();
        var user = scope.ServiceProvider.GetRequiredService<FakeCurrentUser>();
        user.TenantId = tenantId;
        user.UserId = Guid.NewGuid();
        user.DisplayName = "IT ledger";
        return scope;
    }

    /// <summary>کارگاه تازه با ردیف ترجیحات (رقم اعشار داده‌شده)، مستقیم در دیتابیس.</summary>
    public async Task<Guid> NewTenantAsync(short moneyScale = 0, int? serialStart = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var tenant = new Tenant { Name = $"IT ledger {suffix}", Slug = $"it-ledger-{suffix}" };
        await using var db = CreateContext();
        db.Tenants.Add(tenant);
        db.Set<TenantPreferences>().Add(new TenantPreferences { TenantId = tenant.Id, MoneyScale = moneyScale, SerialStart = serialStart });
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    /// <summary>فرش تازه (ساخته‌نشده) با اندازهٔ اول، شماره و تعداد.</summary>
    public static Rug NewRug(Guid tenantId, decimal length, decimal width, string? serial = null, int quantity = 1)
    {
        var rug = new Rug { TenantId = tenantId, Sku = "IT-" + Guid.NewGuid().ToString("N")[..12] };
        rug.SetIntakeSize(length, width);
        rug.SetSerialAndQuantity(SerialNumber.Parse(serial), quantity);
        return rug;
    }

    /// <summary>سطر خرید تازه برای فرش (در گراف فرش و با AddAsync).</summary>
    public static RugPurchaseLine Line(Rug rug, CostBasis basis, decimal rate, int sortOrder = 0, string label = "خرید")
    {
        var line = new RugPurchaseLine
        {
            TenantId = rug.TenantId, RugId = rug.Id, Basis = basis, Rate = rate, SortOrder = sortOrder, Label = label
        };
        rug.PurchaseLines.Add(line);
        return line;
    }

    /// <summary>فرش(ها) را با همان مسیر برنامه (AddAsync + UnitOfWork) ذخیره می‌کند؛ پاس سازگاری اجرا می‌شود.</summary>
    public async Task SaveNewAsync(Guid tenantId, params Rug[] rugs)
    {
        await using var scope = Scope(tenantId);
        var repository = scope.ServiceProvider.GetRequiredService<IRugRepository>();
        var lines = scope.ServiceProvider.GetRequiredService<IRepository<RugPurchaseLine>>();
        var steps = scope.ServiceProvider.GetRequiredService<IRepository<RugWorkflowStep>>();
        foreach (var rug in rugs)
        {
            await repository.AddAsync(rug);
            foreach (var line in rug.PurchaseLines) await lines.AddAsync(line);
            foreach (var step in rug.WorkflowSteps) await steps.AddAsync(step);
        }

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
    }

    /// <summary>ردیف جمع ذخیره‌شده (بی فیلتر سطل زباله).</summary>
    public async Task<RugCostTotal?> TotalAsync(Guid rugId)
    {
        await using var db = CreateContext();
        return await db.Set<RugCostTotal>().IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(t => t.RugId == rugId);
    }

    /// <summary>مبلغ سطرهای خرید یک فرش به ترتیب.</summary>
    public async Task<IReadOnlyList<decimal>> LineAmountsAsync(Guid rugId)
    {
        await using var db = CreateContext();
        return await db.Set<RugPurchaseLine>().IgnoreQueryFilters().AsNoTracking()
            .Where(l => l.RugId == rugId).OrderBy(l => l.SortOrder).Select(l => l.Amount).ToListAsync();
    }

    /// <summary>xmin فعلی فرش در دیتابیس.</summary>
    public async Task<uint> RugVersionAsync(Guid rugId)
    {
        await using var db = CreateContext();
        return await db.Rugs.IgnoreQueryFilters().Where(r => r.Id == rugId).Select(r => EF.Property<uint>(r, "xmin")).SingleAsync();
    }

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
        services.AddSingleton<IMoneyVisibility>(FullMoneyVisibility.Instance);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
