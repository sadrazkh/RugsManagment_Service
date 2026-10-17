using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RugsManagment.Application.Abstractions;
using RugsManagment.Infrastructure.Persistence;
using RugsManagment.Infrastructure.Persistence.Seed;
using RugsManagment.IntegrationTests.Support;

namespace RugsManagment.IntegrationTests;

/// <summary>
/// استارت روی سرور خالی: StartupPipeline باید دیتابیس را بسازد، همهٔ migrationها را اجرا کند، دادهٔ
/// اولیه بکارد و کارهای استارت را به ترتیب اجرا کند؛ استارت دوم نباید هیچ چیزی را تغییر دهد.
/// </summary>
public sealed class StartupPipelineTests(NonExistentTestDatabase database) : IClassFixture<NonExistentTestDatabase>
{
    [PostgresFact]
    public async Task EmptyServer_FirstStartBuildsSchemaAndSeed_SecondStartIsNoOp()
    {
        var log = new List<int>();
        await using var services = database.BuildServices(configure: s =>
        {
            s.AddSingleton(log);
            // عمداً به ترتیب نادرست ثبت می‌شوند؛ اجرا باید بر اساس Order باشد
            s.AddScoped<IStartupTask>(_ => new RecordingTask(20, log));
            s.AddScoped<IStartupTask>(_ => new RecordingTask(10, log));
        });

        await StartupPipeline.RunAsync(services, NullLogger.Instance);

        Snapshot first;
        await using (var db = database.CreateContext())
        {
            var all = db.Database.GetMigrations().ToList();
            var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
            Assert.Equal(all, applied);
            Assert.Contains(applied, id => id.EndsWith("_CountersAndSecurityStamp", StringComparison.Ordinal));
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges());

            first = await TakeSnapshotAsync(db);
            Assert.Equal(8, first.StepTypes);
            Assert.Equal(2, first.Users); // ادمین سیستم + مدیر کارگاه دمو
            Assert.Equal(1, first.Tenants);
            Assert.Equal(5, first.Rugs);
        }
        Assert.Equal(new[] { 10, 20 }, log);

        // استارت دوم (یا نمونهٔ دوم بعد از اولی): بدون migration، بدون دادهٔ تکراری
        await StartupPipeline.RunAsync(services, NullLogger.Instance);

        await using (var db = database.CreateContext())
            Assert.Equal(first, await TakeSnapshotAsync(db));
        Assert.Equal(new[] { 10, 20, 10, 20 }, log);
    }

    private static async Task<Snapshot> TakeSnapshotAsync(AppDbContext db) => new(
        await db.Database.SqlQuery<int>($"""SELECT count(*)::int AS "Value" FROM "__EFMigrationsHistory" """).SingleAsync(),
        await db.ProcessStepTypes.CountAsync(),
        await db.Users.CountAsync(),
        await db.Tenants.CountAsync(),
        await db.Rugs.IgnoreQueryFilters().CountAsync());

    private sealed record Snapshot(int Migrations, int StepTypes, int Users, int Tenants, int Rugs);

    private sealed class RecordingTask(int order, List<int> log) : IStartupTask
    {
        public int Order => order;

        public Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            lock (log) log.Add(order);
            return Task.CompletedTask;
        }
    }
}
