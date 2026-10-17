using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.Infrastructure.Persistence;
using RugsManagment.IntegrationTests.Support;

namespace RugsManagment.IntegrationTests;

/// <summary>
/// دیتابیس دورریختنی فاز سکو: <c>rugs_p1_core_xxxxxxxx</c> (هرگز دیتابیس واقعی <c>rugs_management</c>)، با FORCE پاک
/// می‌شود. <see cref="Migrated"/> = true یعنی بعد از ساخت همهٔ migrationها اجرا می‌شوند.
/// </summary>
public class P1CoreDatabase : IAsyncLifetime
{
    public string DatabaseName { get; } = "rugs_p1_core_" + Guid.NewGuid().ToString("N")[..8];

    public string ConnectionString { get; private set; } = string.Empty;

    protected virtual bool Migrated => false;

    public async Task InitializeAsync()
    {
        if (PostgresTestEnvironment.ShouldSkip)
            return;

        ConnectionString = new NpgsqlConnectionStringBuilder(PostgresTestEnvironment.RequireBaseConnectionString())
        {
            Database = DatabaseName
        }.ConnectionString;

        await ExecuteOnMaintenanceAsync($"CREATE DATABASE \"{DatabaseName}\"");

        if (Migrated)
        {
            await using var db = CreateContext();
            await db.Database.MigrateAsync();
        }
    }

    public async Task DisposeAsync()
    {
        if (ConnectionString.Length == 0)
            return;

        await using (var connection = new NpgsqlConnection(ConnectionString))
            NpgsqlConnection.ClearPool(connection);

        await ExecuteOnMaintenanceAsync($"DROP DATABASE IF EXISTS \"{DatabaseName}\" WITH (FORCE)");
    }

    public AppDbContext CreateContext()
        => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);

    private static async Task ExecuteOnMaintenanceAsync(string sql)
    {
        var maintenance = new NpgsqlConnectionStringBuilder(PostgresTestEnvironment.RequireBaseConnectionString())
        {
            Database = "postgres",
            Pooling = false
        };

        await using var connection = new NpgsqlConnection(maintenance.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>دیتابیس فاز سکو با همهٔ migrationها.</summary>
public sealed class MigratedP1CoreDatabase : P1CoreDatabase
{
    protected override bool Migrated => true;
}

/// <summary>
/// M1 (<c>PlatformPreferences</c>) روی دیتابیسی به شکل پیش از آن: کارگاه‌ها ردیف ترجیحات می‌گیرند، کاربران
/// پیش‌فرض نقش خود را (دقیقاً مثل <see cref="User.ApplyRoleDefaultPreferences"/>)، فیلدهای سفارشی «قابل خروجی»
/// می‌مانند، xmin توکن هم‌زمانی است و Down/Up بی‌خطا رفت‌وبرگشت می‌کند.
/// </summary>
public sealed class PlatformPreferencesMigrationTests
{
    private const string M0 = "20261003195050_CountersAndSecurityStamp";
    private const string M1Suffix = "_PlatformPreferences";

    [PostgresFact]
    public async Task M1_BackfillsPreferencesUsersAndCustomFields_FromAPreM1Database()
    {
        await WithDatabaseAsync(async db =>
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(M0);
            Assert.Contains((await db.Database.GetPendingMigrationsAsync()), id => id.EndsWith(M1Suffix, StringComparison.Ordinal));

            var seed = await SeedLegacyRowsAsync(db);

            await migrator.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges());

            await AssertBackfilledAsync(db, seed);
        });
    }

    [PostgresFact]
    public async Task M1_DownThenUp_RoundTrips_AndBackfillsAgain()
    {
        await WithDatabaseAsync(async db =>
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(M0);
            var seed = await SeedLegacyRowsAsync(db);
            await migrator.MigrateAsync();

            await migrator.MigrateAsync(M0);
            Assert.False(await TableExistsAsync(db, "TenantPreferences"));
            Assert.False(await TableExistsAsync(db, "TenantGridLayouts"));
            Assert.False(await ColumnExistsAsync(db, "Users", "UiMode"));
            Assert.False(await ColumnExistsAsync(db, "AuditEntries", "DetailsJson"));
            Assert.False(await ColumnExistsAsync(db, "CustomFieldDefinitions", "Exportable"));

            await migrator.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            await AssertBackfilledAsync(db, seed);
        });
    }

    [PostgresFact]
    public async Task M1_Tables_HaveNoRealXminColumn_AndXminIsTheConcurrencyToken()
    {
        await WithDatabaseAsync(async db =>
        {
            await db.Database.MigrateAsync();

            // xmin ستون سیستمی است، نه ستونی که migration ساخته باشد (attnum مثبت = ستون کاربر)
            var userColumns = await db.Database.SqlQuery<int>($"""
                SELECT count(*)::int AS "Value" FROM pg_attribute
                WHERE attrelid IN ('"TenantPreferences"'::regclass, '"TenantGridLayouts"'::regclass)
                  AND attname = 'xmin' AND attnum > 0
                """).SingleAsync();
            Assert.Equal(0, userColumns);

            var tenant = new Tenant { Name = "IT xmin", Slug = "it-xmin-" + Guid.NewGuid().ToString("N")[..8] };
            db.Tenants.Add(tenant);
            db.Set<TenantPreferences>().Add(TenantPreferences.CreateDefault(tenant.Id));
            db.Set<TenantGridLayout>().Add(new TenantGridLayout { TenantId = tenant.Id, GridId = TenantGridLayout.IntakeGridId });
            await db.SaveChangesAsync();

            // دو نسخهٔ هم‌زمان از یک ردیف: دومی نباید اولی را بی‌صدا بازنویسی کند
            await using var first = CreateContext(db);
            await using var second = CreateContext(db);
            var a = await first.Set<TenantPreferences>().SingleAsync(p => p.TenantId == tenant.Id);
            var b = await second.Set<TenantPreferences>().SingleAsync(p => p.TenantId == tenant.Id);
            a.SkuPrefix = "AAA";
            await first.SaveChangesAsync();
            b.SkuPrefix = "BBB";
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

            var la = await first.Set<TenantGridLayout>().SingleAsync(l => l.TenantId == tenant.Id);
            var lb = await second.Set<TenantGridLayout>().SingleAsync(l => l.TenantId == tenant.Id);
            la.ColumnsJson = """[{"key":"serial"}]""";
            await first.SaveChangesAsync();
            lb.ColumnsJson = "[]";
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        });
    }

    [PostgresFact]
    public async Task DeletingATenant_CascadesToItsPreferencesAndLayouts()
    {
        await WithDatabaseAsync(async db =>
        {
            await db.Database.MigrateAsync();
            var tenant = new Tenant { Name = "IT cascade", Slug = "it-cascade-" + Guid.NewGuid().ToString("N")[..8] };
            db.Tenants.Add(tenant);
            db.Set<TenantPreferences>().Add(TenantPreferences.CreateDefault(tenant.Id));
            db.Set<TenantGridLayout>().Add(new TenantGridLayout { TenantId = tenant.Id, GridId = TenantGridLayout.BalesGridId });
            await db.SaveChangesAsync();

            await db.Database.ExecuteSqlAsync($"""DELETE FROM "Tenants" WHERE "Id" = {tenant.Id}""");

            Assert.False(await db.Set<TenantPreferences>().AnyAsync(p => p.TenantId == tenant.Id));
            Assert.False(await db.Set<TenantGridLayout>().AnyAsync(l => l.TenantId == tenant.Id));
        });
    }

    // ── کمکی‌ها ──────────────────────────────────────────────────

    private sealed record LegacySeed(Guid TenantA, Guid TenantB, Guid Admin, Guid Operator, Guid Merchant, Guid SystemAdmin, Guid Field);

    /// <summary>ردیف‌های قدیمی با SQL خام، چون مدل فعلی EF ستون‌هایی دارد که هنوز وجود ندارند.</summary>
    private static async Task<LegacySeed> SeedLegacyRowsAsync(AppDbContext db)
    {
        var seed = new LegacySeed(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "Tenants" ("Id", "Name", "Slug", "IsActive", "Currency", "CreatedAt")
            VALUES ({seed.TenantA}, 'Legacy A', {"legacy-a-" + seed.TenantA.ToString("N")[..8]}, true, 0, now()),
                   ({seed.TenantB}, 'Legacy B', {"legacy-b-" + seed.TenantB.ToString("N")[..8]}, true, 1, now())
            """);

        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "Users" ("Id", "TenantId", "Email", "PasswordHash", "FullName", "Role", "IsActive", "CreatedAt", "SecurityStamp")
            VALUES ({seed.Admin}, {seed.TenantA}, {seed.Admin + "@it.local"}, 'x', 'Admin', 1, true, now(), md5(random()::text)),
                   ({seed.Operator}, {seed.TenantA}, {seed.Operator + "@it.local"}, 'x', 'Operator', 2, true, now(), md5(random()::text)),
                   ({seed.Merchant}, {seed.TenantB}, {seed.Merchant + "@it.local"}, 'x', 'Merchant', 3, true, now(), md5(random()::text)),
                   ({seed.SystemAdmin}, NULL, {seed.SystemAdmin + "@it.local"}, 'x', 'System', 0, true, now(), md5(random()::text))
            """);

        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "CustomFieldDefinitions" ("Id", "TenantId", "Key", "Label", "FieldType", "IsRequired", "SortOrder", "IsActive", "CreatedAt")
            VALUES ({seed.Field}, {seed.TenantA}, 'border_color', 'Border', 0, false, 0, true, now())
            """);

        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "AuditEntries" ("Id", "TenantId", "UserName", "Action", "EntityType", "Summary", "CreatedAt")
            VALUES ({Guid.NewGuid()}, {seed.TenantA}, 'x', 0, 'Rug', 'legacy', now())
            """);

        return seed;
    }

    private static async Task AssertBackfilledAsync(AppDbContext db, LegacySeed seed)
    {
        db.ChangeTracker.Clear();

        // هر کارگاه یک ردیف، دقیقاً برابر پیش‌فرض‌های کد
        var preferences = await db.Set<TenantPreferences>().AsNoTracking().ToListAsync();
        Assert.Equal(2, preferences.Count);
        foreach (var tenantId in new[] { seed.TenantA, seed.TenantB })
            AssertEqualToDefaults(preferences.Single(p => p.TenantId == tenantId));

        // کاربران: همان ApplyRoleDefaultPreferences
        var users = await db.Users.AsNoTracking().ToListAsync();
        foreach (var user in users)
        {
            var expected = new User { Role = user.Role };
            expected.ApplyRoleDefaultPreferences();
            Assert.Equal((expected.UiMode, expected.TextScale), (user.UiMode, user.TextScale));
            Assert.Equal(GridEnterDirection.Down, user.GridEnter);
            Assert.Null(user.CoachDismissedJson);
        }

        Assert.Equal((UiMode.Full, TextScale.Normal), Prefs(users, seed.Admin));
        Assert.Equal((UiMode.Simple, TextScale.Normal), Prefs(users, seed.Operator));
        Assert.Equal((UiMode.Simple, TextScale.Large), Prefs(users, seed.Merchant));
        Assert.Equal((UiMode.Full, TextScale.Normal), Prefs(users, seed.SystemAdmin));

        var field = await db.CustomFieldDefinitions.AsNoTracking().SingleAsync(f => f.Id == seed.Field);
        Assert.True(field.Exportable);
        Assert.False(field.ShowInGrid);
        Assert.Null(field.GridOrder);
        Assert.Equal(CustomFieldScope.Rug, field.Scope);

        Assert.All(await db.AuditEntries.AsNoTracking().ToListAsync(), e => Assert.Null(e.DetailsJson));
    }

    private static (UiMode, TextScale) Prefs(IEnumerable<User> users, Guid id)
    {
        var user = users.Single(u => u.Id == id);
        return (user.UiMode, user.TextScale);
    }

    private static void AssertEqualToDefaults(TenantPreferences actual)
    {
        var expected = TenantPreferences.CreateDefault(actual.TenantId);
        foreach (var property in typeof(TenantPreferences).GetProperties())
            Assert.True(Equals(property.GetValue(expected), property.GetValue(actual)),
                $"{property.Name}: «{property.GetValue(actual)}» به‌جای «{property.GetValue(expected)}»");
    }

    private static AppDbContext CreateContext(AppDbContext like)
        => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(like.Database.GetConnectionString()).Options);

    private static async Task WithDatabaseAsync(Func<AppDbContext, Task> body)
    {
        var database = new P1CoreDatabase();
        await database.InitializeAsync();
        try
        {
            await using var db = database.CreateContext();
            await body(db);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private static async Task<bool> TableExistsAsync(AppDbContext db, string table)
        => await db.Database.SqlQuery<bool>($"""
            SELECT EXISTS (SELECT 1 FROM pg_catalog.pg_tables
                           WHERE schemaname = current_schema() AND tablename = {table}) AS "Value"
            """).SingleAsync();

    private static async Task<bool> ColumnExistsAsync(AppDbContext db, string table, string column)
        => await db.Database.SqlQuery<bool>($"""
            SELECT EXISTS (SELECT 1 FROM information_schema.columns
                           WHERE table_schema = current_schema() AND table_name = {table} AND column_name = {column}) AS "Value"
            """).SingleAsync();
}
