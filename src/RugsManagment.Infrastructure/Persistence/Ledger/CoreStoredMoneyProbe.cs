using Microsoft.EntityFrameworkCore;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Infrastructure.Persistence.Ledger;

/// <summary>
/// «آیا کارگاه مبلغی ذخیره کرده؟» برای جدول‌های تا فاز ۲ (spec D.5 #22): سطر خرید؛ مرحله‌ای با هزینه (محاسبه‌شدهٔ غیرصفر،
/// دستی یا تعدیل غیرصفر)؛ فروش؛ پرداخت به طرف خدمات؛ نرخ توافقی؛ نوع مرحلهٔ اختصاصیِ کارگاه با نرخ پیش‌فرض بیشتر از صفر؛
/// قیمت هدف روی یک فرش؛ نرخ پیش‌فرض نوع هزینه. سطل زباله هم شمرده می‌شود (با بازگردانی دوباره زنده است)، پس کل کوئری
/// بی‌فیلتر است. یک رفت‌وبرگشت با چند EXISTS که هر کدام با اولین ردیف می‌ایستد.
///
/// <para>نرخ پیش‌فرضِ نوع هزینهٔ «درصد» (مثل «دلالی» ۲٪) مبلغ نیست و شمرده نمی‌شود؛ وگرنه seed نوع‌های خرید رقم اعشار هر
/// کارگاه تازه را پیش از اولین مبلغ قفل می‌کرد.</para>
/// </summary>
public sealed class CoreStoredMoneyProbe(AppDbContext db) : IStoredMoneyProbe
{
    public async Task<bool> AnyAsync(Guid tenantId, CancellationToken cancellationToken)
        => await db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(_ =>
                db.Set<Domain.Entities.RugPurchaseLine>().Any(l => l.TenantId == tenantId)
                || db.RugWorkflowSteps.Any(s => s.Rug.TenantId == tenantId
                                                && ((s.CalculatedCost != null && s.CalculatedCost != 0)
                                                    || s.ManualCostOverride != null
                                                    || (s.Adjustment != null && s.Adjustment != 0)))
                || db.RugSales.Any(s => s.TenantId == tenantId)
                || db.ProviderPayments.Any(p => p.TenantId == tenantId)
                || db.ServiceProviderRates.Any(r => r.ServiceProvider.TenantId == tenantId)
                || db.ProcessStepTypes.Any(t => t.TenantId == tenantId && t.DefaultUnitRate > 0)
                || db.Rugs.Any(r => r.TenantId == tenantId && r.TargetSalePrice != null)
                || db.Set<Domain.Entities.CostCategory>().Any(c => c.TenantId == tenantId
                                                                   && c.DefaultRate != null
                                                                   && c.DefaultBasis != CostBasis.Percent))
            .FirstOrDefaultAsync(cancellationToken);
}
