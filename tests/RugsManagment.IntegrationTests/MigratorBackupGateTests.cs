using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RugsManagment.Application.Abstractions;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.Infrastructure.Persistence;
using RugsManagment.Infrastructure.Persistence.Seed;
using RugsManagment.IntegrationTests.Support;

namespace RugsManagment.IntegrationTests;

/// <summary>
/// تصمیم خالصِ دروازهٔ پشتیبان و کلید MigrateOnStartup — بدون دیتابیس، پس همیشه اجرا می‌شوند.
/// </summary>
public sealed class MigratorBackupDecisionTests
{
    [Fact]
    public void FindBackupGates_OnlyPendingMigrationsWithTheAttribute()
    {
        var gates = Migrator.FindBackupGates(FakeMigrations.All, [FakeMigrations.UsersRewrite, FakeMigrations.Plain]);

        var gate = Assert.Single(gates);
        Assert.Equal(new BackupGate(FakeMigrations.UsersRewrite, "Users"), gate);
        Assert.Empty(Migrator.FindBackupGates(FakeMigrations.All, [FakeMigrations.Plain, "unknown_id"]));
    }

    [Fact]
    public void NonEmptyTable_WithoutConfirmation_Throws()
    {
        var ex = Assert.Throws<MigrationBackupRequiredException>(() => Migrator.EnsureBackupConfirmed(
            [new BackupGate(FakeMigrations.UsersRewrite, "Users")], new HashSet<string> { "Users" }, confirmedBackupFor: null));

        Assert.Equal(FakeMigrations.UsersRewrite, ex.MigrationId);
        Assert.Equal("Users", ex.Table);
        Assert.IsAssignableFrom<StartupAbortException>(ex); // Program.cs نباید دوباره تلاش کند
    }

    [Theory]
    [InlineData(FakeMigrations.UsersRewrite)]
    [InlineData(" " + FakeMigrations.UsersRewrite + " ")]
    [InlineData("20990109000000_Other, " + FakeMigrations.UsersRewrite)]
    public void NonEmptyTable_WithConfirmation_Passes(string confirmed)
        => Migrator.EnsureBackupConfirmed(
            [new BackupGate(FakeMigrations.UsersRewrite, "Users")], new HashSet<string> { "Users" }, confirmed);

    [Fact]
    public void ConfirmationForAnotherMigration_DoesNotCount()
        => Assert.Throws<MigrationBackupRequiredException>(() => Migrator.EnsureBackupConfirmed(
            [new BackupGate(FakeMigrations.UsersRewrite, "Users")], new HashSet<string> { "Users" },
            "20990101000000_FakeUsers"));

    [Fact]
    public void EmptyTable_NeedsNoConfirmation()
        => Migrator.EnsureBackupConfirmed(
            [new BackupGate(FakeMigrations.UsersRewrite, "Users")], new HashSet<string>(), null);

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("true", true)]
    [InlineData("False", false)]
    public void MigrateOnStartup_DefaultsToTrue(string? value, bool expected)
        => Assert.Equal(expected, Migrator.IsEnabled(Config(value)));

    [Fact]
    public void MigrateOnStartup_InvalidValue_AbortsStartup()
        => Assert.Throws<StartupAbortException>(() => Migrator.IsEnabled(Config("nope")));

    [Fact]
    public async Task MigrateOnStartupFalse_NeverTouchesTheDatabase()
    {
        // رشتهٔ اتصال عمداً نامعتبر است؛ اگر Migrator وصل شود تست با خطای شبکه شکست می‌خورد
        var services = new ServiceCollection()
            .AddSingleton(Config("false"))
            .AddDbContext<AppDbContext>(o => o.UseNpgsql("Host=invalid.invalid;Database=nothing"))
            .BuildServiceProvider();
        await using (services)
        {
            await Migrator.EnsureDatabaseExistsAsync(services, NullLogger.Instance);
            await Migrator.RunAsync(services, NullLogger.Instance);
        }
    }

    private static IConfiguration Config(string? migrateOnStartup)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [Migrator.MigrateOnStartupKey] = migrateOnStartup })
            .Build();
}

/// <summary>
/// همان دروازه روی دیتابیس واقعی، با فهرست migrationِ ساختگی: migration‌ای که
/// <see cref="RequiresBackupAttribute"/> دارد روی جدول پُر، بدون تأیید پشتیبان، استارت را متوقف می‌کند.
/// </summary>
public sealed class MigratorBackupGateTests(MigratedTestDatabase database) : IClassFixture<MigratedTestDatabase>
{
    [PostgresFact]
    public async Task PendingRequiresBackupMigration_OnNonEmptyTable_Throws_UntilConfirmed()
    {
        await using var db = database.CreateContext();
        db.Users.Add(new User
        {
            Email = $"gate-{Guid.NewGuid():N}@it.local",
            FullName = "Gate",
            PasswordHash = "x",
            Role = UserRole.SystemAdmin
        });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<MigrationBackupRequiredException>(() => Migrator.CheckBackupGatesAsync(
            db, FakeMigrations.All, [FakeMigrations.UsersRewrite], confirmedBackupFor: null));
        Assert.Equal(FakeMigrations.UsersRewrite, ex.MigrationId);
        Assert.Equal("Users", ex.Table);

        await Migrator.CheckBackupGatesAsync(
            db, FakeMigrations.All, [FakeMigrations.UsersRewrite], confirmedBackupFor: FakeMigrations.UsersRewrite);
    }

    [PostgresFact]
    public async Task PendingRequiresBackupMigration_OnEmptyOrMissingTable_Passes()
    {
        await using var db = database.CreateContext();

        Assert.False(await Migrator.TableHasRowsAsync(db, "TenantCounters"));
        Assert.False(await Migrator.TableHasRowsAsync(db, "NoSuchTable"));
        await Migrator.CheckBackupGatesAsync(
            db, FakeMigrations.All, [FakeMigrations.CountersRewrite, FakeMigrations.MissingTable], null);
    }
}

/// <summary>
/// migrationهای ساختگی؛ چون [DbContext]/[Migration] ندارند و در اسمبلی Infrastructure نیستند،
/// EF هرگز آن‌ها را اجرا نمی‌کند — فقط فهرستی برای دروازهٔ پشتیبان‌اند.
/// </summary>
internal static class FakeMigrations
{
    public const string UsersRewrite = "20990101000000_FakeUsersRewrite";
    public const string CountersRewrite = "20990102000000_FakeCountersRewrite";
    public const string MissingTable = "20990103000000_FakeMissingTable";
    public const string Plain = "20990104000000_FakePlain";

    public static readonly IReadOnlyDictionary<string, TypeInfo> All = new Dictionary<string, TypeInfo>
    {
        [UsersRewrite] = typeof(FakeUsersRewrite).GetTypeInfo(),
        [CountersRewrite] = typeof(FakeCountersRewrite).GetTypeInfo(),
        [MissingTable] = typeof(FakeMissingTable).GetTypeInfo(),
        [Plain] = typeof(FakePlain).GetTypeInfo()
    };

    [RequiresBackup("Users")]
    private sealed class FakeUsersRewrite : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) { }
    }

    [RequiresBackup("TenantCounters")]
    private sealed class FakeCountersRewrite : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) { }
    }

    [RequiresBackup("NoSuchTable")]
    private sealed class FakeMissingTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) { }
    }

    private sealed class FakePlain : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) { }
    }
}
