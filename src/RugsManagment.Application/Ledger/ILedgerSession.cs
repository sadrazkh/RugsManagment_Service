namespace RugsManagment.Application.Ledger;

/// <summary>
/// کارهای EFیِ پاس سازگاری (پیاده‌سازی: <c>Infrastructure/Persistence/Ledger/LedgerSession</c>): تفاوت‌سنجی ردیابی،
/// تراکنش، قفل کارگاه، flush، و تازه‌کردن ردیف‌های مشتق‌شدهٔ در حال ردیابی. Application خودِ ترتیب پاس را در
/// <see cref="LedgerPass"/> دارد و به EF وابسته نمی‌شود.
/// </summary>
public interface ILedgerSession
{
    /// <summary>تفاوت مقدار همهٔ موجودیت‌های ردیابی‌شده (D.4 گام ۱). خودش DetectChanges را صدا می‌زند؛ کارگاهِ مرحله‌ای که فرشش ردیابی نمی‌شود با یک پرس‌وجو پیدا می‌شود.</summary>
    Task<ChangeSet> BuildChangeSetAsync(CancellationToken cancellationToken);

    /// <summary>آیا همین حالا تراکنشی روی DbContext باز است (تراکنش بیرونی، D.4 «Ambient transactions»).</summary>
    bool InTransaction { get; }

    /// <summary>
    /// تراکنش تازه، یا اگر تراکنشی باز است همان (در این حالت commit/rollback به صاحبش سپرده می‌شود و
    /// <see cref="ILedgerTransaction.Owned"/> false است).
    /// </summary>
    Task<ILedgerTransaction> BeginAsync(CancellationToken cancellationToken);

    /// <summary>
    /// <c>pg_advisory_xact_lock(hashtext('rugs:ledger:' || tenantId))</c> — فقط یک بار برای هر کارگاه در هر تراکنش؛ با
    /// پایان تراکنش آزاد می‌شود. بیرون از تراکنش → <see cref="InvalidOperationException"/>.
    /// </summary>
    Task LockTenantAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>ذخیرهٔ ساده بدون پاس (fast path)، با همان ترجمهٔ خطاها.</summary>
    Task<int> SaveWithoutPassAsync(CancellationToken cancellationToken);

    /// <summary>
    /// flush داخل پاس: <c>SaveChangesAsync(acceptAllChangesOnSuccess: false)</c> در تراکنش جاری، با پرچم ورود دوباره تا
    /// خودش پاس تو در تو نسازد (D.4 گام ۶). cascade و SET NULL همین‌جا رخ می‌دهند.
    /// </summary>
    Task<int> FlushAsync(CancellationToken cancellationToken);

    /// <summary>
    /// بعد از مرحله‌ها (D.4 گام ۸): ردیف‌های مشتق‌شدهٔ در حال ردیابی (<c>RugCostTotal</c> و مشابه) جدا می‌شوند، مبلغ
    /// سطرهای خرید و عکس بهای فروشِ ردیابی‌شده از دیتابیس تازه می‌شوند، و خوانندهٔ جمع هزینه
    /// (<c>IRugCostTotalReader</c>) مقدارهای تازه را می‌گیرد؛ پس DTOیی که از همین DbContext ساخته می‌شود عدد تازه دارد.
    /// </summary>
    Task RefreshTrackedAsync(IReadOnlyList<LedgerContext> contexts, CancellationToken cancellationToken);

    /// <summary><c>ChangeTracker.AcceptAllChanges()</c> بعد از پاس موفق (D.4 گام ۹).</summary>
    void AcceptAllChanges();

    /// <summary>بعد از هر خطا: ردیابی پاک می‌شود تا موجودیت شکست‌خورده در DbContext نماند.</summary>
    void Reset();

    /// <summary>
    /// آیا این خطا سرریز عددی یک ستون است (پستگرس 22003)؟ پشتیبان آخرِ سنجش مبلغ‌ها: نویسنده‌ها مبلغ سطر خرید را پیش از
    /// ذخیره می‌سنجند، ولی هر مسیر دیگری (فرم قدیمی، اندازهٔ اول تازه روی سطر بزرگ) که مبلغی بیرون از numeric(18,2) بسازد
    /// به‌جای ۵۰۰ «مبلغ از حد بیشتر» (۴۰۰) می‌گیرد. پیش‌فرض false برای جایگزین‌های آزمون.
    /// </summary>
    bool IsNumericOverflow(Exception exception) => false;
}

/// <summary>تراکنش پاس؛ اگر تراکنش بیرونی بود (<see cref="Owned"/> = false)، commit و rollback کاری نمی‌کنند.</summary>
public interface ILedgerTransaction : IAsyncDisposable
{
    /// <summary>این پاس تراکنش را باز کرده و خودش می‌بندد.</summary>
    bool Owned { get; }

    Task CommitAsync(CancellationToken cancellationToken);

    Task RollbackAsync(CancellationToken cancellationToken);
}

/// <summary>
/// گرفتن صریح قفل حساب‌های کارگاه از درون <c>IUnitOfWork.ExecuteInTransactionAsync</c>، پیش از flush. کاربرد اصلی: شمارهٔ
/// خودکار (E2) — سرویس قفل را می‌گیرد، بزرگ‌ترین شماره را می‌خواند، شمارهٔ بعدی را روی فرشِ ردیابی‌شده می‌نویسد و بعد
/// ذخیره می‌کند؛ پاسِ همان ذخیره قفل را دوباره نمی‌گیرد و <c>SerialOverlapStage</c> فقط می‌سنجد. بیرون از تراکنش →
/// <see cref="InvalidOperationException"/>.
/// </summary>
public interface ILedgerLock
{
    Task AcquireAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
