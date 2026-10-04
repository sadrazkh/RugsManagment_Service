using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RugsManagment.Infrastructure.Features.Ledger;

/// <summary>
/// ثبت‌های زیرساختی حساب‌ها (repositoryهای گذر سازگاری، کار استارت بررسی حساب‌ها با ترتیب ۲۰۰) — فاز مدل فرش
/// نسخهٔ ۲. فعلاً خالی است تا <c>DependencyInjection.cs</c> (که یخ زده) دوباره ویرایش نشود.
/// </summary>
public static class LedgerInfrastructureModule
{
    public static IServiceCollection AddLedgerModule(this IServiceCollection services, IConfiguration configuration)
        => services;
}
