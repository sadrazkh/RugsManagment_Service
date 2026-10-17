using RugsManagment.Application.Common;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Support;

/// <summary>
/// پاسخ وب به پرسش «کاربر جاری مبلغ تومانی ببیند؟» — همان سیاست <see cref="Policies.Business"/> (بازرگان و
/// تنظیم‌کننده). سرویس‌های Application با همین پاسخ عضوهای پولی DTO را null می‌کنند و نوشتن پولیِ انباردار را
/// رد می‌کنند؛ viewها هم از طریق <see cref="TenantSettingsAccessor.CanSeeMoney"/> همین را می‌پرسند، پس صفحه و
/// JSON هیچ‌وقت دو جواب متفاوت نمی‌دهند.
///
/// بیرون از درخواست HTTP (کار استارت، seed، بازسازی دفتر) کاربری در کار نیست و کد سامانه باید عدد واقعی
/// را ببیند، پس پاسخ «بله» است. داخل هر درخواست — حتی برای کاربر واردنشده — HttpContext هست و پاسخ از
/// claimهای همان درخواست می‌آید.
/// </summary>
public sealed class MoneyVisibility(IHttpContextAccessor accessor) : IMoneyVisibility
{
    public bool CanSeeMoney
        => accessor.HttpContext is not { } http || Policies.IsBusiness(http.User);
}
