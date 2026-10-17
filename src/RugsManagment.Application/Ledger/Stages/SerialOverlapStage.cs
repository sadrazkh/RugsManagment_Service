namespace RugsManagment.Application.Ledger.Stages;

/// <summary>
/// همپوشانی شماره‌ها (ترتیب ۱۰۰، spec D.4 و E1/E2): شمارهٔ هر فرشی که در این ذخیره تازه است، شماره‌اش عوض شده یا از سطل
/// زباله برگشته، زیر قفل کارگاه با شمارهٔ همهٔ فرش‌های فعال دیگر سنجیده می‌شود — بازهٔ عددی با بازهٔ عددی («3950-3970» با
/// «3911 - 3960») و شمارهٔ متنی با همان متن. ایندکس یکتا فقط متن یکسان را می‌بیند، نه بازه‌ها را.
///
/// روی وضعیتِ flushشده اجرا می‌شود، پس برش خودکار دسته (که فرش اصلی را کوتاه و فرش تازه را با بقیهٔ بازه می‌سازد) با خودش
/// همپوشانی نمی‌بیند. این مرحله فقط می‌سنجد (E2): شمارهٔ خودکار پیش از flush روی موجودیت ردیابی‌شده نوشته می‌شود تا DTO و
/// نسخهٔ برگشتی کهنه نباشند. همپوشانی → <see cref="SerialOverlapException"/> (۴۰۹ با <c>nextFree</c>) و همه‌چیز برمی‌گردد.
/// در بازسازی اجرا نمی‌شود: دادهٔ قدیمی نباید بازسازی حساب‌ها را بخواباند.
/// </summary>
public sealed class SerialOverlapStage(ILedgerRepository repository) : ILedgerStage
{
    public int Order => LedgerStageOrder.SerialOverlap;

    public async Task RunAsync(LedgerContext context, CancellationToken cancellationToken)
    {
        if (context.IsRebuild || context.Impact.SerialCheckRugs.Count == 0)
            return;

        var conflicts = await repository.FindSerialOverlapsAsync(
            context.TenantId, context.Impact.SerialCheckRugs, cancellationToken);
        if (conflicts.Count == 0)
            return;

        var first = conflicts[0];
        var max = await repository.MaxSerialAsync(context.TenantId, [first.RugId], cancellationToken);
        throw new SerialOverlapException(first.Serial, first.OtherSerial, LedgerPass.NextFree(context.SerialStart, max));
    }
}
