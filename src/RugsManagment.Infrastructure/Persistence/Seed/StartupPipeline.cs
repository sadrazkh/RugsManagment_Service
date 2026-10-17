using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using RugsManagment.Application.Abstractions;

namespace RugsManagment.Infrastructure.Persistence.Seed;

/// <summary>
/// کار دیتابیسیِ استارت به این ترتیب: migration ← دادهٔ اولیه ← <see cref="IStartupTask"/>ها.
///
/// همه زیر یک قفل advisory پستگرس اجرا می‌شوند. چرا: در CapRover (یا هر جا که دو نمونه هم‌زمان
/// بالا بیایند، مثلاً دیپلوی rolling) بدون قفل هر دو نمونه migration را هم‌زمان شروع می‌کردند یا هر دو
/// «جدول خالی است» را می‌دیدند و دادهٔ اولیه را دو بار می‌کاشتند. نمونهٔ دوم پشت قفل می‌ماند و وقتی
/// نوبتش شد همه‌چیز را انجام‌شده می‌بیند.
///
/// قفل روی یک اتصال اختصاصی و بیرون از pool گرفته می‌شود: قفل advisory مال «نشست» است، پس اگر
/// اتصال به pool برمی‌گشت ممکن بود قفل با آن بماند؛ با بستن این اتصال، قفل حتماً آزاد می‌شود
/// (حتی اگر unlock به هر دلیلی اجرا نشود یا برنامه وسط کار بمیرد).
/// </summary>
public static class StartupPipeline
{
    /// <summary>کلید قفل؛ hashtext در خود پستگرس به عدد تبدیلش می‌کند.</summary>
    private const string LockKey = "rugs:startup";

    /// <summary>
    /// نمونهٔ دوم تا این مدت پشت قفل صبر می‌کند (migration طولانیِ نمونهٔ اول). بعدش خطای معمولی
    /// می‌دهد و حلقهٔ تلاش دوبارهٔ Program.cs دوباره امتحان می‌کند.
    /// </summary>
    private static readonly TimeSpan LockWaitTimeout = TimeSpan.FromMinutes(10);

    public static async Task RunAsync(
        IServiceProvider services, ILogger logger, CancellationToken cancellationToken = default)
    {
        // باید پیش از قفل باشد: قفل روی خود این دیتابیس گرفته می‌شود
        await Migrator.EnsureDatabaseExistsAsync(services, logger, cancellationToken);

        await using var lockConnection = new NpgsqlConnection(BuildLockConnectionString(services));
        await lockConnection.OpenAsync(cancellationToken);

        logger.LogInformation("در انتظار قفل استارت دیتابیس…");
        await ExecuteAsync(lockConnection, $"SELECT pg_advisory_lock(hashtext('{LockKey}'))", cancellationToken);
        logger.LogInformation("قفل استارت گرفته شد.");

        try
        {
            await Migrator.RunAsync(services, logger, cancellationToken);
            await DatabaseSeeder.SeedAsync(services);
            await RunStartupTasksAsync(services, logger, cancellationToken);
        }
        finally
        {
            try
            {
                await ExecuteAsync(lockConnection, $"SELECT pg_advisory_unlock(hashtext('{LockKey}'))", CancellationToken.None);
            }
            catch (Exception ex)
            {
                // خطای اصلی (اگر بود) نباید زیر این یکی گم شود؛ بستن اتصال قفل را آزاد می‌کند
                logger.LogWarning(ex, "آزادسازی قفل استارت ناموفق بود؛ با بستن اتصال آزاد می‌شود.");
            }
        }
    }

    /// <summary>
    /// همان رشتهٔ اتصالی که DbContext استفاده می‌کند — تا قفل و migration حتماً روی یک دیتابیس باشند —
    /// ولی بدون pool و با timeout بلند برای صبر پشت قفل.
    /// </summary>
    private static string BuildLockConnectionString(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var configured = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetConnectionString()
            ?? throw new StartupAbortException("رشتهٔ اتصال دیتابیس (ConnectionStrings:DefaultConnection) تنظیم نشده است.");

        return new NpgsqlConnectionStringBuilder(configured)
        {
            Pooling = false,
            CommandTimeout = (int)LockWaitTimeout.TotalSeconds
        }.ConnectionString;
    }

    /// <summary>
    /// هر کار در scope تازهٔ خودش اجرا می‌شود تا DbContextِ یکی (مثلاً بعد از خطا) به بعدی نرسد.
    /// ترتیب: <see cref="IStartupTask.Order"/> و برای ترتیب برابر نام کلاس، تا اجرا قطعی باشد.
    /// </summary>
    private static async Task RunStartupTasksAsync(
        IServiceProvider services, ILogger logger, CancellationToken cancellationToken)
    {
        using var listScope = services.CreateScope();
        var tasks = listScope.ServiceProvider.GetServices<IStartupTask>()
            .OrderBy(t => t.Order)
            .ThenBy(t => t.GetType().FullName, StringComparer.Ordinal)
            .ToList();

        foreach (var task in tasks)
        {
            logger.LogInformation("کار استارت {Task} (ترتیب {Order})…", task.GetType().Name, task.Order);
            using var taskScope = services.CreateScope();
            await task.RunAsync(taskScope.ServiceProvider, cancellationToken);
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        if (connection.State != ConnectionState.Open)
            throw new InvalidOperationException("اتصال قفل استارت بسته شده است.");

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
