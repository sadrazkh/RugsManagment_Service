namespace RugsManagment.Application.DTOs.Rugs;

/// <summary>
/// <c>GET /api/diagnostics/cost-consistency</c> (Configure) و صفحهٔ «سلامت حساب‌ها»: نتیجهٔ <c>LedgerVerifier</c>، که همه‌چیز
/// را از نو با همان قاعده‌ها حساب می‌کند و با ردیف‌های مشتق‌شدهٔ ذخیره‌شده می‌سنجد (D.4 «Recovery»).
///
/// چرا در DTOهای فرش: قرارداد بین دو بستهٔ موازی فاز ۲ است (حساب‌کننده در <c>App/Ledger</c>، کنترلر و جزیره در Web)؛
/// هر دو همین شکل را می‌بینند. فهرست خالی یعنی «سالم».
/// </summary>
/// <param name="Checked">تعداد فرش‌هایی که سنجیده شد.</param>
/// <param name="Mismatches">ناهمخوانی‌ها؛ هر فرش ممکن است چند سطر (چند بررسی) داشته باشد.</param>
public record CostConsistencyReportDto(
    int Checked,
    IReadOnlyList<CostMismatchDto> Mismatches);

/// <summary>یک ناهمخوانی میان مقدار ذخیره‌شده و مقدار از نو حساب‌شده.</summary>
/// <param name="RugId">فرش.</param>
/// <param name="Sku">کد انبار، برای پیدا کردن.</param>
/// <param name="SerialNo">شمارهٔ بازرگان، برای پیدا کردن؛ null یعنی بی‌شماره.</param>
/// <param name="Check">کدام بررسی (<see cref="CostMismatchCheck"/>).</param>
/// <param name="Stored">مقدار ذخیره‌شده؛ null وقتی ردیف اصلاً نیست.</param>
/// <param name="Expected">مقداری که باید باشد.</param>
/// <param name="Detail">توضیح کوتاه فارسی (مثلاً برچسب سطر خرید)؛ null یعنی ندارد.</param>
public record CostMismatchDto(
    Guid RugId,
    string Sku,
    string? SerialNo,
    string Check,
    decimal? Stored,
    decimal? Expected,
    string? Detail);

/// <summary>کد بررسی‌های <c>LedgerVerifier</c> (رشته در JSON). فقط افزودنی.</summary>
public static class CostMismatchCheck
{
    /// <summary>ردیف <c>RugCostTotal</c> برای فرش فعال نیست.</summary>
    public const string MissingTotal = "missingTotal";

    /// <summary>یکی از ستون‌های <c>RugCostTotal</c> (خرید، مراحل، هزینه‌ها، کل، هر متر، هر تخته) با محاسبهٔ تازه فرق دارد.</summary>
    public const string Total = "total";

    /// <summary>مبلغ یک سطر خرید با قاعدهٔ دسته فرق دارد.</summary>
    public const string PurchaseLine = "purchaseLine";

    /// <summary>جفت مؤثر با COALESCE(نهایی، اول) نمی‌خواند (B.4).</summary>
    public const string EffectiveSize = "effectiveSize";

    /// <summary>نسخهٔ قاعده‌ها (<c>LedgerVersion</c>) کهنه است.</summary>
    public const string StaleVersion = "staleVersion";
}

/// <summary>
/// <c>POST /api/diagnostics/ledger-rebuild</c> (Configure): نتیجهٔ <c>ILedgerMaintenance.RebuildAsync</c> که همهٔ ردیف‌های
/// مشتق‌شدهٔ کارگاه را از نو می‌نویسد.
/// </summary>
/// <param name="Rugs">تعداد فرش‌هایی که بهایشان از نو نوشته شد.</param>
/// <param name="Entries">تعداد ثبت‌های هزینه که از نو تقسیم شد (تا فاز هزینه‌ها ۰).</param>
/// <param name="Lines">تعداد خط‌های محموله که ارزششان از نو حساب شد (تا فاز محموله ۰).</param>
public record LedgerRebuildResultDto(
    int Rugs,
    int Entries,
    int Lines);
