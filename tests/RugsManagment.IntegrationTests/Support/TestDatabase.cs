using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RugsManagment.Infrastructure.Persistence;

namespace RugsManagment.IntegrationTests.Support;

/// <summary>
/// دیتابیس دورریختنیِ هر کلاس تست: <c>rugs_it_xxxxxxxx</c>. در <see cref="DisposeAsync"/> با FORCE پاک می‌شود
/// تا اتصال باز ماندهٔ یک تستِ شکست‌خورده هم مانع حذف نشود.
///
/// به‌صورت پیش‌فرض دیتابیس خالی (بدون جدول) ساخته می‌شود؛ زیرکلاس‌ها می‌توانند ساختش را به خود
/// برنامه بسپارند (<see cref="CreateDatabaseUpFront"/>) یا بعد از ساخت migration بزنند.
/// </summary>
public class TestDatabase : IAsyncLifetime
{
    public string DatabaseName { get; } = "rugs_it_" + Guid.NewGuid().ToString("N")[..8];

    /// <summary>رشتهٔ اتصال به همین دیتابیس؛ تا پیش از <see cref="InitializeAsync"/> خالی است.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>false یعنی دیتابیس ساخته نمی‌شود و خود برنامه (StartupPipeline) باید بسازدش.</summary>
    protected virtual bool CreateDatabaseUpFront => true;

    public async Task InitializeAsync()
    {
        if (PostgresTestEnvironment.ShouldSkip)
            return; // تست‌ها با PostgresFact رد می‌شوند

        var baseConnectionString = PostgresTestEnvironment.RequireBaseConnectionString();
        ConnectionString = new NpgsqlConnectionStringBuilder(baseConnectionString) { Database = DatabaseName }.ConnectionString;

        if (CreateDatabaseUpFront)
            await ExecuteOnMaintenanceDatabaseAsync($"CREATE DATABASE \"{DatabaseName}\"");

        await OnInitializedAsync();
    }

    protected virtual Task OnInitializedAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (ConnectionString.Length == 0)
            return;

        await using (var connection = new NpgsqlConnection(ConnectionString))
            NpgsqlConnection.ClearPool(connection);

        await ExecuteOnMaintenanceDatabaseAsync($"DROP DATABASE IF EXISTS \"{DatabaseName}\" WITH (FORCE)");
    }

    public AppDbContext CreateContext()
        => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);

    /// <summary>
    /// همان سرویس‌هایی که StartupPipeline از میزبان واقعی می‌گیرد: DbContext، پیکربندی و
    /// (اختیاری) کارهای استارت.
    /// </summary>
    public ServiceProvider BuildServices(
        IReadOnlyDictionary<string, string?>? settings = null,
        Action<IServiceCollection>? configure = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings ?? new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(ConnectionString));
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private async Task ExecuteOnMaintenanceDatabaseAsync(string sql)
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

/// <summary>دیتابیسی که خود StartupPipeline باید بسازد (سرور «خالی»).</summary>
public sealed class NonExistentTestDatabase : TestDatabase
{
    protected override bool CreateDatabaseUpFront => false;
}

/// <summary>دیتابیس با همهٔ migrationها، بدون دادهٔ اولیه.</summary>
public sealed class MigratedTestDatabase : TestDatabase
{
    protected override async Task OnInitializedAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }
}
