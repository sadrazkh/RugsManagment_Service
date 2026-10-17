using RugsManagment.Domain.Common;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// «مبلغ ثابت برای یک فرش» در یک ثبت هزینه (spec B.10، D.3 «Pins»): این فرش دقیقاً همین مبلغ را می‌گیرد و باقی‌مانده بین
/// بقیه تقسیم می‌شود. مبلغ همیشه به <b>ارز کارگاه</b> است (بعد از تبدیل دلار یا ریال)، چون با <c>BaseAmount</c> سنجیده
/// می‌شود. فقط روی ثبت «کل» یا رسید منجمد (E9). اگر فرش دیگر جزو فرش‌های ثبت نباشد مبلغ نادیده گرفته می‌شود
/// (<c>W_PIN_DROPPED</c>) ولی می‌ماند تا با برگشتن فرش دوباره اعمال شود. یکتا برای (ثبت، فرش).
/// </summary>
public class CostEntryPin : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid EntryId { get; set; }

    public Guid RugId { get; set; }

    /// <summary>مبلغ به ارز کارگاه (numeric(18,2)، در MoneyScale).</summary>
    public decimal Amount { get; set; }

    public CostEntry Entry { get; set; } = null!;
}
