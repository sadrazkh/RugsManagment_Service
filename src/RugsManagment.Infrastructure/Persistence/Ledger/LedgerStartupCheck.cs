using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Ledger;

namespace RugsManagment.Infrastructure.Persistence.Ledger;

/// <summary>
/// بررسی حساب‌ها هنگام استارت (spec D.4 «Recovery»؛ کار استارت با ترتیب ۲۰۰، بعد از seed کارگاه‌ها): برای هر کارگاه
/// <list type="bullet">
/// <item>اگر فرشی ردیف <c>RugCostTotal</c> ندارد (مثلاً درست بعد از M2، که عمداً ردیف نمی‌سازد) یا ردیفی با نسخهٔ قاعدهٔ
/// قدیمی ساخته شده (<see cref="LedgerVersion"/> بالا رفته)، همهٔ حساب‌های کارگاه را از نو می‌سازد؛</item>
/// <item>وگرنه ۵۰ فرش تصادفی را می‌سنجد و نتیجه را لاگ می‌کند (ناهمخوانی = هشدار، تا «سلامت حساب‌ها» دیده شود).</item>
/// </list>
/// خطای یک کارگاه استارت را نمی‌خواباند: ثبت می‌شود و کارگاه بعدی ادامه می‌دهد (استارت بعدی دوباره امتحان می‌کند). هر
/// کارگاه در scope تازهٔ خودش اجرا می‌شود تا DbContextِ یک خطا به بعدی نرسد.
/// </summary>
public sealed class LedgerStartupCheck(ILogger<LedgerStartupCheck> logger) : IStartupTask
{
    /// <summary>تعداد فرش‌هایی که در استارتِ سالم سنجیده می‌شوند.</summary>
    public const int SampleSize = 50;

    public int Order => 200;

    public async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> tenantIds;
        using (var listScope = services.CreateScope())
            tenantIds = await listScope.ServiceProvider.GetRequiredService<ILedgerRepository>().ListTenantIdsAsync(cancellationToken);

        foreach (var tenantId in tenantIds)
        {
            try
            {
                using var scope = services.CreateScope();
                await CheckTenantAsync(scope.ServiceProvider, tenantId, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "بررسی حساب‌های کارگاه {TenantId} ناموفق بود؛ استارت بعدی دوباره امتحان می‌کند.", tenantId);
            }
        }
    }

    private async Task CheckTenantAsync(IServiceProvider services, Guid tenantId, CancellationToken cancellationToken)
    {
        var repository = services.GetRequiredService<ILedgerRepository>();
        var counts = await repository.GetHealthCountsAsync(tenantId, LedgerVersion.Current, cancellationToken);
        if (counts.Rugs == 0)
            return;

        if (counts.Totals < counts.Rugs || counts.Stale > 0)
        {
            var result = await services.GetRequiredService<ILedgerMaintenance>().RebuildAsync(tenantId, cancellationToken);
            logger.LogInformation(
                "حساب‌های کارگاه {TenantId} از نو ساخته شد: {Rugs} فرش ({Missing} بی‌ردیف، {Stale} با نسخهٔ قدیمی).",
                tenantId, result.Rugs, counts.Rugs - counts.Totals, counts.Stale);
            return;
        }

        var report = await services.GetRequiredService<ILedgerVerifier>().VerifyAsync(tenantId, SampleSize, cancellationToken);
        if (report.Mismatches.Count == 0)
        {
            logger.LogInformation("حساب‌های کارگاه {TenantId} سالم است ({Checked} فرش سنجیده شد).", tenantId, report.Checked);
            return;
        }

        logger.LogWarning(
            "حساب‌های کارگاه {TenantId}: {Count} ناهمخوانی در {Checked} فرش؛ از «سلامت حساب‌ها» بازسازی کنید. نمونه: {Sample}",
            tenantId, report.Mismatches.Count, report.Checked,
            string.Join("; ", report.Mismatches.Take(5).Select(m => $"{m.Sku} {m.Check} {Invariant(m.Stored)}≠{Invariant(m.Expected)}")));
    }

    /// <summary>عدد با فرهنگ ثابت (فرهنگ fa علامت منفیِ غیر ASCII دارد).</summary>
    private static string Invariant(decimal? value)
        => value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-";
}
