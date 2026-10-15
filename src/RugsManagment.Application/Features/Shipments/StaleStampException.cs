using RugsManagment.Application.Common;
using RugsManagment.Domain.Common;

namespace RugsManagment.Application.Features.Shipments;

/// <summary>
/// جدول ارزش گمرکی با «مهر» کهنه نوشته شد (spec C.5، I.2) → ۴۰۹. جدول یکجا جایگزین می‌شود و xmin ردیف‌ها کمکی نمی‌کند؛
/// مهر (<c>TenantPreferences.DeclaredValuesStamp</c>) با هر نوشتن یکی بالا می‌رود و درخواستی که مهر قبلی را دارد کار دیگری
/// را بی‌صدا پاک نمی‌کند. <see cref="Extensions"/> مهر فعلی را می‌دهد.
/// </summary>
public sealed class StaleStampException(int currentStamp)
    : DomainConflictException(Messages.DeclaredStale.Text, "stamp")
{
    public int CurrentStamp { get; } = currentStamp;

    public override IReadOnlyDictionary<string, object?>? Extensions => new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["code"] = Messages.DeclaredStale.Code,
        ["stamp"] = CurrentStamp
    };
}
