using System.Globalization;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Infrastructure.Persistence.Ledger;

/// <summary>
/// همهٔ فرمول‌های پولی فرش در SQL — همتای <c>Domain/Costing/RugCostMath</c> در C#. این دو تنها جای این فرمول‌ها هستند
/// (آزمون معماری «یک جای فرمول»؛ پوشهٔ Migrations استثناست چون migration تاریخی است) و آزمون برابری هر دو را روی بردارهای
/// مشترک <c>tests/fixtures/math-vectors.json</c> می‌سنجد.
///
/// چرا یک جا: پیش از فاز ۲، «هزینهٔ مؤثر مرحله» و «سرمایهٔ کل» در هفت جای کد (سه کوئری فهرست/مرتب‌سازی/سطل زباله، دو
/// کوئری صورت‌حساب طرف خدمات، موتور گردش کار، seed) جدا نوشته شده بود و هر منبع هزینهٔ تازه باید هفت جا اضافه می‌شد.
///
/// قاعده‌ها (spec D.1، D.3): همهٔ حساب‌ها <c>numeric</c> است (هیچ float)، <c>round(numeric, s)</c> پستگرس نصفه را دور از صفر
/// گرد می‌کند (همان <c>Num.Round</c>)، «قاعدهٔ دسته» اول برای یک تخته گرد و بعد × تعداد، و مساحت از ستون‌های تولیدشدهٔ
/// <c>IntakeAreaM2</c>/<c>TotalAreaM2</c> (خودشان <c>round(L×W, 2)</c>) خوانده می‌شود. هر متد یک تکه SQL با نام مستعار جدول
/// می‌سازد؛ مقدار کاربر هرگز در متن نمی‌آید (پارامتر).
/// </summary>
public static class CostSql
{
    private static readonly string InProgress = Int((int)WorkflowStepStatus.InProgress);
    private static readonly string Completed = Int((int)WorkflowStepStatus.Completed);
    private static readonly string BasisTotal = Int((int)CostBasis.Total);
    private static readonly string BasisPerM2 = Int((int)CostBasis.PerM2);
    private static readonly string BasisPerPiece = Int((int)CostBasis.PerPiece);
    private static readonly string BasisPercent = Int((int)CostBasis.Percent);

    // ═════════════════════════════════════════════════════════════════════
    // مرحله‌ها (D.1 StepCost)
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// هزینهٔ مؤثر یک مرحله: <c>GREATEST(0, COALESCE(دستی، محاسبه‌شده، 0) + COALESCE(تعدیل، 0))</c> — کف صفر برای هر مرحله
    /// جدا، تا تخفیف یک مرحله از مرحلهٔ دیگر یا قیمت خرید کم نکند. همتای <c>RugCostMath.StepEffectiveCost</c>.
    /// </summary>
    public static string StepEffectiveCost(string step)
        => $"GREATEST(0, COALESCE({step}.\"ManualCostOverride\", {step}.\"CalculatedCost\", 0) + COALESCE({step}.\"Adjustment\", 0))";

    /// <summary>فقط مرحلهٔ «در حال انجام» و «انجام‌شده» در بها شمرده می‌شود. همتای <c>RugCostMath.CountsTowardCost</c>.</summary>
    public static string StepCounts(string step) => $"{step}.\"Status\" IN ({InProgress}, {Completed})";

    /// <summary>وضعیت یک مرحله برابر «انجام‌شده».</summary>
    public static string StepCompleted(string step) => $"{step}.\"Status\" = {Completed}";

    /// <summary>وضعیت یک مرحله برابر «در حال انجام».</summary>
    public static string StepInProgress(string step) => $"{step}.\"Status\" = {InProgress}";

    /// <summary>جمع هزینهٔ مراحل یک فرش (ستون <c>StepCost</c>) به‌صورت زیرکوئری. همتای <c>RugCostMath.StepCost</c>.</summary>
    public static string StepCostOfRug(string rugIdExpression)
        => $"(SELECT COALESCE(SUM({StepEffectiveCost("st")}), 0) FROM \"RugWorkflowSteps\" st " +
           $"WHERE st.\"RugId\" = {rugIdExpression} AND {StepCounts("st")})";

    // ═════════════════════════════════════════════════════════════════════
    // سطرهای خرید (B.6، جدول قاعدهٔ دسته D.3)
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// مبلغ سطر غیردرصدی: کل ROUND(rate, s)؛ هر متر ROUND(rate × مساحت یک تخته در اندازهٔ اول، s) × تعداد؛ هر تخته
    /// ROUND(rate, s) × تعداد. سطر درصدی این‌جا ۰ است (به جمع بقیه بسته است). همتای <c>RugCostMath.PurchaseLineAmount</c>.
    /// </summary>
    public static string NonPercentLineAmount(string line, string rug, string scale)
        => $"CASE {line}.\"Basis\" " +
           $"WHEN {BasisTotal} THEN round({line}.\"Rate\", {scale}) " +
           $"WHEN {BasisPerM2} THEN round({line}.\"Rate\" * {rug}.\"IntakeAreaM2\", {scale}) * {rug}.\"Quantity\" " +
           $"WHEN {BasisPerPiece} THEN round({line}.\"Rate\", {scale}) * {rug}.\"Quantity\" " +
           "ELSE 0 END";

    /// <summary>سطر درصدی (دلالی): یک بار ROUND(جمع سطرهای غیردرصدی × p / 100، s). همتای <c>RugCostMath.PercentLineAmount</c>.</summary>
    public static string PercentLineAmount(string percent, string nonPercentSum, string scale)
        => $"round({nonPercentSum} * {percent} / 100, {scale})";

    /// <summary>شرط «این سطر درصدی است».</summary>
    public static string IsPercent(string line) => $"{line}.\"Basis\" = {BasisPercent}";

    /// <summary>
    /// CTEهای مبلغ تازهٔ همهٔ سطرهای خرید فرش‌های <paramref name="rugIds"/> در کارگاه <paramref name="tenant"/>:
    /// <c>pl_calc</c> (مبلغ غیردرصدی هر سطر)، <c>pl_sums</c> (جمع غیردرصدی هر فرش) و <c>pl_expected</c>
    /// (<c>"Id"</c>، <c>"RugId"</c>، <c>amount</c>). یک تعریف برای نوشتن (مرحلهٔ ۲۰۰) و سنجیدن (LedgerVerifier).
    /// متن با «WITH» شروع نمی‌شود تا بشود CTEهای دیگری کنارش گذاشت.
    /// </summary>
    public static string ExpectedLineAmountsCtes(string tenant, string rugIds, string scale)
        => "pl_calc AS (" +
           $"SELECT l.\"Id\", l.\"RugId\", l.\"Basis\", l.\"Rate\", {NonPercentLineAmount("l", "r", scale)} AS amt " +
           "FROM \"RugPurchaseLines\" l JOIN \"Rugs\" r ON r.\"Id\" = l.\"RugId\" " +
           $"WHERE r.\"TenantId\" = {tenant} AND l.\"RugId\" = ANY({rugIds})), " +
           "pl_sums AS (" +
           $"SELECT c.\"RugId\", COALESCE(SUM(c.amt) FILTER (WHERE NOT {IsPercent("c")}), 0) AS s " +
           "FROM pl_calc c GROUP BY c.\"RugId\"), " +
           "pl_expected AS (" +
           "SELECT c.\"Id\", c.\"RugId\", " +
           $"CASE WHEN {IsPercent("c")} THEN {PercentLineAmount("c.\"Rate\"", "ps.s", scale)} ELSE c.amt END AS amount " +
           "FROM pl_calc c JOIN pl_sums ps ON ps.\"RugId\" = c.\"RugId\")";

    // ═════════════════════════════════════════════════════════════════════
    // ترکیب (D.1)
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>جمع مبلغ ذخیره‌شدهٔ سطرهای خرید یک فرش (زیرکوئری؛ بی سطر صفر).</summary>
    public static string PurchaseLinesOfRug(string rugIdExpression)
        => $"(SELECT COALESCE(SUM(pl.\"Amount\"), 0) FROM \"RugPurchaseLines\" pl WHERE pl.\"RugId\" = {rugIdExpression})";

    /// <summary>جمع مبلغ «درست» سطرهای یک فرش از CTEِ <see cref="ExpectedLineAmountsCtes"/> (برای سنجش).</summary>
    public static string ExpectedPurchaseLinesOfRug(string rugIdExpression)
        => $"(SELECT COALESCE(SUM(e.amount), 0) FROM pl_expected e WHERE e.\"RugId\" = {rugIdExpression})";

    /// <summary>
    /// قیمت خرید = سطرهای خرید + سهم ثبت‌های گروه خرید (خرید یکجا)؛ سهمِ نبوده صفر. همتای <c>RugCostMath.Compose</c>.
    /// </summary>
    public static string PurchaseCost(string purchaseLines, string purchaseAllocations)
        => $"({purchaseLines} + COALESCE({purchaseAllocations}, 0))";

    /// <summary>سهم ثبت‌های گروه هزینه؛ سهمِ نبوده صفر.</summary>
    public static string ExpenseCost(string expenseAllocations) => $"COALESCE({expenseAllocations}, 0)";

    /// <summary>بهای تمام‌شده = خرید + مراحل + هزینه‌ها. همتای <c>RugCostMath.Compose</c>.</summary>
    public static string Total(string purchase, string step, string expense) => $"({purchase} + {step} + {expense})";

    /// <summary>بها برای هر متر مربع: ROUND(کل ÷ متراژ کل ردیف، s)؛ NULL وقتی متراژ صفر است. همتای <c>RugCostMath.CostPerM2</c>.</summary>
    public static string CostPerM2(string total, string totalArea, string scale)
        => $"CASE WHEN {totalArea} > 0 THEN round({total} / {totalArea}, {scale}) END";

    /// <summary>بها برای هر تخته: ROUND(کل ÷ تعداد، s). همتای <c>RugCostMath.CostPerPiece</c>.</summary>
    public static string CostPerPiece(string total, string quantity, string scale)
        => $"round({total} / {quantity}, {scale})";

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);
}
