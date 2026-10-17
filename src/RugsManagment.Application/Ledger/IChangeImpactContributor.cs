namespace RugsManagment.Application.Ledger;

/// <summary>
/// چیزی که در پاس سازگاری به نوع‌هایی از موجودیت «گوش می‌دهد». مجموع این نوع‌ها در همهٔ contributorها و guardهای ثبت‌شده
/// تعیین می‌کند کدام ذخیره راه قفل‌دار را می‌رود و کدام ذخیرهٔ ساده است (D.4 گام ۲، «fast path»). پس فازی که نوع تازه‌ای
/// اضافه می‌کند (محموله، ثبت هزینه، …) فقط contributor/guard خودش را ثبت می‌کند؛ هیچ فهرست مشترکی ویرایش نمی‌شود.
/// </summary>
public interface ILedgerParticipant
{
    /// <summary>نوع موجودیت‌هایی که تغییرشان برای این بخش مهم است (همان نوع یا زیرنوعش).</summary>
    IReadOnlyCollection<Type> WatchedTypes { get; }
}

/// <summary>
/// «contributor»: تفاوت مقدار (<see cref="ChangeSet"/>) را به اثر (<see cref="LedgerImpact"/>) تبدیل می‌کند (D.4 گام ۴، جدول
/// D.5). بعد از گرفتن قفل کارگاه و پیش از flush اجرا می‌شود؛ پس هر خواندن دیتابیسی همین‌جا انجام می‌شود، از جمله
/// وابسته‌های ردیفی که حذف می‌شود (cascade و SET NULL در flush رخ می‌دهند و بعد از آن دیگر دیده نمی‌شوند).
///
/// contributor می‌تواند روی موجودیت ردیابی‌شده مقدار سروری بنویسد (مثلاً شمارهٔ خودکار E2: زیر قفل و پیش از flush)،
/// ولی تغییرهای خودش دوباره تفاوت‌سنجی نمی‌شوند؛ پس باید اثرشان را خودش در <see cref="LedgerImpact"/> بگذارد.
/// </summary>
public interface IChangeImpactContributor : ILedgerParticipant
{
    Task ContributeAsync(LedgerContext context, CancellationToken cancellationToken);
}
