using Microsoft.EntityFrameworkCore;
using RugsManagment.Domain.Entities;
using RugsManagment.Infrastructure.Persistence;
using RugsManagment.Infrastructure.Persistence.Repositories;
using RugsManagment.IntegrationTests.Support;

namespace RugsManagment.IntegrationTests;

/// <summary>
/// شمارندهٔ اتمیک: هم‌زمانی، بلوک، و ادامهٔ SKU از دادهٔ قدیمی.
/// هر تست کارگاه خودش را می‌سازد تا تست‌های هم‌کلاس روی یک دیتابیس با هم تداخل نکنند.
/// </summary>
public sealed class TenantCounterTests(MigratedTestDatabase database) : IClassFixture<MigratedTestDatabase>
{
    [PostgresFact]
    public async Task FiftyParallelCalls_ReturnDistinctValuesOneToFifty()
    {
        var tenantId = await CreateTenantAsync();

        // هر فراخوانی DbContext و اتصال خودش را دارد — مثل ۵۰ درخواست HTTP هم‌زمان
        var values = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Task.Run(async () =>
        {
            await using var db = database.CreateContext();
            return await new TenantCounterRepository(db).NextBlockAsync(tenantId, "shipment", 1);
        })));

        Assert.Equal(Enumerable.Range(1, 50).Select(i => (long)i), values.Order());
    }

    [PostgresFact]
    public async Task Block_ReturnsItsLastValue_AndNextCallContinuesAfterIt()
    {
        var tenantId = await CreateTenantAsync();
        await using var db = database.CreateContext();
        var counters = new TenantCounterRepository(db);

        Assert.Equal(10, await counters.NextBlockAsync(tenantId, "batch", 10)); // بلوک 1..10
        Assert.Equal(11, await counters.NextBlockAsync(tenantId, "batch"));
        Assert.Equal(16, await counters.NextBlockAsync(tenantId, "batch", 5)); // بلوک 12..16
    }

    [PostgresFact]
    public async Task Counters_AreIndependentPerTenantAndPerKey()
    {
        var first = await CreateTenantAsync();
        var second = await CreateTenantAsync();
        await using var db = database.CreateContext();
        var counters = new TenantCounterRepository(db);

        Assert.Equal(3, await counters.NextBlockAsync(first, "import", 3));
        Assert.Equal(1, await counters.NextBlockAsync(second, "import"));
        Assert.Equal(1, await counters.NextBlockAsync(first, "cost"));
        Assert.Equal(4, await counters.NextBlockAsync(first, "import"));
    }

    [PostgresFact]
    public async Task FirstSkuUse_ContinuesAfterHighestExistingSuffix_IncludingTrashedRugs()
    {
        var tenantId = await CreateTenantAsync();
        var otherTenantId = await CreateTenantAsync();

        await using (var seed = database.CreateContext())
        {
            seed.Rugs.AddRange(
                Rug(tenantId, "RUG-202610-0007"),
                Rug(tenantId, "RUG-202610-0012", trashed: true), // سطل زباله هم جای SKU را گرفته
                Rug(tenantId, "RUG-202610-X001"),                 // پسوند غیرعددی نادیده گرفته می‌شود
                Rug(tenantId, "RUG-202609-0099"),                 // ماه دیگر
                Rug(otherTenantId, "RUG-202610-0500"));           // کارگاه دیگر
            await seed.SaveChangesAsync();
        }

        await using var db = database.CreateContext();
        var counters = new TenantCounterRepository(db);

        Assert.Equal(13, await counters.NextBlockAsync(tenantId, "sku:202610"));
        Assert.Equal(14, await counters.NextBlockAsync(tenantId, "sku:202610"));
        Assert.Equal(17, await counters.NextBlockAsync(tenantId, "sku:202610", 3));
        Assert.Equal(1, await counters.NextBlockAsync(tenantId, "sku:202611"));
        Assert.Equal(501, await counters.NextBlockAsync(otherTenantId, "sku:202610"));
    }

    private async Task<Guid> CreateTenantAsync()
    {
        await using var db = database.CreateContext();
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var tenant = new Tenant { Name = "IT " + suffix, Slug = "it-" + suffix };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    private static Rug Rug(Guid tenantId, string sku, bool trashed = false) => new()
    {
        TenantId = tenantId,
        Sku = sku,
        WidthMeters = 1,
        LengthMeters = 1,
        DeletedAt = trashed ? DateTimeOffset.UtcNow : null
    };
}

/// <summary>اعتبارسنجی ورودی شمارنده — بدون دیتابیس، پس همیشه اجرا می‌شود.</summary>
public sealed class TenantCounterValidationTests
{
    [Fact]
    public async Task InvalidArguments_AreRejectedBeforeTouchingTheDatabase()
    {
        // رشتهٔ اتصال عمداً نامعتبر است: اعتبارسنجی باید پیش از هر اتصالی خطا بدهد
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=invalid.invalid;Database=nothing").Options);
        var counters = new TenantCounterRepository(db);

        await Assert.ThrowsAsync<ArgumentException>(
            () => counters.NextBlockAsync(Guid.NewGuid(), new string('k', TenantCounterRepository.MaxKeyLength + 1)));
        await Assert.ThrowsAsync<ArgumentException>(() => counters.NextBlockAsync(Guid.NewGuid(), " "));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => counters.NextBlockAsync(Guid.NewGuid(), "shipment", 0));
    }
}
