namespace RugsManagment.Application.Ledger;

/// <summary>
/// «قفل» (guard): ذخیره‌ای را که قاعده‌ای را می‌شکند پیش از flush رد می‌کند (مثل تغییر رقم اعشار مبلغ بعد از اولین
/// مبلغ، D.5 #22؛ محمولهٔ قطعی در فاز محموله). در حافظه می‌سنجد، به‌اضافهٔ حداکثر یک جست‌وجوی ایندکس‌دار برای هر ذخیره.
/// برای تغییری با مقدار اصلیِ نامعلوم (<see cref="ChangeKind.Unknown"/>) هر ستون قفل‌دار را با خواندن تازه از دیتابیس
/// مقایسه می‌کند. رد کردن = پرتاب <c>DomainConflictException</c> (۴۰۹) یا زیرکلاس آن.
/// </summary>
public interface ILedgerGuard : ILedgerParticipant
{
    Task CheckAsync(LedgerContext context, CancellationToken cancellationToken);
}
