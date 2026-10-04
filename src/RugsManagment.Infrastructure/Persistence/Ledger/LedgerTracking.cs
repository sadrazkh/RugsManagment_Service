using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace RugsManagment.Infrastructure.Persistence.Ledger;

/// <summary>
/// تشخیص «موجودیتی که بی بارگذاریِ ردیابی‌شده وصل شده» (spec D.4 گام ۱، <c>ChangeKind.Unknown</c>).
///
/// چرا لازم است: <c>Update()</c> روی نمونهٔ جدا (مثلاً از یک پرس‌وجوی AsNoTracking) مقدارهای «اصلی» را برابر مقدارهای
/// فعلی می‌گذارد؛ تفاوت‌سنجی آن‌وقت هیچ تغییری نمی‌دید و هزینه‌ها از نو حساب نمی‌شدند. EF خودش نمی‌گوید نمونه از کجا آمده،
/// ولی رویداد <c>ChangeTracker.Tracked</c> می‌گوید آیا ردیابی از یک پرس‌وجو شروع شد (<c>FromQuery</c>). هر نمونه‌ای که
/// نه از پرس‌وجو آمده و نه «افزوده» است (Update/Attach، یا پیدا شدن با کلیدِ ازپیش‌تعیین در یک ناوبری) این‌جا علامت
/// می‌خورد تا تا ذخیرهٔ موفق بعدی «نامعلوم» حساب شود.
///
/// scoped است و در سازندهٔ <c>UnitOfWork</c> و <c>RugRepository</c> ساخته می‌شود، پس پیش از هر بارگذاری یا Update در
/// همان درخواست به رویداد وصل است.
/// </summary>
public sealed class LedgerTracking : IDisposable
{
    private readonly AppDbContext _db;
    private readonly HashSet<object> _attached = new(ReferenceEqualityComparer.Instance);

    public LedgerTracking(AppDbContext db)
    {
        _db = db;
        _db.ChangeTracker.Tracked += OnTracked;
    }

    /// <summary>آیا این نمونه بی بارگذاری ردیابی‌شده وصل شده و از آن به بعد ذخیرهٔ موفقی نداشته است.</summary>
    public bool IsUnknown(object entity) => _attached.Contains(entity);

    /// <summary>بعد از ذخیرهٔ موفق (عکس مقدارها حالا درست است) یا پاک شدن ردیابی.</summary>
    public void Accept() => _attached.Clear();

    public void Dispose() => _db.ChangeTracker.Tracked -= OnTracked;

    private void OnTracked(object? sender, EntityTrackedEventArgs e)
    {
        if (!e.FromQuery && e.Entry.State is EntityState.Modified or EntityState.Unchanged)
            _attached.Add(e.Entry.Entity);
    }
}
