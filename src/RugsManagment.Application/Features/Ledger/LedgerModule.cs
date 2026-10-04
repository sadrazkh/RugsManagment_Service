using Microsoft.Extensions.DependencyInjection;

namespace RugsManagment.Application.Features.Ledger;

/// <summary>
/// ثبت سرویس‌های «گذر سازگاری» حساب‌ها (فاز مدل فرش نسخهٔ ۲). فعلاً خالی است؛ آن فاز ثبت‌هایش را همین‌جا
/// می‌گذارد تا <c>DependencyInjection.cs</c> (که یخ زده) دوباره ویرایش نشود.
/// </summary>
public static class LedgerModule
{
    public static IServiceCollection AddLedgerModule(this IServiceCollection services) => services;
}
