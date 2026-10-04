using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Ledger;
using RugsManagment.Infrastructure.Persistence.Ledger;

namespace RugsManagment.Infrastructure.Persistence;

/// <summary>
/// یک تراکنش واحد — همهٔ Repositoryها همان DbContext را share می‌کنند؛ SaveChanges یکجا commit می‌کند.
///
/// از فاز ۲ هر ذخیره از «پاس سازگاری» (<see cref="ILedgerPass"/>، spec D.4) می‌گذرد: ذخیره‌ای که به داده‌های پولی/قفل‌دار
/// نمی‌رسد همان SaveChanges ساده است، و بقیه زیر قفل کارگاه، دو مرحله‌ای (اول ردیف‌های خودِ درخواست، بعد ردیف‌های
/// مشتق‌شده با SQL) و در یک تراکنش ذخیره می‌شوند. استثناهای EF/Npgsql همان‌جا به استثناهای Application ترجمه می‌شوند
/// (<c>ConcurrencyConflictException</c>، <c>DuplicateKeyException</c>، <c>SerialOverlapException</c>) تا Application
/// (که عمداً به EF Core وابسته نیست) پیام فارسی مناسب بدهد.
///
/// <see cref="LedgerTracking"/> عمداً این‌جا گرفته می‌شود: هر سرویسی که ذخیره می‌کند UnitOfWork را در سازنده دارد، پس
/// ردیاب «وصل‌شده بی بارگذاری» پیش از هر بارگذاری و Update همان درخواست به DbContext گوش می‌دهد.
/// </summary>
public class UnitOfWork(AppDbContext db, ILedgerPass pass, ILedgerSession session, LedgerTracking tracking) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => pass.SaveChangesAsync(cancellationToken);

    public void Reset() => session.Reset();

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        await ExecuteInTransactionAsync<object?>(async ct =>
        {
            await work(ct);
            return null;
        }, cancellationToken);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        _ = tracking; // ردیاب باید پیش از کار ساخته شده باشد (سازنده)

        // تراکنش بیرونی از پیش: همین کار جزئی از آن است و commit مال صاحبش
        if (db.Database.CurrentTransaction is not null)
            return await work(cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await work(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
                // تراکنش پیش از این بسته شده است
            }

            // ردیف‌های پذیرفته‌شدهٔ ذخیره‌های میانی دیگر در دیتابیس نیستند؛ ردیابی و حافظهٔ جمع هزینه پاک می‌شوند
            session.Reset();
            throw;
        }
    }
}
