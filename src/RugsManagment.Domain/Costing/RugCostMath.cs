using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Domain.Costing;

/// <summary>یک سطر خرید برای محاسبه: مبنا و نرخ (درصد برای <see cref="CostBasis.Percent"/>).</summary>
public readonly record struct PurchaseLineInput(CostBasis Basis, decimal Rate);

/// <summary>یک بند از قیمت‌گذاری ترکیبیِ مرحله.</summary>
public readonly record struct StepPricingItem(StepPricingModel Model, decimal Rate);

/// <summary>یک مرحلهٔ فرش برای جمع هزینهٔ مراحل (مستقل از EF، برای آزمون برابری با SQL).</summary>
public readonly record struct StepCostInput(
    WorkflowStepStatus Status, decimal? CalculatedCost, decimal? ManualCostOverride, decimal? Adjustment);

/// <summary>
/// ترکیب هزینهٔ یک فرش (spec D.1) — همان ستون‌های <c>RugCostTotal</c>.
/// </summary>
/// <param name="PurchaseCost">سطرهای خرید + سهم هزینه‌های گروه «خرید» (مثل خرید یکجا).</param>
/// <param name="StepCost">جمع هزینهٔ مؤثر مراحل انجام‌شده و در حال انجام، هر کدام کف صفر.</param>
/// <param name="ExpenseCost">سهم هزینه‌های گروه «هزینه».</param>
/// <param name="Total">جمع سه بالا.</param>
/// <param name="CostPerM2">ROUND(Total / متراژ کل، MoneyScale)؛ null وقتی متراژ صفر است.</param>
/// <param name="CostPerPiece">ROUND(Total / تعداد، MoneyScale).</param>
public sealed record RugCostBreakdown(
    decimal PurchaseCost, decimal StepCost, decimal ExpenseCost, decimal Total, decimal? CostPerM2, decimal CostPerPiece);

/// <summary>
/// همهٔ فرمول‌های پولیِ یک فرش در C# — همتای <c>Infrastructure/Persistence/Ledger/CostSql</c> در SQL؛ این دو تنها جای
/// این فرمول‌ها هستند (آزمون معماری) و آزمون برابری هر دو را روی بردارهای مشترک می‌سنجد.
///
/// چرا یک جا: پیش از این، «هزینهٔ مؤثر مرحله» و «جمع سرمایه» در هفت جای کد (C# و SQL) جدا نوشته شده بود و هر
/// منبع هزینهٔ تازه باید هفت جا اضافه می‌شد. حالا جمع ذخیره‌شده (<c>RugCostTotal</c>) را پاس سازگاری با همین قاعده‌ها
/// می‌سازد و پیش‌نمایش‌های درون‌حافظه هم از همین کلاس می‌خوانند.
///
/// قاعدهٔ دسته (D.3) همه‌جا: اول برای یک تخته، با گرد کردن نصفه-دور-از-صفر در MoneyScale، بعد × تعداد. نرخ متری
/// سطر خرید همیشه روی <b>اندازهٔ اول</b> است (M.2 #12)؛ قیمت مرحله روی عکس اندازه‌ای که هنگام قیمت‌گذاری گرفته شد
/// (<c>RugWorkflowStep.Priced*</c>). مبلغ دستی و تعدیلِ مرحله برای کل ردیف‌اند و هرگز در تعداد ضرب نمی‌شوند.
/// همهٔ گردکردن‌ها با <see cref="Num.Round(decimal, int)"/>؛ هیچ double ای در کار نیست.
/// </summary>
public static class RugCostMath
{
    /// <summary>درصد سطر خرید بر این عدد تقسیم می‌شود («۲» یعنی ۲٪).</summary>
    private const decimal PercentDivisor = 100m;

    // ═════════════════════════════════════════════════════════════════════
    // سطرهای خرید (B.6، جدول قاعدهٔ دسته D.3)
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// مبلغ یک سطر خرید غیردرصدی:
    /// <list type="bullet">
    /// <item>کل: ROUND(rate, s) — یک بار، بی تعداد؛</item>
    /// <item>هر متر: ROUND(rate × مساحت یک تخته در اندازهٔ اول، s) × تعداد؛</item>
    /// <item>هر تخته: ROUND(rate, s) × تعداد.</item>
    /// </list>
    /// سطر درصدی جمع سطرهای دیگر را لازم دارد؛ از <see cref="PercentLineAmount"/> یا <see cref="PurchaseLineAmounts(IReadOnlyList{PurchaseLineInput}, decimal, int, int)"/> استفاده کنید.
    /// </summary>
    public static decimal PurchaseLineAmount(CostBasis basis, decimal rate, decimal intakePieceArea, int quantity, int moneyScale)
        => basis switch
        {
            CostBasis.Total => Num.Round(rate, moneyScale),
            CostBasis.PerM2 => AreaMath.LineMoney(AreaMath.PieceMoney(rate, intakePieceArea, moneyScale), quantity),
            CostBasis.PerPiece => AreaMath.LineMoney(AreaMath.PieceMoneyPerPiece(rate, moneyScale), quantity),
            CostBasis.Percent => throw new ArgumentException(
                "سطر درصدی به جمع سطرهای دیگر بسته است؛ PercentLineAmount را صدا بزنید.", nameof(basis)),
            _ => throw new ArgumentOutOfRangeException(nameof(basis), basis, "مبنای ناشناخته.")
        };

    /// <summary>
    /// سطر درصدی (دلالی): ROUND(Σ مبلغ سطرهای غیردرصدی × p / 100، s) — یک بار برای کل سطر، روی دسته هم.
    /// </summary>
    public static decimal PercentLineAmount(decimal percent, decimal nonPercentSum, int moneyScale)
        => Num.Round(nonPercentSum * percent / PercentDivisor, moneyScale);

    /// <summary>
    /// مبلغ همهٔ سطرهای خرید یک فرش، هم‌ترتیب با ورودی: اول سطرهای غیردرصدی، بعد هر سطر درصدی روی جمع آن‌ها
    /// (سطرهای درصدی روی هم اثر نمی‌گذارند).
    /// </summary>
    public static IReadOnlyList<decimal> PurchaseLineAmounts(
        IReadOnlyList<PurchaseLineInput> lines, decimal intakePieceArea, int quantity, int moneyScale)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var amounts = new decimal[lines.Count];
        var nonPercentSum = 0m;
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Basis == CostBasis.Percent) continue;
            amounts[i] = PurchaseLineAmount(lines[i].Basis, lines[i].Rate, intakePieceArea, quantity, moneyScale);
            nonPercentSum += amounts[i];
        }

        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Basis == CostBasis.Percent)
                amounts[i] = PercentLineAmount(lines[i].Rate, nonPercentSum, moneyScale);
        }

        return amounts;
    }

    /// <summary>
    /// همان <see cref="PurchaseLineAmounts(IReadOnlyList{PurchaseLineInput}, decimal, int, int)"/> برای سطرهای یک فرش؛
    /// مساحت از اندازهٔ اول و تعداد از خود فرش.
    /// </summary>
    public static IReadOnlyList<decimal> PurchaseLineAmounts(Rug rug, IReadOnlyList<RugPurchaseLine> lines, int moneyScale)
    {
        ArgumentNullException.ThrowIfNull(rug);
        ArgumentNullException.ThrowIfNull(lines);
        return PurchaseLineAmounts(
            lines.Select(l => new PurchaseLineInput(l.Basis, l.Rate)).ToList(), rug.IntakeAreaM2, rug.Quantity, moneyScale);
    }

    // ═════════════════════════════════════════════════════════════════════
    // ثبت هزینه و تبدیل ارز (ردیف‌های دیگر جدول D.3)
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ضریب تبدیل از ارز مبلغ به ارز پایهٔ کارگاه: هم‌ارز → null (تبدیلی نیست)؛ ریال ← تومان ۰٫۱؛ تومان ← ریال ۱۰؛
    /// دلار ← نرخ <paramref name="usdFx"/> (بدون نرخ → <see cref="ArgumentException"/>؛ سرویس پیش از این
    /// <c>E_FX_REQUIRED</c> می‌دهد).
    /// </summary>
    public static decimal? FxFactor(CurrencyUnit amountCurrency, CurrencyUnit baseCurrency, decimal? usdFx)
    {
        if (amountCurrency == baseCurrency) return null;
        return amountCurrency switch
        {
            CurrencyUnit.Usd => usdFx is > 0 ? usdFx : throw new ArgumentException("مبلغ دلاری نرخ ارز لازم دارد.", nameof(usdFx)),
            CurrencyUnit.Rial when baseCurrency == CurrencyUnit.Toman => 0.1m,
            CurrencyUnit.Toman when baseCurrency == CurrencyUnit.Rial => 10m,
            _ => throw new ArgumentOutOfRangeException(nameof(baseCurrency), baseCurrency, "ارز پایهٔ کارگاه فقط تومان یا ریال است.")
        };
    }

    /// <summary>
    /// مبلغ یک تخته در ارز پایه برای ثبت هزینهٔ متری یا دانه‌ای (زنده):
    /// <list type="bullet">
    /// <item>هر متر، ارز پایه: ROUND(rate × مساحت تخته، s)؛</item>
    /// <item>هر متر، ارز دیگر: ROUND(ROUND(rate × مساحت تخته، 2) × fx، s) — اول در ارز اصلی، بعد تبدیل، برای هر تخته؛</item>
    /// <item>هر تخته، ارز پایه: ROUND(rate، s)؛ ارز دیگر: ROUND(rate × fx، s).</item>
    /// </list>
    /// نمونه: ۴۵٫۵۵ دلار روی ۰٫۱۶ متر → ۷٫۲۹ × ۱۰۵٬۰۰۰٫۵ → ۷۶۵٬۴۵۴ برای هر تخته (نه نرخ روی جمع ردیف).
    /// </summary>
    public static decimal EntryPieceAmount(CostBasis basis, decimal rate, decimal pieceArea, int moneyScale, decimal? fx = null)
        => basis switch
        {
            CostBasis.PerM2 when fx is { } f => AreaMath.ConvertPiece(AreaMath.PieceUsd(pieceArea, rate), f, moneyScale),
            CostBasis.PerM2 => AreaMath.PieceMoney(rate, pieceArea, moneyScale),
            CostBasis.PerPiece when fx is { } f => AreaMath.ConvertPiece(rate, f, moneyScale),
            CostBasis.PerPiece => AreaMath.PieceMoneyPerPiece(rate, moneyScale),
            _ => throw new ArgumentOutOfRangeException(nameof(basis), basis, "فقط هزینهٔ متری یا دانه‌ای مبلغ تخته دارد.")
        };

    /// <summary>مبلغ ردیف = مبلغ تخته × تعداد (<see cref="EntryPieceAmount"/>).</summary>
    public static decimal EntryLineAmount(
        CostBasis basis, decimal rate, decimal pieceArea, int quantity, int moneyScale, decimal? fx = null)
        => AreaMath.LineMoney(EntryPieceAmount(basis, rate, pieceArea, moneyScale, fx), quantity);

    /// <summary>
    /// مبلغ پایهٔ یک هزینهٔ «کل» یا درآمد: ROUND(amount × fx، s) یک بار، پیش از تقسیم (بی fx: ROUND(amount، s)).
    /// </summary>
    public static decimal BaseAmount(decimal amount, decimal? fx, int moneyScale)
        => Num.Round(fx is { } f ? amount * f : amount, moneyScale);

    // ═════════════════════════════════════════════════════════════════════
    // قیمت مرحله روی دسته (D.3 «Step pricing on lots»)
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// قیمت یک تخته برای یک مدل قیمت‌گذاری، روی اندازهٔ عکس‌گرفته‌شده:
    /// <list type="bullet">
    /// <item>ثابت / فرمول سفارشی (مثل ثابت): ROUND(rate، s)؛</item>
    /// <item>متر مربع: ROUND(rate × مساحت تخته، s)؛</item>
    /// <item>فوت مربع: ROUND(rate × مساحت تخته × ۱۰٫۷۶۳۹۱۰۴، s)؛</item>
    /// <item>طول / عرض: ROUND(rate × L، s) / ROUND(rate × W، s)؛</item>
    /// <item>ترکیبی: جمع بندها، هر بند جدا برای یک تخته گرد شده. بی بند (JSON خالی، رفتار قدیمی) مثل ثابت؛ فهرست
    /// خالی صفر. بندِ «ترکیبی» درون ترکیبی مثل ثابت حساب می‌شود.</item>
    /// </list>
    /// </summary>
    public static decimal StepPiecePrice(
        StepPricingModel model, decimal rate, decimal length, decimal width, int moneyScale,
        IReadOnlyList<StepPricingItem>? combinedItems = null)
    {
        if (model == StepPricingModel.Combined && combinedItems is not null)
        {
            var sum = 0m;
            foreach (var item in combinedItems)
            {
                var itemModel = item.Model == StepPricingModel.Combined ? StepPricingModel.Fixed : item.Model;
                sum += StepPiecePrice(itemModel, item.Rate, length, width, moneyScale);
            }

            return sum;
        }

        return model switch
        {
            StepPricingModel.PerSquareMeter => Num.Round(rate * AreaMath.PieceArea(length, width), moneyScale),
            StepPricingModel.PerSquareFoot => Num.Round(
                rate * AreaMath.PieceArea(length, width) * AreaMath.SquareFeetPerSquareMeter, moneyScale),
            StepPricingModel.PerLength => Num.Round(rate * Num.Round(length, AreaMath.DimensionDecimals), moneyScale),
            StepPricingModel.PerWidth => Num.Round(rate * Num.Round(width, AreaMath.DimensionDecimals), moneyScale),
            // Fixed، CustomFormula (فعلاً همان ثابت) و Combined بی بند
            _ => Num.Round(rate, moneyScale)
        };
    }

    /// <summary>قیمت ردیف مرحله = قیمت تخته × تعداد عکس‌گرفته (<c>PricedQuantity</c>).</summary>
    public static decimal StepLinePrice(
        StepPricingModel model, decimal rate, decimal length, decimal width, int quantity, int moneyScale,
        IReadOnlyList<StepPricingItem>? combinedItems = null)
        => AreaMath.LineMoney(StepPiecePrice(model, rate, length, width, moneyScale, combinedItems), quantity);

    /// <summary>
    /// هزینهٔ مؤثر یک مرحله: max(0، (دستی ?? محاسبه‌شده ?? 0) + (تعدیل ?? 0)). کف صفر برای هر مرحله جدا، تا تخفیف یک
    /// مرحله از قیمت خرید یا مرحلهٔ دیگری کم نکند. دستی و تعدیل برای کل ردیف‌اند.
    /// </summary>
    public static decimal StepEffectiveCost(decimal? calculatedCost, decimal? manualCostOverride, decimal? adjustment)
        => Math.Max(0m, (manualCostOverride ?? calculatedCost ?? 0m) + (adjustment ?? 0m));

    /// <summary>فقط مرحلهٔ انجام‌شده یا در حال انجام در هزینه حساب می‌شود (در صف، ردشده و لغوشده نه).</summary>
    public static bool CountsTowardCost(WorkflowStepStatus status)
        => status is WorkflowStepStatus.Completed or WorkflowStepStatus.InProgress;

    /// <summary>جمع هزینهٔ مراحل یک فرش (ستون <c>StepCost</c>).</summary>
    public static decimal StepCost(IEnumerable<StepCostInput> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        var sum = 0m;
        foreach (var step in steps)
        {
            if (CountsTowardCost(step.Status))
                sum += StepEffectiveCost(step.CalculatedCost, step.ManualCostOverride, step.Adjustment);
        }

        return sum;
    }

    /// <summary>همان <see cref="StepCost(IEnumerable{StepCostInput})"/> روی مراحل یک فرش.</summary>
    public static decimal StepCost(IEnumerable<RugWorkflowStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        return StepCost(steps.Select(s => new StepCostInput(s.Status, s.CalculatedCost, s.ManualCostOverride, s.Adjustment)));
    }

    // ═════════════════════════════════════════════════════════════════════
    // ترکیب (D.1)
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ستون‌های <c>RugCostTotal</c> از چهار منبع: سطرهای خرید، سهم خرید یکجا، مراحل، سهم هزینه‌ها.
    /// </summary>
    public static RugCostBreakdown Compose(
        decimal purchaseLines, decimal purchaseAllocations, decimal stepCost, decimal expenseAllocations,
        decimal totalAreaM2, int quantity, int moneyScale)
    {
        var purchase = purchaseLines + purchaseAllocations;
        var total = purchase + stepCost + expenseAllocations;
        return new RugCostBreakdown(
            purchase, stepCost, expenseAllocations, total,
            CostPerM2(total, totalAreaM2, moneyScale), CostPerPiece(total, quantity, moneyScale));
    }

    /// <summary>ROUND(Total / متراژ کل، s)؛ null وقتی متراژ صفر (یا منفیِ نامعتبر) است.</summary>
    public static decimal? CostPerM2(decimal total, decimal totalAreaM2, int moneyScale)
        => totalAreaM2 > 0 ? Num.Round(total / totalAreaM2, moneyScale) : null;

    /// <summary>ROUND(Total / تعداد، s).</summary>
    public static decimal CostPerPiece(decimal total, int quantity, int moneyScale)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);
        return Num.Round(total / quantity, moneyScale);
    }
}
