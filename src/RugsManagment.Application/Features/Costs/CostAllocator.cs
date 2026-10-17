using RugsManagment.Application.DTOs.Costs;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Costing;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Costs;

/// <summary>یک فرش عضو یک ثبت هزینه، با هر چیزی که تقسیم و هشدارها لازم دارند (همه از وضعیتِ flushشده).</summary>
/// <param name="RugId">فرش.</param>
/// <param name="Serial">شمارهٔ بازرگان (یا کد انبار) برای پیام‌ها و سهم‌ها.</param>
/// <param name="SerialSort">کلید شکستن تساوی (null آخر).</param>
/// <param name="Quantity">تعداد تخته‌ها.</param>
/// <param name="IntakePieceArea">مساحت یک تخته در اندازهٔ اول (ستون تولیدشدهٔ <c>IntakeAreaM2</c>).</param>
/// <param name="EffectivePieceArea">مساحت یک تخته در اندازهٔ مؤثر (ستون تولیدشدهٔ <c>AreaM2</c>).</param>
/// <param name="LineUsd">دلار خط (ارزش اظهاری)؛ null یعنی بی خط یا بی نرخ.</param>
/// <param name="PurchaseCost">قیمت خرید (<c>RugCostTotal.PurchaseCost</c>: سطرها + خرید یکجا).</param>
/// <param name="HasPurchaseLines">سطر خرید غیرصفر دارد (<c>W_PURCHASE_DOUBLE</c>).</param>
/// <param name="HasLinkedStep">مرحلهٔ هزینه‌دارِ همان نوع مرحلهٔ نوع هزینه را دارد (<c>W_STEP_DOUBLE</c>).</param>
/// <param name="InLateShipment">در محموله‌ای است که پیش از ثبت این هزینه قطعی شده (<c>W_LATE_COST</c>).</param>
public sealed record AllocationMember(
    Guid RugId,
    string? Serial,
    long? SerialSort,
    int Quantity,
    decimal IntakePieceArea,
    decimal EffectivePieceArea,
    decimal? LineUsd,
    decimal PurchaseCost,
    bool HasPurchaseLines,
    bool HasLinkedStep,
    bool InLateShipment);

/// <summary>یک خط محموله برای سهم درآمد: فرش، دلار خط و کلید شماره.</summary>
public sealed record RevenueLine(Guid RugId, decimal? LineUsd, long? SerialSort);

/// <summary>یک مبلغ ثابت (به ارز کارگاه) با شمارهٔ فرشش (برای پیام «حساب نشد»).</summary>
public sealed record AllocationPin(Guid RugId, decimal Amount, string? Serial);

/// <summary>آنچه از خودِ ثبت برای تقسیم لازم است.</summary>
/// <param name="Group">خرید یا هزینه.</param>
/// <param name="Basis">کل، هر متر یا هر تخته.</param>
/// <param name="Rate">نرخ «هر متر»/«هر تخته» در ارز ثبت.</param>
/// <param name="Amount">مبلغ ثبت در ارز خودش (کل؛ رسید منجمد).</param>
/// <param name="Currency">ارز ثبت.</param>
/// <param name="FxRate">نرخ دلار.</param>
/// <param name="BaseAmount">کل به ارز کارگاه برای «کل» و رسید منجمد.</param>
/// <param name="Frozen">رسید منجمد (D.3).</param>
/// <param name="AreaBasis">پایهٔ متراژ.</param>
/// <param name="Method">روش تقسیم «کل».</param>
/// <param name="TargetDeleted">نگله، محموله یا ورودیِ ثبت حذف شده (<c>W_TARGET_DELETED</c> به‌جای <c>W_EMPTY_TARGET</c>).</param>
/// <param name="LateWithoutMembers">محمولهٔ ثبت پیش از ثبت قطعی شده، حتی اگر فرشی ندارد.</param>
public sealed record AllocationEntry(
    CostGroup Group,
    CostBasis Basis,
    decimal? Rate,
    decimal? Amount,
    CurrencyUnit Currency,
    decimal? FxRate,
    decimal BaseAmount,
    bool Frozen,
    AreaBasis AreaBasis,
    AllocationMethod Method,
    bool TargetDeleted = false,
    bool LateWithoutMembers = false)
{
    /// <summary>ثبت زندهٔ «هر متر»/«هر تخته».</summary>
    public bool IsLive => Basis is CostBasis.PerM2 or CostBasis.PerPiece && !Frozen;
}

/// <summary>سهم یک فرش.</summary>
public sealed record AllocationShare(Guid RugId, decimal Amount, long WeightUnits, bool IsPinned, decimal? PerPiece);

/// <summary>یک هشدار (پیام فارسی هنگام خواندن ساخته می‌شود تا واژهٔ نگله و واحد پول کارگاه بنشیند).</summary>
public sealed record AllocationWarning(string Code, int Count, decimal? Amount = null, string? Serial = null);

/// <summary>نتیجهٔ تقسیم یک ثبت.</summary>
/// <param name="BaseAmount">کلی که تقسیم شد (به ارز کارگاه).</param>
/// <param name="OriginalAmount">همان کل در ارز ثبت.</param>
/// <param name="Lines">شمار فرش‌های عضو.</param>
/// <param name="Pieces">جمع تخته‌ها.</param>
/// <param name="AreaM2">جمع متراژ در پایهٔ ثبت.</param>
/// <param name="TotalWeightUnits">جمع وزن فرش‌های بی مبلغ ثابت.</param>
/// <param name="Allocated">جمع سهم‌ها.</param>
/// <param name="Unallocated">باقی‌مانده.</param>
/// <param name="State">وضع.</param>
/// <param name="Shares">سهم‌ها (فقط اعضا و مبلغ‌های ثابتِ فعال)، به ترتیب شماره.</param>
/// <param name="Warnings">هشدارها، به ترتیب ثابت کد.</param>
/// <param name="WeightUnit">واحد وزن (<c>ShareWeightUnits</c>).</param>
public sealed record AllocationResult(
    decimal BaseAmount,
    decimal OriginalAmount,
    int Lines,
    int Pieces,
    decimal AreaM2,
    long TotalWeightUnits,
    decimal Allocated,
    decimal Unallocated,
    AllocationState State,
    IReadOnlyList<AllocationShare> Shares,
    IReadOnlyList<AllocationWarning> Warnings,
    string WeightUnit);

/// <summary>
/// تقسیم‌کنندهٔ ثبت هزینه (spec D.3) — خالص و بی‌وابستگی، تنها جایی که «سهم هر فرش» ساخته می‌شود. هم مرحله‌های پاس
/// سازگاری (۴۰۰ و ۶۰۰)، هم پیش‌نمایش پنل و هم سنجش سلامت حساب‌ها همین را صدا می‌زنند؛ پس پیش‌نمایش همان عددی است که
/// ذخیره می‌شود.
///
/// <list type="bullet">
/// <item><b>زنده («هر متر»/«هر تخته» بی رسید):</b> سهم هر فرش با قاعدهٔ دسته (<see cref="RugCostMath.EntryPieceAmount"/>: اول برای
/// یک تخته گرد، ارز دیگر همان‌جا برای هر تخته تبدیل، بعد × تعداد)؛ کل = Σ.</item>
/// <item><b>«کل» یا رسید منجمد:</b> مبلغ‌های ثابتِ فعال اول، باقی‌مانده با «بزرگ‌ترین باقی‌مانده» (<see cref="LargestRemainder"/>)
/// روی بقیه با وزن روش (صدم متر × تعداد، تعداد، ۱، سنت دلار اظهاری، سنت قیمت خرید). رسید متری به نسبت متراژ (همان پایه) و
/// رسید دانه‌ای به تعداد تقسیم می‌شود.</item>
/// <item><b>ناوردا:</b> تقسیم‌شده + تقسیم‌نشده = کل برای هر ثبت، حتی وقتی همه مبلغ ثابت دارند.</item>
/// </list>
/// هر اجرا همهٔ هشدارها را از نو می‌سازد (D.4)، پس هیچ هشداری کهنه نمی‌ماند.
/// </summary>
public static class CostAllocator
{
    /// <summary>وزن «صدم متر مربع» برای متراژ (۴٫۳۷ → ۴۳۷).</summary>
    private const decimal AreaUnitsPerSquareMeter = 100m;

    /// <summary>وزن «سنت» برای دلار اظهاری و قیمت خرید (مستقل از MoneyScale).</summary>
    private const decimal CentsPerUnit = 100m;

    /// <summary>
    /// ضریب تبدیل ارز ثبت به ارز کارگاه (D.3): هم‌ارز null؛ ریال ← تومان ۰٫۱؛ تومان ← ریال ۱۰؛ دلار ← نرخ (بی نرخ
    /// <see cref="ArgumentException"/>، که سرویس پیش از آن <c>E_FX_REQUIRED</c> داده است).
    /// </summary>
    public static decimal? Fx(CurrencyUnit currency, CurrencyUnit tenantCurrency, decimal? fxRate)
        => RugCostMath.FxFactor(currency, tenantCurrency, fxRate);

    /// <summary>مبلغ پایهٔ ثبت «کل» یا درآمد: ROUND(amount × fx، s) یک بار (D.3، ردیف «Total entry, revenue»).</summary>
    public static decimal BaseOfTotal(decimal amount, CurrencyUnit currency, CurrencyUnit tenantCurrency, decimal? fxRate, int moneyScale)
        => RugCostMath.BaseAmount(amount, Fx(currency, tenantCurrency, fxRate), moneyScale);

    /// <summary>
    /// درآمد محموله به ارز کارگاه (D.6): ROUND(مبلغ × نرخ، s) یک بار؛ null بی درآمد یا وقتی درآمد دلاری نرخ ندارد.
    /// </summary>
    public static decimal? RevenueBase(
        decimal? amount, CurrencyUnit? currency, decimal? fxRate, CurrencyUnit tenantCurrency, int moneyScale)
    {
        if (amount is not { } a || currency is not { } c) return null;
        if (c == CurrencyUnit.Usd && fxRate is not > 0) return null;
        return BaseOfTotal(a, c, tenantCurrency, fxRate, moneyScale);
    }

    /// <summary>
    /// سهم درآمد هر فرش (D.6): درآمد پایه با «بزرگ‌ترین باقی‌مانده» به نسبت سنت دلار خط؛ خط بی دلار وزن صفر دارد (همهٔ وزن‌ها
    /// صفر → مساوی). بی خط → هیچ.
    /// </summary>
    public static IReadOnlyDictionary<Guid, decimal> RevenueShares(decimal revenueBase, int moneyScale, IReadOnlyList<RevenueLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var distinct = lines.GroupBy(l => l.RugId).Select(g => g.First()).ToList();
        if (distinct.Count == 0) return new Dictionary<Guid, decimal>();
        var members = distinct.Select(l => new LrMember(l.RugId, l.LineUsd is { } usd ? Cents(usd) : 0, l.SerialSort)).ToList();
        var split = LargestRemainder.Split(revenueBase, moneyScale, members);
        var result = new Dictionary<Guid, decimal>(distinct.Count);
        for (var i = 0; i < distinct.Count; i++)
            result[distinct[i].RugId] = split.Shares[i];
        return result;
    }

    /// <summary>مساحت یک تخته در پایهٔ ثبت.</summary>
    public static decimal PieceArea(AllocationMember member, AreaBasis basis)
        => basis == AreaBasis.Intake ? member.IntakePieceArea : member.EffectivePieceArea;

    /// <summary>
    /// کل زندهٔ «هر متر»/«هر تخته» روی این اعضا: (به ارز کارگاه، به ارز ثبت). همان قاعدهٔ دسته؛ برای ارز دیگر مبلغ تخته
    /// اول در ارز ثبت (دلار: ۲ رقم) و بعد برای هر تخته تبدیل می‌شود.
    /// </summary>
    public static (decimal Base, decimal Original) LiveTotal(
        CostBasis basis, decimal rate, CurrencyUnit currency, decimal? fxRate, AreaBasis areaBasis,
        IEnumerable<AllocationMember> members, CurrencyUnit tenantCurrency, int moneyScale)
    {
        ArgumentNullException.ThrowIfNull(members);
        var fx = Fx(currency, tenantCurrency, fxRate);
        var total = 0m;
        var original = 0m;
        foreach (var m in Distinct(members))
        {
            var area = PieceArea(m, areaBasis);
            total += RugCostMath.EntryLineAmount(basis, rate, area, m.Quantity, moneyScale, fx);
            original += OriginalPiece(basis, rate, area, moneyScale, fx) * m.Quantity;
        }

        return (total, original);
    }

    /// <summary>تقسیم یک ثبت روی اعضایش (فرش‌های سطل زباله پیش از این کنار گذاشته شده‌اند).</summary>
    public static AllocationResult Allocate(
        AllocationEntry entry, IReadOnlyList<AllocationMember> members, IReadOnlyList<AllocationPin> pins,
        CurrencyUnit tenantCurrency, int moneyScale)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(pins);

        var list = Distinct(members)
            .OrderBy(m => m.SerialSort is null ? 1 : 0)
            .ThenBy(m => m.SerialSort)
            .ThenBy(m => m.Serial, StringComparer.Ordinal)
            .ThenBy(m => m.RugId.ToString("D"), StringComparer.Ordinal)
            .ToList();

        var lines = list.Count;
        var pieces = list.Sum(m => m.Quantity);
        var area = list.Sum(m => PieceArea(m, entry.AreaBasis) * m.Quantity);
        var warnings = new List<AllocationWarning>();

        AllocationResult result = entry.IsLive
            ? AllocateLive(entry, list, tenantCurrency, moneyScale, lines, pieces, area, warnings)
            : AllocateTotal(entry, list, pins, moneyScale, lines, pieces, area, warnings);

        AddCommonWarnings(entry, list, result, tenantCurrency, moneyScale, warnings);
        return result with { Warnings = Order(warnings) };
    }

    // ═════════════════════════════════════════════════════════════════════
    // زنده
    // ═════════════════════════════════════════════════════════════════════

    private static AllocationResult AllocateLive(
        AllocationEntry entry, IReadOnlyList<AllocationMember> members, CurrencyUnit tenantCurrency, int moneyScale,
        int lines, int pieces, decimal area, List<AllocationWarning> warnings)
    {
        var rate = entry.Rate ?? 0m;
        var fx = Fx(entry.Currency, tenantCurrency, entry.FxRate);
        var shares = new List<AllocationShare>(members.Count);
        var total = 0m;
        var original = 0m;
        long weightSum = 0;
        foreach (var m in members)
        {
            var pieceArea = PieceArea(m, entry.AreaBasis);
            var piece = RugCostMath.EntryPieceAmount(entry.Basis, rate, pieceArea, moneyScale, fx);
            var amount = piece * m.Quantity;
            var weight = entry.Basis == CostBasis.PerM2 ? AreaWeight(pieceArea, m.Quantity) : m.Quantity;
            weightSum += weight;
            total += amount;
            original += OriginalPiece(entry.Basis, rate, pieceArea, moneyScale, fx) * m.Quantity;
            shares.Add(new AllocationShare(m.RugId, amount, weight, false, m.Quantity > 1 ? piece : null));
        }

        var state = AllocationState.Allocated;
        if (members.Count == 0)
        {
            state = AllocationState.Unallocated;
            warnings.Add(new AllocationWarning(entry.TargetDeleted ? CostWarnings.TargetDeleted : CostWarnings.EmptyTarget, 1));
        }

        return new AllocationResult(total, original, lines, pieces, area, weightSum, total, 0m, state, shares, [],
            entry.Basis == CostBasis.PerM2 ? Dtos.AreaUnit : Dtos.PieceUnit);
    }

    // ═════════════════════════════════════════════════════════════════════
    // «کل» و رسید منجمد
    // ═════════════════════════════════════════════════════════════════════

    private static AllocationResult AllocateTotal(
        AllocationEntry entry, IReadOnlyList<AllocationMember> members, IReadOnlyList<AllocationPin> pins, int moneyScale,
        int lines, int pieces, decimal area, List<AllocationWarning> warnings)
    {
        var total = entry.BaseAmount;
        var original = entry.Amount ?? total;
        var method = EffectiveMethod(entry);
        var memberIds = members.Select(m => m.RugId).ToHashSet();

        // مبلغ ثابتِ فرشی که عضو نیست نادیده گرفته می‌شود ولی می‌ماند (با برگشتن فرش دوباره اعمال می‌شود)
        var activePins = new Dictionary<Guid, decimal>();
        var dropped = new List<string>();
        foreach (var pin in pins.GroupBy(p => p.RugId).Select(g => g.First()))
        {
            if (memberIds.Contains(pin.RugId))
                activePins[pin.RugId] = pin.Amount;
            else
                dropped.Add(pin.Serial ?? string.Empty);
        }

        if (dropped.Count > 0)
            warnings.Add(new AllocationWarning(CostWarnings.PinDropped, dropped.Count,
                Serial: string.Join("، ", dropped.Where(s => s.Length > 0).OrderBy(s => s, StringComparer.Ordinal))));

        var shares = new List<AllocationShare>(members.Count);
        var pinned = 0m;
        foreach (var m in members)
        {
            if (!activePins.TryGetValue(m.RugId, out var pin)) continue;
            pinned += pin;
            shares.Add(new AllocationShare(m.RugId, pin, 0, true, PerPiece(pin, m.Quantity, moneyScale)));
        }

        var unpinned = members.Where(m => !activePins.ContainsKey(m.RugId)).ToList();
        var residual = total - pinned;
        var unit = WeightUnitOf(method);

        if (members.Count == 0)
        {
            warnings.Add(new AllocationWarning(entry.TargetDeleted ? CostWarnings.TargetDeleted : CostWarnings.EmptyTarget, 1));
            return new AllocationResult(total, original, 0, 0, 0m, 0, 0m, total, AllocationState.Unallocated, [], [], unit);
        }

        if (method == AllocationMethod.Manual)
        {
            // «خودم سهم هر فرش را می‌نویسم»: فقط مبلغ‌های ثابت؛ بقیهٔ اعضا سهم صفر دارند
            foreach (var m in unpinned)
                shares.Add(new AllocationShare(m.RugId, 0m, 0, false, null));
            var manualState = residual == 0m ? AllocationState.Allocated : AllocationState.Partial;
            return new AllocationResult(total, original, lines, pieces, area, 0, pinned, residual, manualState,
                Sort(shares, members), [], unit);
        }

        if (unpinned.Count == 0)
        {
            // همه مبلغ ثابت دارند: باقی‌مانده جایی برای رفتن ندارد (این‌طور است که حذف یا انتقال فرش‌های بی‌مبلغ ثابت تمام می‌شود)
            if (residual != 0m)
            {
                warnings.Add(new AllocationWarning(CostWarnings.PinsResidual, 1, Amount: residual));
                return new AllocationResult(total, original, lines, pieces, area, 0, pinned, residual, AllocationState.Partial,
                    Sort(shares, members), [], unit);
            }

            return new AllocationResult(total, original, lines, pieces, area, 0, pinned, 0m, AllocationState.Allocated,
                Sort(shares, members), [], unit);
        }

        var lrMembers = new List<LrMember>(unpinned.Count);
        var noDeclared = 0;
        var negative = 0;
        foreach (var m in unpinned)
        {
            var weight = Weight(method, m, entry.AreaBasis);
            if (method == AllocationMethod.ByDeclaredValue && m.LineUsd is null) noDeclared++;
            if (weight < 0) negative++;
            lrMembers.Add(new LrMember(m.RugId, weight, m.SerialSort));
        }

        var split = LargestRemainder.Split(residual, moneyScale, lrMembers);
        long weightSum = 0;
        for (var i = 0; i < unpinned.Count; i++)
        {
            var m = unpinned[i];
            var weight = split.ZeroWeightsUsedEqual ? 1 : Math.Max(0, lrMembers[i].Weight);
            weightSum += weight;
            shares.Add(new AllocationShare(m.RugId, split.Shares[i], weight, false, PerPiece(split.Shares[i], m.Quantity, moneyScale)));
        }

        if (noDeclared > 0)
            warnings.Add(new AllocationWarning(CostWarnings.NoDeclaredValue, noDeclared));
        if (split.ZeroWeightsUsedEqual)
            warnings.Add(new AllocationWarning(CostWarnings.ZeroWeights, unpinned.Count));
        if (split.ClampedNegative)
            warnings.Add(new AllocationWarning(CostWarnings.NegativeWeight, negative));

        return new AllocationResult(total, original, lines, pieces, area, weightSum, total, 0m, AllocationState.Allocated,
            Sort(shares, members), [], split.ZeroWeightsUsedEqual ? Dtos.EqualUnit : unit);
    }

    /// <summary>روش مؤثر: رسید متری به نسبت متراژ و رسید دانه‌ای به تعداد (D.3)؛ بقیه همان روش ثبت.</summary>
    public static AllocationMethod EffectiveMethod(AllocationEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!entry.Frozen) return entry.Method;
        return entry.Basis == CostBasis.PerM2 ? AllocationMethod.ByArea : AllocationMethod.ByCount;
    }

    /// <summary>وزن یک فرش در روش (همیشه صحیح؛ منفی را <see cref="LargestRemainder"/> صفر می‌کند).</summary>
    public static long Weight(AllocationMethod method, AllocationMember member, AreaBasis areaBasis)
    {
        ArgumentNullException.ThrowIfNull(member);
        return method switch
        {
            AllocationMethod.ByArea => AreaWeight(PieceArea(member, areaBasis), member.Quantity),
            AllocationMethod.ByCount => member.Quantity,
            AllocationMethod.Equal => 1,
            AllocationMethod.ByDeclaredValue => member.LineUsd is { } usd ? Cents(usd) : 0,
            AllocationMethod.ByPurchaseCost => Cents(member.PurchaseCost),
            _ => 0
        };
    }

    private static long AreaWeight(decimal pieceArea, int quantity)
        => checked((long)decimal.Truncate(Num.Round(pieceArea, 2) * AreaUnitsPerSquareMeter) * quantity);

    private static long Cents(decimal value) => checked((long)decimal.Truncate(Num.Round(value * CentsPerUnit, 0)));

    // ═════════════════════════════════════════════════════════════════════
    // هشدارهای مشترک
    // ═════════════════════════════════════════════════════════════════════

    private static void AddCommonWarnings(
        AllocationEntry entry, IReadOnlyList<AllocationMember> members, AllocationResult result,
        CurrencyUnit tenantCurrency, int moneyScale, List<AllocationWarning> warnings)
    {
        var stepDouble = members.Count(m => m.HasLinkedStep);
        if (stepDouble > 0)
            warnings.Add(new AllocationWarning(CostWarnings.StepDouble, stepDouble));

        if (entry.Group == CostGroup.Purchase)
        {
            var shareOf = result.Shares.ToDictionary(s => s.RugId, s => s.Amount);
            var purchaseDouble = members.Count(m => m.HasPurchaseLines && shareOf.TryGetValue(m.RugId, out var a) && a != 0m);
            if (purchaseDouble > 0)
                warnings.Add(new AllocationWarning(CostWarnings.PurchaseDouble, purchaseDouble));
        }

        var late = members.Count(m => m.InLateShipment);
        if (late > 0 || entry.LateWithoutMembers)
            warnings.Add(new AllocationWarning(CostWarnings.LateCost, Math.Max(1, late)));

        // رسید منجمد: با اندازه‌های امروز چقدر می‌شد؟ (پول رسید عوض نمی‌شود؛ فقط هشدار با فرق)
        if (entry.Frozen && entry.Rate is { } rate)
        {
            var (live, _) = LiveTotal(entry.Basis, rate, entry.Currency, entry.FxRate, entry.AreaBasis, members, tenantCurrency, moneyScale);
            if (live != entry.BaseAmount)
                warnings.Add(new AllocationWarning(CostWarnings.RateDrift, 1, Amount: live - entry.BaseAmount));
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // جزئیات
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// مبلغ یک تخته در ارز ثبت (برای «کل در ارز ثبت»): بی تبدیل همان قاعدهٔ دسته در MoneyScale؛ با تبدیل، «هر متر» همان
    /// ROUND(rate × مساحت، ۲) که پیش از تبدیل گرفته می‌شود و «هر تخته» خودِ نرخ (D.3).
    /// </summary>
    private static decimal OriginalPiece(CostBasis basis, decimal rate, decimal pieceArea, int moneyScale, decimal? fx)
        => (basis, fx) switch
        {
            (CostBasis.PerM2, null) => AreaMath.PieceMoney(rate, pieceArea, moneyScale),
            (CostBasis.PerM2, _) => AreaMath.PieceUsd(pieceArea, rate),
            (_, null) => AreaMath.PieceMoneyPerPiece(rate, moneyScale),
            _ => rate
        };

    private static decimal? PerPiece(decimal amount, int quantity, int moneyScale)
        => quantity > 1 ? Num.Round(amount / quantity, moneyScale) : null;

    private static IEnumerable<AllocationMember> Distinct(IEnumerable<AllocationMember> members)
        => members.GroupBy(m => m.RugId).Select(g => g.First());

    /// <summary>سهم‌ها به همان ترتیب اعضا (ترتیب شماره)، مستقل از اینکه مبلغ ثابت اول ساخته شد.</summary>
    private static List<AllocationShare> Sort(List<AllocationShare> shares, IReadOnlyList<AllocationMember> members)
    {
        var position = new Dictionary<Guid, int>(members.Count);
        for (var i = 0; i < members.Count; i++) position[members[i].RugId] = i;
        return shares.OrderBy(s => position.TryGetValue(s.RugId, out var p) ? p : int.MaxValue).ToList();
    }

    private static string WeightUnitOf(AllocationMethod method) => method switch
    {
        AllocationMethod.ByArea => Dtos.AreaUnit,
        AllocationMethod.ByCount => Dtos.PieceUnit,
        AllocationMethod.Equal => Dtos.EqualUnit,
        AllocationMethod.ByDeclaredValue => Dtos.UsdUnit,
        AllocationMethod.ByPurchaseCost => Dtos.PurchaseUnit,
        _ => Dtos.AmountUnit
    };

    private static IReadOnlyList<AllocationWarning> Order(List<AllocationWarning> warnings)
        => warnings.OrderBy(w => Array.IndexOf(CostWarnings.Order, w.Code)).ToList();

    /// <summary>نام واحدهای وزن (همان ثابت‌های <see cref="ShareWeightUnits"/> در DTOها).</summary>
    private static class Dtos
    {
        public const string AreaUnit = ShareWeightUnits.AreaHundredths;
        public const string PieceUnit = ShareWeightUnits.Pieces;
        public const string EqualUnit = ShareWeightUnits.Equal;
        public const string UsdUnit = ShareWeightUnits.UsdCents;
        public const string PurchaseUnit = ShareWeightUnits.PurchaseCents;
        public const string AmountUnit = ShareWeightUnits.Amount;
    }
}

/// <summary>کدهای هشدار تقسیم (D.8)، با ترتیب ثابت نمایش.</summary>
public static class CostWarnings
{
    public const string TargetDeleted = "W_TARGET_DELETED";
    public const string EmptyTarget = "W_EMPTY_TARGET";
    public const string PinsResidual = "W_PINS_RESIDUAL";
    public const string PinDropped = "W_PIN_DROPPED";
    public const string ZeroWeights = "W_ZERO_WEIGHTS";
    public const string NoDeclaredValue = "W_NO_DECLARED_VALUE";
    public const string NegativeWeight = "W_NEGATIVE_WEIGHT";
    public const string RateDrift = "W_RATE_DRIFT";
    public const string StepDouble = "W_STEP_DOUBLE";
    public const string PurchaseDouble = "W_PURCHASE_DOUBLE";
    public const string LateCost = "W_LATE_COST";

    /// <summary>ترتیب نمایش: اول آن‌هایی که پول را بی‌صاحب گذاشته‌اند.</summary>
    public static readonly string[] Order =
    [
        TargetDeleted, EmptyTarget, PinsResidual, PinDropped, ZeroWeights, NoDeclaredValue, NegativeWeight, RateDrift,
        StepDouble, PurchaseDouble, LateCost
    ];
}
