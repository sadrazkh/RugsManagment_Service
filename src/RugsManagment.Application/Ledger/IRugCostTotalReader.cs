namespace RugsManagment.Application.Ledger;

/// <summary>
/// ستون‌های ردیف <c>RugCostTotal</c> یک فرش، همان‌طور که پاس سازگاری نوشته است (بی‌ردیابی).
/// </summary>
public sealed record RugCostTotalSnapshot(
    Guid RugId,
    decimal PurchaseCost,
    decimal StepCost,
    decimal ExpenseCost,
    decimal Total,
    decimal? CostPerM2,
    decimal CostPerPiece,
    decimal? ExportRevenueShare,
    long LedgerVersion);

/// <summary>
/// خوانندهٔ جمع هزینهٔ ذخیره‌شده برای «جای اول» از هفت جای قدیمیِ «سرمایهٔ کل» (D.4: <c>WorkflowEngine.CalculateRugCosts</c>)
/// و همهٔ DTOهایی که از یک فرش ردیابی‌شده ساخته می‌شوند.
///
/// چرا با حافظهٔ درون‌درخواستی: <c>CalculateRugCosts</c> همگام است و DTO ساز (<c>EntityMappers</c>) به آن تکیه دارد؛ پس
/// بارگذاری‌ها (<c>RugRepository.GetWithWorkflowAsync</c>، <c>ListByTenantAsync</c>) ردیف‌ها را از پیش و ناهمگام در این حافظه
/// می‌گذارند، و پاس سازگاری بعد از هر ذخیره همان فرش‌ها را با عدد تازه جایگزین می‌کند (D.4 گام ۸)؛ فقط اگر چیزی در حافظه
/// نبود یک پرس‌وجوی کوچک همگام اجرا می‌شود. حافظه مال همان scope (درخواست) است، پس عدد کهنهٔ درخواست دیگری را نمی‌بیند.
/// </summary>
public interface IRugCostTotalReader
{
    /// <summary>ردیف جمع فرش؛ null یعنی هنوز ساخته نشده (فرش تازه پیش از ذخیره، یا پیش از بازسازی استارت).</summary>
    RugCostTotalSnapshot? Find(Guid tenantId, Guid rugId);

    /// <summary>ردیف‌های این فرش‌ها را یک‌جا (یک پرس‌وجو) در حافظه می‌گذارد.</summary>
    Task PreloadAsync(IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken = default);

    /// <summary>همهٔ ردیف‌های کارگاه را یک‌جا در حافظه می‌گذارد (داشبورد، تحلیل‌ها).</summary>
    Task PreloadTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// تعداد رقم اعشار مبلغ‌های کارگاه (پیش‌فرض ۰ وقتی ردیف ترجیحات نیست)؛ برای پیش‌نمایش‌ها و محاسبهٔ درون‌حافظه.
    /// </summary>
    int MoneyScale(Guid tenantId);

    /// <summary>همان <see cref="MoneyScale"/>، ناهمگام (برای مسیرهای async).</summary>
    Task<int> MoneyScaleAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>این فرش‌ها را از حافظه برمی‌دارد؛ خواندن بعدی از دیتابیس است.</summary>
    void Invalidate(IEnumerable<Guid> rugIds);

    /// <summary>کل حافظه را پاک می‌کند (بعد از بازسازی یا خطا).</summary>
    void Clear();
}
