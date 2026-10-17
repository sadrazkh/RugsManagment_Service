using RugsManagment.Application.DTOs.Rugs;

namespace RugsManagment.Application.Ledger;

/// <summary>
/// بازسازی کامل حساب‌های یک کارگاه (spec D.4 «Recovery»): همهٔ ردیف‌های مشتق‌شده (مبلغ سطرهای خرید، جمع بهای هر فرش و در
/// فازهای بعد تقسیم ثبت‌ها و ارزش خط‌ها) از نو نوشته می‌شوند. کاربردها: <c>POST /api/diagnostics/ledger-rebuild</c>
/// (تنظیم‌کننده)، بررسی استارت وقتی ردیفی نیست یا نسخهٔ قاعده‌ها عوض شده، و seed.
/// </summary>
public interface ILedgerMaintenance
{
    /// <summary>
    /// در یک تراکنش و زیر قفل کارگاه همهٔ مرحله‌ها را روی همهٔ فرش‌ها (سطل زباله هم) اجرا می‌کند. شماره‌ها سنجیده
    /// نمی‌شوند و عکس بهای فروش‌ها دست نمی‌خورد.
    /// </summary>
    Task<LedgerRebuildResultDto> RebuildAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// سنجش حساب‌ها (spec D.4 «Recovery»): همه‌چیز را با همان قاعده‌ها از نو حساب می‌کند و با ذخیره‌شده می‌سنجد. فهرست خالی
/// یعنی سالم. <c>GET /api/diagnostics/cost-consistency</c> و صفحهٔ «سلامت حساب‌ها» همین را نشان می‌دهند.
/// </summary>
public interface ILedgerVerifier
{
    /// <summary>
    /// همهٔ فرش‌های کارگاه (سطل زباله هم)، یا با <paramref name="sample"/> فقط آن تعداد به‌تصادف (بررسی استارت). زیر قفل
    /// کارگاه اجرا می‌شود تا ذخیره‌ای وسط سنجش ناهمخوانیِ گذرا نسازد.
    /// </summary>
    Task<CostConsistencyReportDto> VerifyAsync(Guid tenantId, int? sample = null, CancellationToken cancellationToken = default);
}
