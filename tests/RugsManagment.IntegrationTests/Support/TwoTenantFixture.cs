using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RugsManagment.Application;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.Infrastructure;
using RugsManagment.Infrastructure.Persistence;
using DiServiceProvider = Microsoft.Extensions.DependencyInjection.ServiceProvider;

namespace RugsManagment.IntegrationTests.Support;

/// <summary>شناسه‌های دادهٔ اولیهٔ یک کارگاه در <see cref="TwoTenantFixture"/>.</summary>
public sealed record TenantSeed(
    string Label,
    Guid TenantId,
    Guid AdminUserId,
    string AdminName,
    Guid PrivateStepTypeId,
    Guid ProviderId,
    Guid BatchId,
    Guid CustomFieldId,
    Guid LabelId,
    Guid WorkflowTemplateId);

/// <summary>
/// دو کارگاه کامل (A و B) در یک دیتابیس دورریختنی، به‌اضافهٔ یک نوع مرحلهٔ سیستمی مشترک،
/// و سرویس‌ها با DI واقعیِ برنامه (AddApplication + AddInfrastructure) — همان گرافی که Web می‌سازد،
/// فقط ICurrentUser ساختگی است. هدف: تست‌های رگرسیون جداسازی کارگاه‌ها روی پستگرس واقعی.
///
/// هر کارگاه: یک مدیر، یک نوع مرحلهٔ اختصاصی، یک طرف خدمات (با نرخ توافقی روی مرحلهٔ سیستمی)،
/// یک گروه، یک فیلد سفارشی، یک قالب برچسب و یک قالب گردش کار.
/// </summary>
public sealed class TwoTenantFixture : IAsyncLifetime
{
    /// <summary>نرخ پیش‌فرض مرحلهٔ سیستمی (به ازای m²).</summary>
    public const decimal SystemDefaultRate = 600m;

    /// <summary>نرخ توافقیِ طرف خدماتِ هر کارگاه برای مرحلهٔ سیستمی (به ازای m²).</summary>
    public const decimal ProviderAgreedRate = 1200m;

    public const string AdminPassword = "it-Passw0rd-1";

    private readonly MigratedTestDatabase _database = new();
    private DiServiceProvider? _services;

    public TenantSeed A { get; private set; } = null!;
    public TenantSeed B { get; private set; } = null!;
    public Guid SystemStepTypeId { get; private set; }

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        if (PostgresTestEnvironment.ShouldSkip)
            return; // تست‌ها با PostgresFact رد می‌شوند

        _services = BuildRealServices(_database.ConnectionString);
        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_services is not null)
            await _services.DisposeAsync();
        await _database.DisposeAsync();
    }

    /// <summary>DbContext مستقل برای خواندن وضعیت واقعی دیتابیس (بدون کش ردیابی سرویس‌ها).</summary>
    public AppDbContext CreateContext() => _database.CreateContext();

    /// <summary>یک «درخواست» از طرف مدیرِ کارگاه داده‌شده.</summary>
    public AsyncServiceScope As(TenantSeed tenant)
        => CreateScope(tenant.TenantId, tenant.AdminUserId, tenant.AdminName);

    /// <summary>یک «درخواست» بدون کارگاه (مثل ادمین سیستم).</summary>
    public AsyncServiceScope AsSystemAdmin() => CreateScope(null, null, "System admin");

    /// <summary>
    /// فرش تازه برای کارگاه با یک مرحلهٔ سیستمی (در حال انجام). در scope جدا ساخته می‌شود تا
    /// درخواستِ بعدیِ تست مثل یک درخواست HTTP مستقل با DbContext خالی شروع شود.
    /// </summary>
    public async Task<RugDto> CreateRugAsync(TenantSeed tenant, Guid? batchId = null)
    {
        await using var scope = As(tenant);
        var service = scope.ServiceProvider.GetRequiredService<IRugManagementService>();
        var rug = await service.CreateAsync(tenant.TenantId, NewRugRequest([new(SystemStepTypeId, false, null)]));

        if (batchId is Guid id)
        {
            await scope.ServiceProvider.GetRequiredService<IRugBatchService>()
                .AssignRugsAsync(tenant.TenantId, id, new([rug.Id]));
        }

        return rug;
    }

    /// <summary>درخواست ثبت فرش ۲×۳ متر با مراحل دستیِ داده‌شده.</summary>
    public static CreateRugRequest NewRugRequest(IReadOnlyList<CustomRugStepRequest> steps) => new(
        Title: "IT rug",
        Origin: null,
        Pattern: null,
        Material: null,
        KnotDensity: null,
        WidthMeters: 2m,
        LengthMeters: 3m,
        PurchaseCost: 1000m,
        TargetSalePrice: null,
        ImageUrl: null,
        Notes: null,
        WorkflowTemplateId: null,
        SkippedOptionalStepIds: null,
        CustomSteps: steps);

    private AsyncServiceScope CreateScope(Guid? tenantId, Guid? userId, string displayName)
    {
        var services = _services ?? throw new InvalidOperationException("فیکسچر مقداردهی نشده است.");
        var scope = services.CreateAsyncScope();
        var currentUser = scope.ServiceProvider.GetRequiredService<FakeCurrentUser>();
        currentUser.TenantId = tenantId;
        currentUser.UserId = userId;
        currentUser.DisplayName = displayName;
        return scope;
    }

    /// <summary>
    /// همان ثبت‌هایی که Program.cs برای لایه‌های Application و Infrastructure انجام می‌دهد.
    /// ValidateOnBuild هر نام‌گذاری/ثبتِ ناجور بین دو لایه را همین‌جا آشکار می‌کند.
    /// </summary>
    private static DiServiceProvider BuildRealServices(string connectionString)
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

        // در Web این HttpCurrentUser است (از کوکی)؛ اینجا هر scope خودش مقدار می‌گیرد
        services.AddScoped<FakeCurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<FakeCurrentUser>());

        // در Web از سیاست Business ساخته می‌شود؛ Application عمداً پیش‌فرض ندارد تا میزبانی که
        // ثبتش را فراموش کند هنگام شروع بشکند، نه اینکه پول را بی‌صدا به انباردار نشان دهد.
        // این تست‌ها با مدیر کارگاه اجرا می‌شوند، پس دید کامل پول درست است.
        services.AddSingleton<RugsManagment.Application.Common.IMoneyVisibility>(
            RugsManagment.Application.Common.FullMoneyVisibility.Instance);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private async Task SeedAsync()
    {
        await using var db = CreateContext();

        var systemType = new ProcessStepType
        {
            TenantId = null,
            Code = "it_system_wash",
            NameFa = "شست‌وشوی سیستمی",
            NameEn = "System wash",
            DefaultPricingModel = StepPricingModel.PerSquareMeter,
            DefaultUnitRate = SystemDefaultRate
        };
        db.ProcessStepTypes.Add(systemType);
        SystemStepTypeId = systemType.Id;

        A = AddTenant(db, "A", systemType);
        B = AddTenant(db, "B", systemType);

        await db.SaveChangesAsync();
    }

    private static TenantSeed AddTenant(AppDbContext db, string label, ProcessStepType systemType)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];

        var tenant = new Tenant { Name = $"IT tenant {label}", Slug = $"it-{label.ToLowerInvariant()}-{suffix}" };
        var admin = new User
        {
            TenantId = tenant.Id,
            Email = $"admin-{label.ToLowerInvariant()}-{suffix}@it.test",
            FullName = $"Admin {label}",
            Role = UserRole.TenantAdmin,
            PasswordHash = AuthService.HashPassword(AdminPassword),
            IsActive = true
        };
        var privateType = new ProcessStepType
        {
            TenantId = tenant.Id,
            Code = "it_private",
            NameFa = $"مرحلهٔ اختصاصی {label}",
            NameEn = $"Private {label}",
            DefaultPricingModel = StepPricingModel.Fixed,
            DefaultUnitRate = 100m
        };
        var provider = new RugsManagment.Domain.Entities.ServiceProvider { TenantId = tenant.Id, Name = $"Provider {label}" };
        var rate = new ServiceProviderRate
        {
            ServiceProviderId = provider.Id,
            ProcessStepTypeId = systemType.Id,
            PricingModel = StepPricingModel.PerSquareMeter,
            UnitRate = ProviderAgreedRate
        };
        var batch = new RugBatch { TenantId = tenant.Id, Name = $"Batch {label}", ReceivedAt = DateTimeOffset.UtcNow };
        var field = new CustomFieldDefinition
        {
            TenantId = tenant.Id,
            Key = "border_color",
            Label = $"Border {label}",
            FieldType = CustomFieldType.Text
        };
        var labelTemplate = new LabelTemplate { TenantId = tenant.Id, Name = $"Label {label}" };
        var template = new WorkflowTemplate { TenantId = tenant.Id, Name = $"Template {label}" };

        db.Tenants.Add(tenant);
        db.Users.Add(admin);
        db.ProcessStepTypes.Add(privateType);
        db.ServiceProviders.Add(provider);
        db.ServiceProviderRates.Add(rate);
        db.RugBatches.Add(batch);
        db.CustomFieldDefinitions.Add(field);
        db.LabelTemplates.Add(labelTemplate);
        db.WorkflowTemplates.Add(template);

        return new TenantSeed(
            label, tenant.Id, admin.Id, admin.FullName, privateType.Id, provider.Id,
            batch.Id, field.Id, labelTemplate.Id, template.Id);
    }
}
