namespace RugsManagment.Application.Abstractions;

/// <summary>ذخیرهٔ نهایی تغییرات دیتابیس — بعد از چند Add/Update یکبار SaveChanges صدا زده می‌شود</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
