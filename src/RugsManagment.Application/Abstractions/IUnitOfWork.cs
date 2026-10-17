namespace RugsManagment.Application.Abstractions;

/// <summary>
/// ذخیرهٔ نهایی تغییرات دیتابیس — بعد از چند Add/Update یکبار SaveChanges صدا زده می‌شود.
///
/// از فاز ۲ هر <see cref="SaveChangesAsync"/> از «پاس سازگاری» حساب‌ها (spec D.4) می‌گذرد: اگر تغییرها به فرش،
/// مرحله، فروش، سطر خرید یا دیگر داده‌های پولی/قفل‌دار برسند، زیر قفل کارگاه و در یک تراکنش، اول ردیف‌های خود درخواست
/// نوشته می‌شوند و بعد ردیف‌های مشتق‌شده (مبلغ سطرهای خرید، بهای تمام‌شده، عکس بهای فروش) از نو حساب می‌شوند؛ پس DTOی
/// که بعد از ذخیره ساخته می‌شود همان عددهای تازه را دارد. تغییری که به هیچ‌کدام نمی‌رسد همان SaveChanges ساده است.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// همهٔ تغییرها را با پاس سازگاری ذخیره می‌کند. خطای هم‌زمانی → <c>ConcurrencyConflictException</c>، کلید تکراری →
    /// <c>DuplicateKeyException</c>، شمارهٔ هم‌پوشان → <c>SerialOverlapException</c>، قفل‌ها (مثل رقم اعشار بعد از اولین
    /// مبلغ) → <c>DomainConflictException</c>. هر خطا همه‌چیز را برمی‌گرداند و ردیابی DbContext پاک می‌شود.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// همهٔ موجودیت‌های در حال ردیابی را رها می‌کند. قبل از تلاش دوباره بعد از شکست SaveChanges
    /// لازم است؛ وگرنه موجودیتِ شکست‌خورده در DbContext می‌ماند و تلاش بعدی هم با همان خطا شکست می‌خورد.
    /// </summary>
    void Reset();

    /// <summary>
    /// چند ذخیره در یک تراکنش (spec D.4 «Ambient transactions»): <paramref name="work"/> هر چند بار که لازم است
    /// <see cref="SaveChangesAsync"/> را صدا می‌زند؛ پاس سازگاری بعد از هر ذخیره مرحله‌هایش را اجرا می‌کند ولی قفل کارگاه را
    /// فقط یک بار در تراکنش می‌گیرد و commit را به پایان کار می‌سپارد. هر خطا کل کار را برمی‌گرداند و ردیابی پاک می‌شود.
    /// کاربردها: شماره‌گذاری دوبارهٔ نگله‌ها، ثبت و برگشت ورود از اکسل، مسیر ردیف‌به‌ردیف جدول، و گرفتن قفل کارگاه پیش
    /// از flush (<c>ILedgerLock</c>، مثلاً شمارهٔ خودکار E2). اگر تراکنشی از پیش باز است، کار در همان اجرا می‌شود.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default);

    /// <summary>همان <see cref="ExecuteInTransactionAsync(Func{CancellationToken, Task}, CancellationToken)"/> با نتیجه.</summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default);
}
