using RugsManagment.Domain.Entities;

namespace RugsManagment.Infrastructure.Persistence.Ledger;

/// <summary>
/// تنظیم‌های پاس سازگاری در زیرساخت. <see cref="DerivedTypes"/> نوع ردیف‌هایی است که فقط مرحله‌های پاس می‌نویسند: در
/// تفاوت‌سنجی نیستند (پاس به خودش برنمی‌گردد) و بعد از هر پاس از ردیابی جدا می‌شوند تا DTO عدد تازه را بخواند (D.4 گام ۸).
/// فاز محموله و فاز هزینه‌ها نوع‌های خودشان (<c>ShipmentLineValue</c>، <c>CostAllocation</c>، <c>CostEntryResult</c>) را با
/// <c>services.Configure&lt;LedgerOptions&gt;(o =&gt; o.DerivedTypes.Add(...))</c> در ماژول خودشان اضافه می‌کنند.
/// </summary>
public sealed class LedgerOptions
{
    public HashSet<Type> DerivedTypes { get; } = [typeof(RugCostTotal)];
}
