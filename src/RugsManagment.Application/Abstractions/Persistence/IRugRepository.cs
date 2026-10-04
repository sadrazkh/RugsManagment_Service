using RugsManagment.Application.DTOs.Common;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Abstractions.Persistence;

/// <summary>دسترسی دیتابیس به فرش — همیشه همراه TenantId برای امنیت</summary>
public interface IRugRepository : IRepository<Rug>
{
    /// <summary>
    /// فرش ردیابی‌شده برای ویرایش و صفحهٔ جزئیات، با ورودی، فروش، عکس‌ها (به ترتیب)، مراحل (به ترتیب، با نوع و
    /// انجام‌دهنده) و از فاز ۲ سطرهای خرید (<see cref="Rug.PurchaseLines"/>، به ترتیب SortOrder) — نویسندهٔ فیلدها و
    /// ویرایشگر سطرهای خرید روی همین گراف کار می‌کنند. <see cref="Rug.CostTotal"/> عمداً لود نمی‌شود: ردیف مشتق‌شده فقط
    /// با projection بدون ردیابی خوانده می‌شود (D.4 گام ۸). کارگاه دیگر → null.
    /// </summary>
    Task<Rug?> GetWithWorkflowAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// بارگذاری کامل همهٔ فرش‌های کارگاه همراه مراحل — پرهزینه است.
    /// فقط برای محاسبات آماری (داشبورد) مناسب است؛ برای فهرست از
    /// <see cref="SearchAsync"/> استفاده کنید.
    /// </summary>
    Task<IReadOnlyList<Rug>> ListByTenantAsync(Guid tenantId, RugStatus? status, CancellationToken cancellationToken = default);

    /// <summary>
    /// فهرست صفحه‌بندی‌شده با جستجو، فیلتر و مرتب‌سازی.
    /// شمارش، هزینه و مرحلهٔ جاری همه در SQL محاسبه می‌شوند و موجودیت‌ها
    /// اصلاً materialize نمی‌شوند — مستقل از تعداد کل فرش‌های کارگاه.
    /// <para>از فاز ۲: جستجو <c>SerialNo</c> را هم می‌گردد؛ فیلتر <see cref="RugQuery.Serial"/> تطبیق دقیق/داخل دسته است
    /// (همان قاعدهٔ <see cref="FindBySerialAsync"/>)؛ <see cref="RugSortBy.Serial"/> بر اساس <c>SerialSort</c> با
    /// بی‌شماره‌ها در آخر؛ <see cref="RugQuery.Location"/> با ترتیب C.10 در SQL.</para>
    /// </summary>
    Task<PagedResult<RugListItemDto>> SearchAsync(Guid tenantId, RugQuery query, CancellationToken cancellationToken = default);

    Task<string> GenerateNextSkuAsync(Guid tenantId, CancellationToken cancellationToken = default);

    // ── شمارهٔ بازرگان (فاز ۲) ─────────────────────────────────────

    /// <summary>
    /// بزرگ‌ترین شمارهٔ عددیِ در حال استفاده: <c>MAX(COALESCE("SerialTo", "SerialFrom"))</c> روی فرش‌های فعال کارگاه —
    /// صادرشده و فروخته‌شده هم (شماره بینشان یکتا می‌ماند)، سطل زباله نه، شماره‌های متنی نه. null یعنی هنوز هیچ شمارهٔ
    /// عددی‌ای نیست. قاعدهٔ «شمارهٔ بعدی» (<c>SerialStart</c>، حالت‌ها) در <c>SerialSuggester</c> است، نه این‌جا. حالت
    /// «خودکار» همین را زیر قفل کارگاه و پیش از flush صدا می‌زند (E2)؛ ردیف‌های همان درخواست که هنوز flush نشده‌اند در
    /// این عدد نیستند، پس برای چند فرش تازه در یک درخواست، فراخواننده خودش یکی‌یکی جلو می‌رود.
    /// </summary>
    Task<int?> GetMaxSerialAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// یافتن فرش با شماره (اسکنر، «شماره را بزنید»، <c>/api/rugs/by-serial</c>)؛ فقط فرش‌های فعال کارگاه.
    /// <list type="number">
    /// <item><see cref="SerialMatchType.Exact"/>: <c>SerialNo</c> همان <see cref="SerialNumber.Display"/> است، یا
    /// شمارهٔ تکِ عددی با همان مقدار (پس «0123» فرش «123» را هم پیدا می‌کند).</item>
    /// <item>وگرنه، برای عدد تک، <see cref="SerialMatchType.InRange"/>: دسته‌ای که <c>SerialFrom ≤ n ≤ SerialTo</c>.</item>
    /// </list>
    /// دستهٔ نوشته‌شده («3911 - 3960») فقط تطبیق دقیق دارد. چون همپوشانی رد می‌شود حداکثر یک فرش می‌خورد؛ اگر دادهٔ
    /// قدیمی دو تا داشت، کوچک‌ترین <c>SerialSort</c> و بعد Id. ردیف همان projection فهرست است (پول خام؛ پنهان‌سازی با
    /// سرویس). پیدا نشد → null (کنترلر ۴۰۴).
    /// </summary>
    Task<RugBySerialDto?> FindBySerialAsync(Guid tenantId, SerialNumber serial, CancellationToken cancellationToken = default);

    // ── پیشنهاد مقدار (فاز ۲) ─────────────────────────────────────

    /// <summary>
    /// مقدارهای متمایزی که کارگاه برای یک ستون نوشته (خانه‌های combo)؛ قاعده‌ها در <see cref="RugValuesQuery"/>.
    /// </summary>
    Task<IReadOnlyList<string>> ListDistinctValuesAsync(
        Guid tenantId, RugValuesQuery query, CancellationToken cancellationToken = default);

    // ── سطل زباله ─────────────────────────────────────────────────
    // فیلتر سراسری EF فرش‌های حذف‌شده را از همهٔ کوئری‌های بالا بیرون می‌گذارد،
    // پس این دو متد تنها راه دیدن و بازگرداندن آن‌ها هستند.

    /// <summary>فرش‌های سطل زباله، تازه‌ترین اول.</summary>
    Task<IReadOnlyList<DeletedRugDto>> ListDeletedAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>یک فرش حذف‌شده برای بازگردانی؛ null یعنی وجود ندارد یا مال کارگاه دیگری است.</summary>
    Task<Rug?> GetDeletedAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
}
