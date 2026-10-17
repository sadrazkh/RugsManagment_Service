using Microsoft.EntityFrameworkCore;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Infrastructure.Persistence.Repositories;

/// <summary>
/// بارگذاری ProcessStepType برای موتور گردش کار (مسیر سفارشی و ویرایش مسیر).
///
/// فقط نوع‌های سیستمی (TenantId = null) و نوع‌های اختصاصیِ کارگاه کاربر جاری برگردانده می‌شوند.
/// قبلاً یک FindAsync بدون هیچ شرطی بود و با فرستادن شناسهٔ نوع مرحلهٔ کارگاه دیگر، مرحلهٔ
/// آن کارگاه (با نام، نرخ و فرم داینامیکش) به فرشِ این کارگاه وصل می‌شد.
///
/// Scoped است چون به کاربر همان درخواست وابسته است. نتیجه عمداً tracked می‌ماند: موتور آن را
/// به‌عنوان ناوبری مرحلهٔ تازه ست می‌کند و نسخهٔ untracked باعث می‌شد EF بخواهد دوباره درجش کند.
/// </summary>
public sealed class ProcessStepTypeLookup(AppDbContext db, ICurrentUser currentUser) : IProcessStepTypeLookup
{
    public async Task<ProcessStepType> GetRequiredAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // بدون کارگاه (ادمین سیستم/اجرای پس‌زمینه) فقط نوع‌های سیستمی دیده می‌شوند
        var tenantId = currentUser.TenantId;

        return await db.ProcessStepTypes
                   .FirstOrDefaultAsync(
                       s => s.Id == id && (s.TenantId == null || s.TenantId == tenantId),
                       cancellationToken)
               // نوع مرحلهٔ کارگاه دیگر درست مثل ناموجود است (۴۰۴) تا وجودش لو نرود
               ?? throw new KeyNotFoundException($"نوع مرحله {id} یافت نشد.");
    }
}
