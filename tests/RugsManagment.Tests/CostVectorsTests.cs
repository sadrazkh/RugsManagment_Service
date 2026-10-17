using System.Globalization;
using System.Text.Json;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Costing;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Tests;

/// <summary>
/// بردارهای مشترک قاعده‌های پولی (<c>tests/fixtures/math-vectors.json</c>، آرایه‌های <c>costVectors</c>،
/// <c>serialVectors</c> و <c>serialQtyVectors</c>) روی همتای C#: <see cref="RugCostMath"/>،
/// <see cref="LargestRemainder"/> و <see cref="SerialNumber"/> (spec D.3، D.7، G.4، L.1).
///
/// چرا از فایل: همین بردارها را آزمون برابری SQL (CostSql) و در صورت نیاز vitest هم می‌خوانند؛ عددی که این‌جا درست
/// است باید در هر سه جا همان باشد. هر بردار یک مورد آزمون جداست تا شکست، شناسهٔ همان ردیف را نشان دهد.
/// </summary>
public class CostVectorsTests
{
    private static decimal D(string value) => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);

    private static decimal Str(JsonElement v, string name) => D(v.GetProperty(name).GetString()!);

    private static decimal? OptStr(JsonElement v, string name)
        => v.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? D(p.GetString()!) : null;

    private static JsonElement Find(string array, string id)
    {
        // JsonDocument باید زنده بماند؛ Clone عنصر را مستقل از سند می‌کند
        using var document = Fixtures.MathVectors();
        return document.RootElement.GetProperty(array).EnumerateArray()
            .Single(e => e.GetProperty("id").GetString() == id)
            .Clone();
    }

    private static TheoryData<string> Ids(string array)
    {
        var data = new TheoryData<string>();
        using var document = Fixtures.MathVectors();
        foreach (var vector in document.RootElement.GetProperty(array).EnumerateArray())
            data.Add(vector.GetProperty("id").GetString()!);
        return data;
    }

    public static TheoryData<string> CostVectorIds() => Ids("costVectors");

    public static TheoryData<string> SerialVectorIds() => Ids("serialVectors");

    public static TheoryData<string> SerialQtyVectorIds() => Ids("serialQtyVectors");

    [Fact]
    public void TheFileCarriesEveryP2Family()
    {
        using var document = Fixtures.MathVectors();
        var ops = document.RootElement.GetProperty("costVectors").EnumerateArray()
            .Select(v => v.GetProperty("op").GetString())
            .ToHashSet();
        foreach (var op in new[] { "purchaseLines", "entryPiece", "baseAmount", "stepPrice", "effectiveCost", "stepCost", "lr", "compose" })
            Assert.Contains(op, ops);

        // هر مدل قیمت‌گذاری مرحله دست‌کم یک بردار با تعداد ۵۰ دارد (K.2/P2)
        var models = document.RootElement.GetProperty("costVectors").EnumerateArray()
            .Where(v => v.GetProperty("op").GetString() == "stepPrice" && v.GetProperty("qty").GetInt32() == 50)
            .Select(v => Enum.Parse<StepPricingModel>(v.GetProperty("model").GetString()!))
            .ToHashSet();
        Assert.Equal(Enum.GetValues<StepPricingModel>().ToHashSet(), models);
    }

    [Theory]
    [MemberData(nameof(CostVectorIds))]
    public void CostVector(string id)
    {
        var v = Find("costVectors", id);
        Assert.Equal("money", v.GetProperty("kind").GetString());
        var scale = v.GetProperty("scale").GetInt32();

        switch (v.GetProperty("op").GetString())
        {
            case "purchaseLines":
            {
                var lines = v.GetProperty("lines").EnumerateArray()
                    .Select(l => new PurchaseLineInput(Enum.Parse<CostBasis>(l.GetProperty("basis").GetString()!), Str(l, "rate")))
                    .ToList();
                var pieceArea = AreaMath.PieceArea(Str(v, "intakeLength"), Str(v, "intakeWidth"));
                var amounts = RugCostMath.PurchaseLineAmounts(lines, pieceArea, v.GetProperty("qty").GetInt32(), scale);
                var expected = v.GetProperty("amounts").EnumerateArray().Select(a => D(a.GetString()!)).ToList();
                Assert.Equal(expected, amounts);
                Assert.Equal(Str(v, "total"), amounts.Sum());
                break;
            }

            case "entryPiece":
            {
                var basis = Enum.Parse<CostBasis>(v.GetProperty("basis").GetString()!);
                var piece = RugCostMath.EntryPieceAmount(basis, Str(v, "rate"), Str(v, "pieceArea"), scale, OptStr(v, "fx"));
                Assert.Equal(Str(v, "piece"), piece);
                Assert.Equal(Str(v, "line"), RugCostMath.EntryLineAmount(
                    basis, Str(v, "rate"), Str(v, "pieceArea"), v.GetProperty("qty").GetInt32(), scale, OptStr(v, "fx")));
                break;
            }

            case "baseAmount":
                Assert.Equal(Str(v, "expected"), RugCostMath.BaseAmount(Str(v, "amount"), OptStr(v, "fx"), scale));
                break;

            case "stepPrice":
            {
                var model = Enum.Parse<StepPricingModel>(v.GetProperty("model").GetString()!);
                IReadOnlyList<StepPricingItem>? items = v.TryGetProperty("items", out var itemsJson)
                    ? itemsJson.EnumerateArray()
                        .Select(i => new StepPricingItem(Enum.Parse<StepPricingModel>(i.GetProperty("model").GetString()!), Str(i, "rate")))
                        .ToList()
                    : null;
                var qty = v.GetProperty("qty").GetInt32();
                Assert.Equal(Str(v, "piece"),
                    RugCostMath.StepPiecePrice(model, Str(v, "rate"), Str(v, "length"), Str(v, "width"), scale, items));
                Assert.Equal(Str(v, "line"),
                    RugCostMath.StepLinePrice(model, Str(v, "rate"), Str(v, "length"), Str(v, "width"), qty, scale, items));
                break;
            }

            case "effectiveCost":
                Assert.Equal(Str(v, "expected"),
                    RugCostMath.StepEffectiveCost(OptStr(v, "calculated"), OptStr(v, "manual"), OptStr(v, "adjustment")));
                break;

            case "stepCost":
            {
                var steps = v.GetProperty("steps").EnumerateArray()
                    .Select(s => new StepCostInput(
                        Enum.Parse<WorkflowStepStatus>(s.GetProperty("status").GetString()!),
                        OptStr(s, "calculated"), OptStr(s, "manual"), OptStr(s, "adjustment")))
                    .ToList();
                Assert.Equal(Str(v, "expected"), RugCostMath.StepCost(steps));
                break;
            }

            case "lr":
            {
                var members = ReadMembers(v);
                var expected = v.GetProperty("shares").EnumerateArray().Select(s => D(s.GetString()!)).ToList();
                var result = LargestRemainder.Split(Str(v, "total"), scale, members);
                Assert.Equal(expected, result.Shares);
                Assert.Equal(Str(v, "total"), result.Shares.Sum());
                Assert.Equal(v.TryGetProperty("zeroWeightsUsedEqual", out var z) && z.GetBoolean(), result.ZeroWeightsUsedEqual);
                Assert.Equal(v.TryGetProperty("clampedNegative", out var c) && c.GetBoolean(), result.ClampedNegative);

                // ترتیب ورودی بی‌اثر است: همان اعضا وارونه، همان سهم هر عضو
                var reversed = LargestRemainder.Split(Str(v, "total"), scale, members.AsEnumerable().Reverse().ToList());
                Assert.Equal(expected, reversed.Shares.Reverse().ToList());
                break;
            }

            case "compose":
            {
                var breakdown = RugCostMath.Compose(
                    Str(v, "purchaseLines"), Str(v, "purchaseAllocations"), Str(v, "stepCost"), Str(v, "expenseAllocations"),
                    Str(v, "totalAreaM2"), v.GetProperty("qty").GetInt32(), scale);
                Assert.Equal(Str(v, "purchaseCost"), breakdown.PurchaseCost);
                Assert.Equal(Str(v, "total"), breakdown.Total);
                Assert.Equal(OptStr(v, "costPerM2"), breakdown.CostPerM2);
                Assert.Equal(Str(v, "costPerPiece"), breakdown.CostPerPiece);
                Assert.Equal(breakdown.PurchaseCost + breakdown.StepCost + breakdown.ExpenseCost, breakdown.Total);
                break;
            }

            default:
                Assert.Fail($"عمل ناشناخته در بردار {id}");
                break;
        }
    }

    private static List<LrMember> ReadMembers(JsonElement v)
        => v.GetProperty("members").EnumerateArray()
            .Select(m => new LrMember(
                Guid.Parse(m.GetProperty("id").GetString()!),
                m.GetProperty("weight").GetInt64(),
                m.GetProperty("serialSort").ValueKind == JsonValueKind.Null ? null : m.GetProperty("serialSort").GetInt64()))
            .ToList();

    // ═════════════════════════════════════════════════════════════════════
    // شماره‌ها
    // ═════════════════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(SerialVectorIds))]
    public void SerialVector(string id)
    {
        var v = Find("serialVectors", id);
        var input = v.GetProperty("input").GetString();

        var ok = SerialNumber.TryParse(input, out var serial, out var error);

        if (v.TryGetProperty("error", out var expectedError))
        {
            Assert.False(ok);
            Assert.Null(serial);
            Assert.Equal(expectedError.GetString(), error!.Code);
            var thrown = Assert.Throws<DomainValidationException>(() => SerialNumber.Parse(input));
            Assert.Equal(error.Message, thrown.Message);
            Assert.True(thrown.Errors.ContainsKey(SerialNumber.Field));
            return;
        }

        Assert.True(ok, error?.Message);
        Assert.Null(error);
        if (v.GetProperty("display").ValueKind == JsonValueKind.Null)
        {
            Assert.Null(serial);
            return;
        }

        Assert.NotNull(serial);
        Assert.Equal(v.GetProperty("display").GetString(), serial.Display);
        Assert.Equal(ReadInt(v, "from"), serial.From);
        Assert.Equal(ReadInt(v, "to"), serial.To);
        Assert.Equal(serial.From, (int?)serial.SortKey);
        if (v.TryGetProperty("count", out var count))
            Assert.Equal(count.GetInt32(), serial.Count);
        if (v.TryGetProperty("next", out var next))
            Assert.Equal(next.GetInt32(), serial.NextAfter);

        // شکل نمایشی دوباره خوانده شود همان می‌شود (پایدار)
        Assert.Equal(serial, SerialNumber.Parse(serial.Display));
    }

    [Theory]
    [MemberData(nameof(SerialQtyVectorIds))]
    public void SerialQuantityVector(string id)
    {
        var v = Find("serialQtyVectors", id);
        var serial = SerialNumber.Parse(v.GetProperty("input").GetString())!;
        var qty = v.GetProperty("qty").GetInt32();

        Assert.Equal(v.GetProperty("covers").GetBoolean(), serial.Covers(qty));
        if (serial.Covers(qty))
        {
            serial.EnsureCovers(qty);
        }
        else
        {
            var thrown = Assert.Throws<DomainValidationException>(() => serial.EnsureCovers(qty));
            Assert.Equal(serial.QuantityError(qty).Message, thrown.Message);
            Assert.Equal(SerialNumber.QuantityErrorCode, serial.QuantityError(qty).Code);
        }
    }

    private static int? ReadInt(JsonElement v, string name)
        => v.GetProperty(name).ValueKind == JsonValueKind.Null ? null : v.GetProperty(name).GetInt32();
}
