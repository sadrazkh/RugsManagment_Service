using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RugsManagment.Application.Abstractions;

namespace RugsManagment.Infrastructure.Persistence.Seed;

/// <summary>
/// اجرای خودکار migrationها هنگام استارت — همیشه زیر قفل advisory که <see cref="StartupPipeline"/> گرفته.
///
/// دو ترمز دارد که با تلاش دوباره پوشانده نمی‌شوند (<see cref="StartupAbortException"/>):
/// <list type="bullet">
/// <item>migration در انتظاری که <see cref="RequiresBackupAttribute"/> دارد و جدولش خالی نیست، تا
/// وقتی پشتیبانش در <c>Database:ConfirmedBackupFor</c> تأیید نشده باشد؛</item>
/// <item>مدلی که تغییر کرده ولی migration‌اش ساخته نشده (PendingModelChangesWarning در EF)؛
/// ادامه دادن یعنی کوئری روی ستونی که وجود ندارد.</item>
/// </list>
/// با <c>Database:MigrateOnStartup = false</c> کلاً کنار می‌رود (مثلاً وقتی DBA اسکریپت را دستی اجرا می‌کند).
/// </summary>
public static class Migrator
{
    public const string MigrateOnStartupKey = "Database:MigrateOnStartup";
    public const string ConfirmedBackupForKey = "Database:ConfirmedBackupFor";

    /// <summary>migration روی جدول بزرگ ممکن است از ۳۰ ثانیهٔ پیش‌فرض EF بیشتر طول بکشد.</summary>
    private static readonly TimeSpan MigrationCommandTimeout = TimeSpan.FromMinutes(10);

    /// <summary>پیش‌فرض روشن است؛ فقط مقدار صریح <c>false</c> خاموشش می‌کند.</summary>
    public static bool IsEnabled(IConfiguration? configuration)
    {
        var raw = configuration?[MigrateOnStartupKey];
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        return bool.TryParse(raw, out var enabled)
            ? enabled
            : throw new StartupAbortException(
                $"مقدار «{MigrateOnStartupKey}» باید true یا false باشد؛ «{raw}» معتبر نیست.");
    }

    /// <summary>
    /// اگر خود دیتابیس هنوز ساخته نشده، یک دیتابیس خالی می‌سازد (بدون جدول؛ جدول‌ها کار migration است).
    /// قبل از قفل صدا زده می‌شود چون قفل advisory روی همین دیتابیس گرفته می‌شود و بدون آن اتصال باز نمی‌شود.
    /// </summary>
    public static async Task EnsureDatabaseExistsAsync(
        IServiceProvider services, ILogger logger, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled(services.GetService<IConfiguration>()))
            return;

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var creator = db.GetService<IRelationalDatabaseCreator>();
        if (await creator.ExistsAsync(cancellationToken))
            return;

        logger.LogWarning("دیتابیس وجود ندارد؛ یک دیتابیس خالی ساخته می‌شود.");
        await creator.CreateAsync(cancellationToken);
    }

    public static async Task RunAsync(
        IServiceProvider services, ILogger logger, CancellationToken cancellationToken = default)
    {
        var configuration = services.GetService<IConfiguration>();
        if (!IsEnabled(configuration))
        {
            logger.LogWarning("{Key} خاموش است؛ migrationها اجرا نشدند و schema باید دستی به‌روز شود.",
                MigrateOnStartupKey);
            return;
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.SetCommandTimeout(MigrationCommandTimeout);

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0)
            logger.LogInformation("schema به‌روز است؛ migration در انتظاری نیست.");
        else
            logger.LogInformation("{Count} migration در انتظار: {Migrations}", pending.Count, string.Join(", ", pending));

        await CheckBackupGatesAsync(
            db, db.GetService<IMigrationsAssembly>().Migrations, pending,
            configuration?[ConfirmedBackupForKey], cancellationToken);

        try
        {
            // حتی بدون migration در انتظار صدا زده می‌شود: EF همین‌جا بررسی می‌کند که مدل
            // migrationِ ساخته‌نشده نداشته باشد، و این بررسی نباید فقط هنگام دیپلوی بعدی رخ دهد.
            await db.Database.MigrateAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
            when (ex.Message.Contains("PendingModelChangesWarning", StringComparison.Ordinal))
        {
            throw new StartupAbortException(
                "مدل EF تغییری دارد که migration‌اش ساخته نشده است. اول " +
                "«dotnet ef migrations add <نام> --project src/RugsManagment.Infrastructure --startup-project src/RugsManagment.Web» " +
                "را اجرا کنید و بعد دوباره دیپلوی کنید.", ex);
        }

        if (pending.Count > 0)
            logger.LogInformation("{Count} migration با موفقیت اجرا شد.", pending.Count);
    }

    /// <summary>
    /// ترمز پشتیبان: برای هر migration در انتظار که <see cref="RequiresBackupAttribute"/> دارد، اگر
    /// جدولش ردیف داشته باشد و پشتیبانش تأیید نشده باشد، <see cref="MigrationBackupRequiredException"/>.
    /// جدا از <see cref="RunAsync"/> است تا تست بتواند با فهرست migration ساختگی صدایش بزند.
    /// </summary>
    internal static async Task CheckBackupGatesAsync(
        AppDbContext db,
        IReadOnlyDictionary<string, TypeInfo> migrations,
        IEnumerable<string> pendingMigrationIds,
        string? confirmedBackupFor,
        CancellationToken cancellationToken = default)
    {
        var gates = FindBackupGates(migrations, pendingMigrationIds);
        if (gates.Count == 0)
            return;

        var nonEmptyTables = new HashSet<string>(StringComparer.Ordinal);
        foreach (var table in gates.Select(g => g.Table).Distinct(StringComparer.Ordinal))
        {
            if (await TableHasRowsAsync(db, table, cancellationToken))
                nonEmptyTables.Add(table);
        }

        EnsureBackupConfirmed(gates, nonEmptyTables, confirmedBackupFor);
    }

    /// <summary>migrationهای در انتظار که روی کلاسشان <see cref="RequiresBackupAttribute"/> است، به ترتیب اجرا.</summary>
    internal static IReadOnlyList<BackupGate> FindBackupGates(
        IReadOnlyDictionary<string, TypeInfo> migrations, IEnumerable<string> pendingMigrationIds)
        => pendingMigrationIds
            .Where(migrations.ContainsKey)
            .SelectMany(id => migrations[id]
                .GetCustomAttributes<RequiresBackupAttribute>(inherit: false)
                .Select(attribute => new BackupGate(id, attribute.Table)))
            .ToList();

    /// <summary>
    /// تصمیم خالص (بدون دیتابیس): اولین دروازه‌ای که جدولش پُر است و شناسه‌اش در
    /// <paramref name="confirmedBackupFor"/> نیامده، استثنا می‌دهد. چند شناسه را می‌توان با ویرگول جدا کرد،
    /// برای وقتی که یک دیپلوی دو migration پشتیبان‌خواه دارد.
    /// </summary>
    internal static void EnsureBackupConfirmed(
        IEnumerable<BackupGate> gates, IReadOnlySet<string> nonEmptyTables, string? confirmedBackupFor)
    {
        var confirmed = (confirmedBackupFor ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var gate in gates)
        {
            if (nonEmptyTables.Contains(gate.Table) && !confirmed.Contains(gate.MigrationId))
                throw new MigrationBackupRequiredException(gate.MigrationId, gate.Table);
        }
    }

    /// <summary>
    /// جدولی که هنوز ساخته نشده خالی حساب می‌شود (مثلاً روی دیتابیس تازه، پیش از اولین migration).
    /// </summary>
    internal static async Task<bool> TableHasRowsAsync(
        AppDbContext db, string table, CancellationToken cancellationToken = default)
    {
        var exists = await db.Database
            .SqlQuery<bool>($"""
                SELECT EXISTS (
                    SELECT 1 FROM pg_catalog.pg_tables
                    WHERE schemaname = current_schema() AND tablename = {table}) AS "Value"
                """)
            .ToListAsync(cancellationToken);
        if (!exists[0])
            return false;

        // نام جدول پارامترپذیر نیست؛ از attribute می‌آید و اینجا quote می‌شود
        var quoted = "\"" + table.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        var sql = "SELECT EXISTS (SELECT 1 FROM " + quoted + ") AS \"Value\"";
        var hasRows = await db.Database.SqlQueryRaw<bool>(sql).ToListAsync(cancellationToken);
        return hasRows[0];
    }
}

/// <summary>یک migration در انتظار و جدولی که پیش از اجرایش باید پشتیبان داشته باشد.</summary>
internal sealed record BackupGate(string MigrationId, string Table);
