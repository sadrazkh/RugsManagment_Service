using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Abstractions.Persistence;

/// <summary>کاربران — ورود با ایمیل</summary>
public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// قفل «تنظیم‌کننده‌های کارگاه» تا پایان تراکنش جاری (<c>pg_advisory_xact_lock</c>). هر ویرایشی که ممکن است یک
    /// تنظیم‌کنندهٔ فعال را کم کند (پایین آوردن نقش، غیرفعال کردن) اول این قفل را می‌گیرد و بعد می‌شمارد؛ پس دو
    /// تنظیم‌کننده‌ای که هم‌زمان نقش هم را پایین می‌آورند پشت هم اجرا می‌شوند و دومی آخرین تنظیم‌کننده را می‌بیند.
    /// فقط درون <c>IUnitOfWork.ExecuteInTransactionAsync</c> معنا دارد؛ بیرون از تراکنش قفل بلافاصله آزاد می‌شد.
    /// </summary>
    Task LockTenantAdminsAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
