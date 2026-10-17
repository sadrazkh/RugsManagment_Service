using System.Numerics;

namespace RugsManagment.Domain.Costing;

/// <summary>یک سهم‌بَر در تقسیم: شناسه، وزن و کلید مرتب‌سازیِ شماره (برای شکستن تساوی).</summary>
/// <param name="Id">شناسهٔ فرش (یا هر سهم‌بری؛ در برش دسته شناسهٔ ساختگی).</param>
/// <param name="Weight">وزن صحیح (صدم متر، سنت، تعداد، …)؛ منفی به صفر بریده می‌شود.</param>
/// <param name="SerialSort">کلید شماره (<c>Rug.SerialSort</c>)؛ null آخر می‌آید.</param>
public readonly record struct LrMember(Guid Id, long Weight, long? SerialSort = null);

/// <summary>نتیجهٔ تقسیم: سهم هر عضو به همان ترتیب ورودی، و اینکه کدام قاعدهٔ کمکی به کار رفت.</summary>
/// <param name="Shares">سهم‌ها، هم‌ترتیب با اعضای ورودی؛ جمعشان دقیقاً برابر کل است.</param>
/// <param name="ClampedNegative">دست‌کم یک وزن منفی بود و صفر شد (هشدار <c>W_NEGATIVE_WEIGHT</c>).</param>
/// <param name="ZeroWeightsUsedEqual">جمع وزن‌ها صفر بود و تقسیم مساوی شد (هشدار <c>W_ZERO_WEIGHTS</c>).</param>
public sealed record LrResult(IReadOnlyList<decimal> Shares, bool ClampedNegative, bool ZeroWeightsUsedEqual);

/// <summary>
/// تقسیم یک مبلغ بین چند سهم‌بر به روش «بزرگ‌ترین باقی‌مانده» (spec D.3) — تنها پیاده‌سازی در کد.
///
/// چرا این روش: جمع سهم‌ها باید دقیقاً همان مبلغ کل باشد (نه یک تومان کم یا زیاد) و هر سهم حداکثر یک واحد با
/// سهم دقیقش فاصله داشته باشد؛ گرد کردن جداگانهٔ هر سهم این را تضمین نمی‌کند. مراحل:
/// <list type="number">
/// <item>A = |T| بر حسب واحد 10^−scale (T باید در همان scale گرد شده باشد).</item>
/// <item>با حساب صحیح دقیق: floorᵢ = (A·wᵢ) div W و remᵢ = (A·wᵢ) mod W.</item>
/// <item>R = A − Σfloor واحدِ باقی‌مانده به R عضو با بزرگ‌ترین remᵢ می‌رسد.</item>
/// <item>تساوی: وزن بزرگ‌تر، بعد <c>SerialSort</c> کوچک‌تر (null آخر)، بعد شناسه به‌صورت رشتهٔ کوچک «D» با
/// مقایسهٔ ordinal (همان ترتیب بایتی RFC 4122 در C#، پستگرس و TypeScript) — هرگز <c>Guid.CompareTo</c>.</item>
/// <item>علامت T دوباره اعمال می‌شود؛ پس −۱۰۰ بین سه سهم مساوی −۳۴ / −۳۳ / −۳۳ است.</item>
/// </list>
/// وزن منفی صفر می‌شود؛ اگر جمع وزن‌ها صفر باشد هر عضو وزن ۱ می‌گیرد (مساوی). نتیجه به ترتیب ورودی بستگی ندارد.
/// تقسیم‌کنندهٔ هزینه‌ها (فاز هزینه)، برش دسته (D.9) و سهم درآمد صادرات همه از همین استفاده می‌کنند.
/// </summary>
public static class LargestRemainder
{
    /// <summary>
    /// <paramref name="total"/> را بین <paramref name="members"/> تقسیم می‌کند؛ هر سهم در <paramref name="scale"/>
    /// رقم اعشار. اعضای خالی یا مبلغی با رقم اعشار بیش از scale → <see cref="ArgumentException"/>.
    /// </summary>
    public static LrResult Split(decimal total, int scale, IReadOnlyList<LrMember> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentOutOfRangeException.ThrowIfNegative(scale);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(scale, 6);
        if (members.Count == 0)
            throw new ArgumentException("تقسیم بدون سهم‌بر معنا ندارد.", nameof(members));

        // شناسهٔ تکراری قاعدهٔ شکستن تساوی را نامعین می‌کرد (نتیجه به ترتیب ورودی بسته می‌شد)
        var ids = new HashSet<Guid>();
        foreach (var member in members)
        {
            if (!ids.Add(member.Id))
                throw new ArgumentException($"شناسهٔ {member.Id} دو بار آمده است.", nameof(members));
        }

        var unit = Pow10(scale);
        var scaled = Math.Abs(total) * unit;
        if (decimal.Truncate(scaled) != scaled)
            throw new ArgumentException($"مبلغ {total} بیش از {scale} رقم اعشار دارد؛ اول آن را گرد کنید.", nameof(total));

        var a = new BigInteger(scaled);
        var weights = new BigInteger[members.Count];
        var clamped = false;
        var sum = BigInteger.Zero;
        for (var i = 0; i < members.Count; i++)
        {
            var w = members[i].Weight;
            if (w < 0)
            {
                clamped = true;
                w = 0;
            }

            weights[i] = w;
            sum += w;
        }

        var equal = sum.IsZero;
        if (equal)
        {
            for (var i = 0; i < weights.Length; i++) weights[i] = BigInteger.One;
            sum = weights.Length;
        }

        var floors = new BigInteger[members.Count];
        var remainders = new BigInteger[members.Count];
        var allotted = BigInteger.Zero;
        for (var i = 0; i < members.Count; i++)
        {
            floors[i] = BigInteger.DivRem(a * weights[i], sum, out var rem);
            remainders[i] = rem;
            allotted += floors[i];
        }

        var leftover = (int)(a - allotted); // همیشه کمتر از تعداد اعضا
        if (leftover > 0)
        {
            var order = Enumerable.Range(0, members.Count).ToArray();
            Array.Sort(order, (x, y) => CompareForExtraUnit(x, y, remainders, weights, members));
            for (var k = 0; k < leftover; k++)
                floors[order[k]] += BigInteger.One;
        }

        var sign = total < 0 ? -1m : 1m;
        var shares = new decimal[members.Count];
        for (var i = 0; i < members.Count; i++)
            shares[i] = sign * (decimal)floors[i] / unit;

        return new LrResult(shares, clamped, equal);
    }

    /// <summary>ترتیب گرفتن واحد اضافه: باقی‌ماندهٔ بزرگ‌تر، وزن بزرگ‌تر، شمارهٔ کوچک‌تر (null آخر)، شناسهٔ ordinal.</summary>
    private static int CompareForExtraUnit(
        int x, int y, BigInteger[] remainders, BigInteger[] weights, IReadOnlyList<LrMember> members)
    {
        var byRemainder = remainders[y].CompareTo(remainders[x]);
        if (byRemainder != 0) return byRemainder;

        var byWeight = weights[y].CompareTo(weights[x]);
        if (byWeight != 0) return byWeight;

        var sx = members[x].SerialSort;
        var sy = members[y].SerialSort;
        if (sx != sy)
        {
            if (sx is null) return 1;
            if (sy is null) return -1;
            return sx.Value.CompareTo(sy.Value);
        }

        return StringComparer.Ordinal.Compare(members[x].Id.ToString("D"), members[y].Id.ToString("D"));
    }

    private static decimal Pow10(int scale)
    {
        var result = 1m;
        for (var i = 0; i < scale; i++) result *= 10m;
        return result;
    }
}
