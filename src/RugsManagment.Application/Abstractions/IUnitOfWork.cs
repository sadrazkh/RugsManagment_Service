namespace RugsManagment.Application.Abstractions;

/// <summary>ذخیرهٔ نهایی تغییرات دیتابیس — بعد از چند Add/Update یکبار SaveChanges صدا زده می‌شود</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// همهٔ موجودیت‌های در حال ردیابی را رها می‌کند. قبل از تلاش دوباره بعد از شکست SaveChanges
    /// لازم است؛ وگرنه موجودیتِ شکست‌خورده در DbContext می‌ماند و تلاش بعدی هم با همان خطا شکست می‌خورد.
    /// </summary>
    void Reset();
}
