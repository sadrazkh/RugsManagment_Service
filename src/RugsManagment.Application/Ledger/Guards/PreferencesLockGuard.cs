using RugsManagment.Application.Common;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Ledger.Guards;

/// <summary>
/// قفل «رقم اعشار مبلغ‌ها و واحد پول» (spec D.5 #22): وقتی کارگاه هر مبلغی ذخیره کرده (سطر خرید، مرحلهٔ پول‌دار، فروش،
/// پرداخت، نرخ توافقی، نرخ پیش‌فرض نوع مرحلهٔ کارگاه، قیمت هدف، نرخ پیش‌فرض نوع هزینه، و در فازهای بعد ثبت هزینه و
/// درآمد محموله)، <c>TenantPreferences.MoneyScale</c> و <c>Tenant.Currency</c> دیگر عوض نمی‌شوند → ۴۰۹.
///
/// چرا در پاس و نه فقط در سرویس ترجیحات: هر مسیر نوشتنی (صفحهٔ تنظیمات کارگاه، API ترجیحات، seed، کد آینده) از همین
/// پاس می‌گذرد؛ عوض شدن بی‌صدای این دو، همهٔ مبلغ‌های ذخیره‌شده را با مقیاس یا واحد دیگری بازمعنا می‌کرد. ردیف ترجیحاتِ
/// تازه مثل تغییر از پیش‌فرض (۰) حساب می‌شود؛ ردیفی با مقدار اصلیِ نامعلوم با مقدار دیتابیس سنجیده می‌شود.
/// </summary>
public sealed class PreferencesLockGuard(IEnumerable<IStoredMoneyProbe> probes) : ILedgerGuard
{
    private readonly IReadOnlyList<IStoredMoneyProbe> _probes = probes.ToList();

    public IReadOnlyCollection<Type> WatchedTypes { get; } = [typeof(TenantPreferences), typeof(Tenant)];

    public async Task CheckAsync(LedgerContext context, CancellationToken cancellationToken)
    {
        string? field = null;

        foreach (var change in context.Changes.Of<TenantPreferences>())
        {
            var preferences = change.As<TenantPreferences>();
            var changed = change.Kind switch
            {
                ChangeKind.Modified => change.Has(nameof(TenantPreferences.MoneyScale)),
                // ردیف تازه: مقیاس مؤثر تا الان پیش‌فرض (۰) بود؛ مقدار دیتابیسِ «اصلی» همان context.MoneyScale است
                ChangeKind.Added or ChangeKind.Unknown => preferences.MoneyScale != context.MoneyScale,
                _ => false
            };
            if (changed) field = "moneyScale";
        }

        foreach (var change in context.Changes.Of<Tenant>())
        {
            var tenant = change.As<Tenant>();
            var changed = change.Kind switch
            {
                ChangeKind.Modified => change.Has(nameof(Tenant.Currency)),
                ChangeKind.Unknown => tenant.Currency != context.Currency,
                _ => false
            };
            if (changed) field ??= "currency";
        }

        if (field is null)
            return;

        foreach (var probe in _probes)
        {
            if (await probe.AnyAsync(context.TenantId, cancellationToken))
                throw new DomainConflictException(Messages.MoneyLocked.Text, field);
        }
    }
}
