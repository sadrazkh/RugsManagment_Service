using RugsManagment.Domain.Common;

namespace RugsManagment.Application.Features.Shipments;

/// <summary>
/// تغییری که محمولهٔ قطعی را می‌شکند (spec C.8) → ۴۰۹ با پیام فارسی. <c>ShipmentLockGuard</c> در پاس سازگاری پرتابش
/// می‌کند، پس هر مسیر نوشتنی (PUT فرش، ویرایش گروهی، گردش کار، فروش، سطل زباله، نگله، سربرگ، جدول) یک پاسخ دارد.
/// <see cref="Extensions"/> شمارهٔ محموله را هم می‌دهد تا جزیره پیوند «باز کردن دوباره» بسازد.
/// </summary>
public sealed class ShipmentLockedException(string message, Guid shipmentId, int shipmentNumber, string? field = null)
    : DomainConflictException(message, field)
{
    public Guid ShipmentId { get; } = shipmentId;

    public int ShipmentNumber { get; } = shipmentNumber;

    public override IReadOnlyDictionary<string, object?>? Extensions => new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["code"] = "E_SHIPMENT_LOCKED",
        ["shipmentId"] = ShipmentId,
        ["shipmentNumber"] = ShipmentNumber
    };
}
