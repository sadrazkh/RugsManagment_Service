namespace RugsManagment.Application.Ledger;

/// <summary>
/// «پاس سازگاری» (spec D.4): هر <c>IUnitOfWork.SaveChangesAsync</c> از این‌جا می‌گذرد. پیاده‌سازی: <see cref="LedgerPass"/>.
/// </summary>
public interface ILedgerPass
{
    /// <summary>
    /// ذخیرهٔ تغییرهای ردیابی‌شده با همهٔ گام‌های D.4 (تفاوت مقدار ← fast path ← قفل ← contributorها ← قفل‌ها ← flush ←
    /// مرحله‌ها ← تازه‌کردن ردیابی ← commit). شمار ردیف‌های نوشته‌شدهٔ خودِ درخواست را برمی‌گرداند.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// مرحله‌ها را به ترتیب <see cref="LedgerStageOrder"/> روی یک context اجرا می‌کند (بازسازی و آزمون‌ها). باید در
    /// تراکنش و زیر قفل همان کارگاه صدا زده شود.
    /// </summary>
    Task RunStagesAsync(LedgerContext context, CancellationToken cancellationToken);
}
