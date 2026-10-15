using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Xunit.Abstractions;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.IntegrationTests.P1;
using RugsManagment.IntegrationTests.P2;
using RugsManagment.IntegrationTests.Support;
using static RugsManagment.IntegrationTests.P2.P2Http;
using static RugsManagment.IntegrationTests.P4a.P4aApi;

namespace RugsManagment.IntegrationTests.P4a;

/// <summary>
/// کارایی «قطعی» و «باز کردن دوباره» (spec D.4 «Performance»، L.2): محمولهٔ ۳۰۰ خطی زیر قفل حساب‌های کارگاه با کمتر از
/// ۲۰ دستور SQL — همهٔ فرش‌ها با مراحلشان در یک پرس‌وجوی تقسیم‌شده بار می‌شوند و وضعیتشان در حافظه حل می‌شود، و قفل‌ها
/// خط‌های فرش‌ها را با یک پرس‌وجو می‌یابند، نه یکی برای هر فرش.
///
/// شمارش از راه ActivitySource «Npgsql» است (هر دستور یک activity)، فقط برای دیتابیس دورریختنیِ همین کلاس، پس
/// آزمون‌های موازی کلاس‌های دیگر در شمارش نمی‌آیند.
/// </summary>
public sealed class P4aPerformanceTests(RugsWebFactory factory, ITestOutputHelper output) : IClassFixture<RugsWebFactory>
{
    private const int Lines = 300;
    private const int MaxCommands = 20;

    [PostgresFact]
    public async Task FinalizeAndReopen_OfA300LineShipment_IssueFewerThan20SqlCommands()
    {
        var t = await P2Tenant.CreateAsync(factory);
        using var merchant = P1Client.As(factory, t.Merchant);
        using var op = P1Client.As(factory, t.Operator);
        await AddRateAsync(merchant, "گبه", 45m);

        // ۳۰۰ فرش اندازه‌گرفته مستقیم در دیتابیس (ساختن فرش موضوع این آزمون نیست)
        var rugIds = new List<Guid>(Lines);
        await using (var db = factory.CreateDbContext())
        {
            var now = DateTimeOffset.UtcNow;
            for (var i = 0; i < Lines; i++)
            {
                var rug = new Rug { TenantId = t.TenantId, Sku = $"P4A-PERF-{i:D4}", Origin = "گبه" };
                rug.SetSerial(SerialNumber.Parse((7001 + i).ToString(CultureInfo.InvariantCulture)));
                rug.SetIntakeSize(2m, 3m);
                rug.SetFinalSizeSameAsIntake(now, null);
                db.Rugs.Add(rug);
                rugIds.Add(rug.Id);
            }

            await db.SaveChangesAsync();
        }

        var shipmentId = await NewShipmentAsync(merchant);
        var added = await OpsAsync(op, shipmentId, [.. rugIds.Select((id, i) => Add(id, 1 + i / 30))]);
        Assert.All(added.GetProperty("results").EnumerateArray(), r => Assert.True(r.GetProperty("ok").GetBoolean(), r.ToString()));
        Assert.Equal(10, added.GetProperty("bales").GetArrayLength());
        Assert.Equal(Lines * 270m, Dec(added.GetProperty("totals"), "usdTotal"));

        // «قطعی»
        var version = await ShipmentVersionAsync(merchant, shipmentId);
        var (finalized, finalizeCommands) = await CountAsync(() =>
            SendAsync(merchant, HttpMethod.Post, $"/api/shipments/{shipmentId}/finalize", new { version }));
        Assert.True(finalized.GetProperty("ok").GetBoolean(), finalized.ToString());
        Assert.NotEmpty(finalizeCommands); // شمارش واقعاً دستورها را می‌بیند
        Assert.True(finalizeCommands.Count < MaxCommands,
            $"قطعی {finalizeCommands.Count} دستور SQL زد:\n{string.Join("\n", finalizeCommands)}");

        // «باز کردن دوباره» (ارزش هر ۳۰۰ خط از جدول دوباره حساب می‌شود)
        version = finalized.GetProperty("shipment").GetProperty("version").GetUInt32();
        var (reopened, reopenCommands) = await CountAsync(() =>
            SendAsync(merchant, HttpMethod.Post, $"/api/shipments/{shipmentId}/reopen", new { version, reason = (string?)null }));
        output.WriteLine($"finalize: {finalizeCommands.Count} SQL commands; reopen: {reopenCommands.Count} SQL commands");
        Assert.Equal(0, reopened.GetProperty("status").GetInt32());
        Assert.True(reopenCommands.Count < MaxCommands,
            $"باز کردن دوباره {reopenCommands.Count} دستور SQL زد:\n{string.Join("\n", reopenCommands)}");
        Assert.Equal(Lines * 270m, Dec(reopened.GetProperty("totals"), "usdTotal"));

        // یک «اندازه» روی محمولهٔ ۳۰۰ خطی زیر ۲۰۰ میلی‌ثانیه (D.4). بار اول گرم کردن است؛ از پنج بار بعدی سریع‌ترین سنجیده
        // می‌شود، چون کلاس‌های دیگرِ آزمون هم‌زمان روی همان پستگرس کار می‌کنند و یک بارِ تنها اندازهٔ صف را می‌گیرد، نه کارِ خودش
        var rows = (await RowsAsync(op, shipmentId)).Take(6).ToList();
        Result(await OpsAsync(op, shipmentId, Measure(rows[0], 2.10m)));
        var timings = new List<long>();
        foreach (var row in rows.Skip(1))
        {
            var watch = Stopwatch.StartNew();
            var measured = Result(await OpsAsync(op, shipmentId, Measure(row, 2.10m))).GetProperty("row");
            watch.Stop();
            timings.Add(watch.ElapsedMilliseconds);
            Assert.Equal(6.30m, Dec(measured, "areaM2"));
            Assert.Equal(283.50m, Dec(measured, "lineUsd")); // 6.30 × 45
        }

        output.WriteLine($"measure: {string.Join(", ", timings)} ms");
        Assert.True(timings.Min() < 200, $"اندازه دست‌کم {timings.Min()} میلی‌ثانیه طول کشید ({string.Join("، ", timings)}).");
        Assert.Equal(Lines * 270m + rows.Count * 13.50m, Dec((await ShipmentAsync(op, shipmentId)).GetProperty("totals"), "usdTotal"));
    }

    private static object Measure(JsonElement row, decimal length) => new
    {
        op = "measure",
        clientOpId = "m",
        rugId = row.GetProperty("rugId").GetGuid(),
        rugVersion = row.GetProperty("rugVersion").GetUInt32(),
        finalLength = length,
        finalWidth = 3m
    };

    /// <summary>دستورهای SQL (activityهای «Npgsql») این دیتابیس در طول کار.</summary>
    private async Task<(JsonElement Result, IReadOnlyList<string> Commands)> CountAsync(Func<Task<JsonElement>> work)
    {
        var database = factory.DatabaseName;
        var commands = new ConcurrentQueue<string>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Npgsql",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (!activity.TagObjects.Any(tag => tag.Value is string value && value.Contains(database, StringComparison.Ordinal)))
                    return;
                var statement = activity.TagObjects
                    .FirstOrDefault(tag => tag.Key is "db.statement" or "db.query.text").Value as string ?? activity.DisplayName;
                commands.Enqueue(statement.Length > 160 ? statement[..160] : statement);
            }
        };
        ActivitySource.AddActivityListener(listener);

        var result = await work();
        return (result, commands.ToList());
    }
}
