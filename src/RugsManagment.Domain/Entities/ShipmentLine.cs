using RugsManagment.Domain.Common;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// «عضویت» یک فرش در یک نگله (spec B.9، C.3) — یک خط برای هر فرش (ایندکس یکتای <see cref="RugId"/>، پس فرش هرگز در دو
/// نگله نیست). جای فرش («در نگله» / «صادر شده») از همین خط و وضعیت محموله‌اش حساب می‌شود و هرگز ذخیره نمی‌شود (C.10).
///
/// <para>کلیدهای خارجی به فرش و نگله NO ACTION‌اند: فرشی که خط دارد حذف سخت نمی‌شود و نگلهٔ غیرخالی حذف نمی‌شود؛ ولی
/// چون NO ACTION در پایان دستور سنجیده می‌شود، حذف آبشاری کارگاه همچنان کار می‌کند.</para>
///
/// <see cref="Seq"/> ترتیب درون نگله است («افزودن» انتها می‌گذارد؛ کشیدن و «انتقال» جابه‌جا می‌کنند). ارزش دلاری خط در
/// ردیف مشتق‌شدهٔ <see cref="ShipmentLineValue"/> است که فقط پاس سازگاری می‌نویسد. xmin دارد. هرگز روی نمونهٔ
/// ردیابی‌نشده <c>Update()</c> نشود (B.1).
/// </summary>
public class ShipmentLine : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid ShipmentId { get; set; }

    public Guid BaleId { get; set; }

    public Guid RugId { get; set; }

    /// <summary>ترتیب در نگله (۱ به بعد؛ یکتا بودنش اجباری نیست، ترتیب با Seq و بعد شناسه پایدار است).</summary>
    public int Seq { get; set; }

    /// <summary>نرخ دستیِ دلار هر متر برای همین خط (بازرگان؛ numeric(14,2)، ≥ ۰)؛ null یعنی از جدول.</summary>
    public decimal? UsdPerM2Override { get; set; }

    public Shipment Shipment { get; set; } = null!;
    public Bale Bale { get; set; } = null!;
    public Rug Rug { get; set; } = null!;
    public ShipmentLineValue? Value { get; set; }
}
