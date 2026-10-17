using RugsManagment.Domain.Common;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// یک هدف صریح ثبت هزینه (spec B.10، D.2): یا یک فرش (<see cref="RugId"/>) یا یک نگله (<see cref="BaleId"/>)، هرگز هر دو
/// (CHECK در دیتابیس). فرش برای دامنهٔ «انتخاب» (این فرش، شماره‌ها، فیلتر یا «همهٔ انبار» که هنگام ذخیره به فهرست صریح
/// تبدیل شدند) و برای عکس «خرید یکجای این ورودی» (E4)؛ نگله برای دامنهٔ «این نگله» که عضویتش زنده است.
///
/// با ثبت، فرش و نگله cascade می‌شود؛ پس حذف یک نگله هدفش را می‌برد و پاس سازگاری (که پیش از flush آن را خوانده) ثبت را
/// دوباره تقسیم می‌کند. کارگاه دارد تا تغییر هدف‌ها حتی بی تغییر خودِ ثبت به پاس همان کارگاه برسد.
/// </summary>
public class CostEntryTarget : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid EntryId { get; set; }

    public Guid? RugId { get; set; }

    public Guid? BaleId { get; set; }

    public CostEntry Entry { get; set; } = null!;
}
