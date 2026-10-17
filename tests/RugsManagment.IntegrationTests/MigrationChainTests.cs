using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RugsManagment.Infrastructure.Persistence;
using RugsManagment.IntegrationTests.Support;

namespace RugsManagment.IntegrationTests;

/// <summary>
/// زنجیرهٔ migration روی دیتابیس خالی (مسیری که قبلاً با 42701 می‌شکست) و مسیر ارتقای دیتابیسِ
/// موجودِ صاحب پروژه: همهٔ migrationها جز PendingModelSync اجرا شده‌اند و کاربرانی بدون مهر امنیتی دارد.
/// </summary>
public sealed class MigrationChainTests(TestDatabase database) : IClassFixture<TestDatabase>
{
    private const string PendingModelSync = "20260604082452_PendingModelSync";
    private const string LastBeforeM0 = "20260728181348_SoftDeleteRugs";
    private const string CatalogPatternsLabels = "20260603141744_CatalogPatternsLabels";

    [PostgresFact]
    public async Task OwnerDatabaseShape_UpgradesCleanly_BackfillsDistinctStamps_AndReapplyIsNoOp()
    {
        await using var db = database.CreateContext();
        var migrator = db.GetService<IMigrator>();

        // ۱) کل زنجیرهٔ قدیمی روی دیتابیس خالی؛ پیش از اصلاح، SyncModelState همین‌جا با 42701 می‌شکست
        await migrator.MigrateAsync(LastBeforeM0);

        // ۲) شکل دیتابیس صاحب پروژه: PendingModelSync هرگز اجرا نشده ولی ستون‌ها هستند
        await db.Database.ExecuteSqlAsync(
            $"""DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = {PendingModelSync}""");

        // ۳) کاربران قدیمی، پیش از وجود ستون SecurityStamp (پس با SQL خام، نه با مدل فعلی EF)
        var firstUser = Guid.NewGuid();
        var secondUser = Guid.NewGuid();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "Users" ("Id", "Email", "PasswordHash", "FullName", "Role", "IsActive", "CreatedAt")
            VALUES ({firstUser}, 'first@it.local', 'x', 'First', 0, true, now()),
                   ({secondUser}, 'second@it.local', 'x', 'Second', 2, true, now())
            """);

        // ۴) ارتقا: PendingModelSync (بی‌اثر) + M0
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        Assert.Contains(PendingModelSync, pending);
        Assert.Contains(pending, id => id.EndsWith("_CountersAndSecurityStamp", StringComparison.Ordinal));

        await db.Database.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(3, await CountPricingColumnsAsync(db));

        var stamps = await db.Users.AsNoTracking().Select(u => u.SecurityStamp).ToListAsync();
        Assert.Equal(2, stamps.Count);
        Assert.All(stamps, stamp => Assert.Equal(32, stamp.Length)); // md5 به hex
        Assert.Equal(2, stamps.Distinct(StringComparer.Ordinal).Count());
        Assert.False(await IsNullableAsync(db, "Users", "SecurityStamp"));

        // ۵) اجرای دوباره هیچ کاری نمی‌کند
        var historyBefore = await CountHistoryAsync(db);
        await db.Database.MigrateAsync();
        Assert.Equal(historyBefore, await CountHistoryAsync(db));
        Assert.Equal(db.Database.GetMigrations().Count(), historyBefore);
    }

    [PostgresFact]
    public async Task M0_DownThenUp_RoundTrips()
    {
        // دیتابیس جدا تا ترتیب اجرای تست‌های این کلاس اهمیتی نداشته باشد
        var fresh = new TestDatabase();
        await fresh.InitializeAsync();
        try
        {
            await using var db = fresh.CreateContext();
            await db.Database.MigrateAsync();

            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(LastBeforeM0);
            Assert.False(await TableExistsAsync(db, "TenantCounters"));

            await db.Database.MigrateAsync();
            Assert.True(await TableExistsAsync(db, "TenantCounters"));
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        }
        finally
        {
            await fresh.DisposeAsync();
        }
    }

    [PostgresFact]
    public async Task RollbackToPendingModelSync_KeepsThePricingColumnsItOwns()
    {
        // SyncModelState فقط تکرارِ PendingModelSync است؛ برگشت تا PendingModelSync نباید ستون‌هایی را که
        // مدلِ همان migration دارد (و داده‌شان را) پاک کند. Down صاحب ستون‌ها (PendingModelSync) پاکشان می‌کند.
        await WithFreshDatabaseAsync(async db =>
        {
            await db.Database.MigrateAsync();
            await db.GetService<IMigrator>().MigrateAsync(PendingModelSync);

            Assert.Equal(PendingModelSync, (await db.Database.GetAppliedMigrationsAsync()).Last());
            Assert.Equal(3, await CountPricingColumnsAsync(db));

            await db.Database.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Equal(3, await CountPricingColumnsAsync(db));
        });
    }

    [PostgresFact]
    public async Task FullChain_UpThenDownToCatalogPatternsLabelsThenUpAgain_Succeeds()
    {
        await WithFreshDatabaseAsync(async db =>
        {
            var migrator = db.GetService<IMigrator>();

            await migrator.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());

            await migrator.MigrateAsync(CatalogPatternsLabels);
            Assert.Equal(CatalogPatternsLabels, (await db.Database.GetAppliedMigrationsAsync()).Last());
            Assert.False(await TableExistsAsync(db, "TenantCounters"));

            await migrator.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Equal(db.Database.GetMigrations().Count(), await CountHistoryAsync(db));
            Assert.Equal(3, await CountPricingColumnsAsync(db));
            Assert.True(await TableExistsAsync(db, "TenantCounters"));
            Assert.False(db.Database.HasPendingModelChanges());
        });
    }

    /// <summary>دیتابیس جدا برای هر تست، تا ترتیب اجرای تست‌های این کلاس اهمیتی نداشته باشد.</summary>
    private static async Task WithFreshDatabaseAsync(Func<AppDbContext, Task> body)
    {
        var fresh = new TestDatabase();
        await fresh.InitializeAsync();
        try
        {
            await using var db = fresh.CreateContext();
            await body(db);
        }
        finally
        {
            await fresh.DisposeAsync();
        }
    }

    private static async Task<int> CountHistoryAsync(AppDbContext db)
        => await db.Database
            .SqlQuery<int>($"""SELECT count(*)::int AS "Value" FROM "__EFMigrationsHistory" """)
            .SingleAsync();

    private static async Task<int> CountPricingColumnsAsync(AppDbContext db)
        => await db.Database
            .SqlQuery<int>($"""
                SELECT count(*)::int AS "Value" FROM information_schema.columns
                WHERE table_schema = current_schema() AND table_name = 'RugWorkflowSteps'
                  AND column_name IN ('AppliedPricingModel', 'AppliedUnitRate', 'PricingConfigJson')
                """)
            .SingleAsync();

    private static async Task<bool> IsNullableAsync(AppDbContext db, string table, string column)
        => await db.Database
            .SqlQuery<bool>($"""
                SELECT is_nullable = 'YES' AS "Value" FROM information_schema.columns
                WHERE table_schema = current_schema() AND table_name = {table} AND column_name = {column}
                """)
            .SingleAsync();

    private static async Task<bool> TableExistsAsync(AppDbContext db, string table)
        => await db.Database
            .SqlQuery<bool>($"""
                SELECT EXISTS (SELECT 1 FROM pg_catalog.pg_tables
                               WHERE schemaname = current_schema() AND tablename = {table}) AS "Value"
                """)
            .SingleAsync();
}
