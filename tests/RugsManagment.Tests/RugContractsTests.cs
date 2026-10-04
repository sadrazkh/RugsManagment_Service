using System.Reflection;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Tests;

/// <summary>
/// قراردادهای فرش در فاز ۲ (spec I.3): ترتیب عضوهای افزوده‌شدهٔ recordهای موقعیتی، فیلترهای <see cref="RugQuery"/> و
/// پیشنهاد مقدار. این‌ها پیش از دو بستهٔ موازی (پاس سازگاری و برنامهٔ فرش) قفل می‌شوند؛ اگر کسی عضوی را جابه‌جا کند یا
/// فیلتری بیفزاید و در «فیلتر فعال است؟» نیاورد، همین‌جا می‌شکند.
/// </summary>
public class RugContractsTests
{
    // ═════════════════════════════════════════════════════════════════════
    // ترتیب عضوهای افزوده (هرگز جابه‌جا نشوند؛ فقط به انتها)
    // ═════════════════════════════════════════════════════════════════════

    private static string[] Tail(Type record, int count)
    {
        var parameters = record.GetConstructors()
            .Single(c => c.GetParameters().Length > 1 && c.GetParameters()[0].ParameterType != record)
            .GetParameters();
        return parameters.Skip(parameters.Length - count).Select(p => p.Name!).ToArray();
    }

    [Fact]
    public void RugDto_AppendsTheI3MembersInOrder_AfterTheP1Shape()
    {
        string[] i3 =
        [
            "SerialNo", "Quantity", "IntakeLength", "IntakeWidth", "FinalLength", "FinalWidth", "TotalAreaM2", "Location",
            "ShipmentId", "ShipmentNumber", "BaleId", "BaleNumber", "UsdPerM2", "LineUsd", "Version", "PurchaseLines",
            "IntakeAreaM2", "FinalMeasuredAt", "BatchNumber", "SellerName"
        ];
        Assert.Equal(i3, Tail(typeof(RugDto), i3.Length));

        // شکل پیش از فاز ۲ دست نخورده: ۲۴ عضو اول با Images تمام می‌شوند
        var all = typeof(RugDto).GetConstructors().Single(c => c.GetParameters().Length > 1).GetParameters();
        Assert.Equal("Images", all[23].Name);
        Assert.Equal(24 + i3.Length, all.Length);
    }

    [Fact]
    public void RugListItemDto_AppendsTheI3MembersInOrder()
    {
        string[] i3 =
        [
            "SerialNo", "Quantity", "TotalAreaM2", "CostComputed", "CostPerM2", "Location", "BaleNumber", "ShipmentNumber",
            "Version"
        ];
        Assert.Equal(i3, Tail(typeof(RugListItemDto), i3.Length));
        Assert.Equal(typeof(decimal?), typeof(RugListItemDto).GetProperty(nameof(RugListItemDto.TotalInvestment))!.PropertyType);
    }

    [Fact]
    public void RugCostSummaryDto_AppendsTheI3MembersInOrder_KeepingTheOldNames()
    {
        string[] i3 = ["StepCost", "ExpenseCost", "CostPerM2", "CostPerPiece", "ExportRevenueShare", "Computed", "CostAtSale"];
        Assert.Equal(i3, Tail(typeof(RugCostSummaryDto), i3.Length));
        Assert.Equal(["TotalProcessCost", "PurchaseCost", "TotalInvestment"],
            typeof(RugCostSummaryDto).GetConstructors().Single(c => c.GetParameters().Length > 1)
                .GetParameters().Take(3).Select(p => p.Name));
    }

    [Fact]
    public void UpdateRugRequest_AppendsVersionAndValues()
    {
        Assert.Equal(["MetadataJson", "Version", "Values"], Tail(typeof(UpdateRugRequest), 3));
        Assert.Equal(["MetadataJson", "Values"], Tail(typeof(CreateRugRequest), 2));
    }

    [Fact]
    public void NewRecords_MatchTheI3Outlines()
    {
        Assert.Equal(["Id", "CategoryId", "Label", "Basis", "Rate", "Amount", "SortOrder", "Notes"],
            Tail(typeof(PurchaseLineDto), 8));
        Assert.Equal(["Purchase", "Steps", "Allocations", "Summary"], Tail(typeof(RugCostBreakdownDto), 4));
        Assert.Equal(
            ["EntryId", "EntryNumber", "Category", "Group", "Date", "Amount", "WeightUnits", "TotalWeightUnits", "Method", "IsPinned", "EntryBase"],
            Tail(typeof(AllocationLineDto), 11));
        Assert.Equal(["Length", "Width", "SameAsIntake", "Clear", "Version"], Tail(typeof(SetFinalSizeRequest), 5));
        Assert.Equal(["Quantity", "SerialFrom", "SerialTo", "Version"], Tail(typeof(SplitRugRequest), 4));
        Assert.Equal(["Rug", "Location", "MatchType"], Tail(typeof(RugBySerialDto), 3));
    }

    [Fact]
    public void RugSortBy_KeepsItsValues_AndAppendsSerial()
    {
        Assert.Equal(
            [0, 1, 2, 3, 4, 5, 6, 7],
            new[]
            {
                RugSortBy.CreatedAt, RugSortBy.Sku, RugSortBy.Title, RugSortBy.Area, RugSortBy.TotalCost,
                RugSortBy.Status, RugSortBy.CurrentStep, RugSortBy.Serial
            }.Select(v => (int)v));
    }

    // ═════════════════════════════════════════════════════════════════════
    // RugQuery
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>عضوهایی که فیلتر نیستند (مرتب‌سازی و صفحه).</summary>
    private static readonly HashSet<string> NotFilters =
        [nameof(RugQuery.SortBy), nameof(RugQuery.Descending), nameof(RugQuery.Page), nameof(RugQuery.PageSize)];

    /// <summary>همهٔ فیلترهای قابل bind (ویژگی عمومی با setter)؛ عضو تازه خودبه‌خود این‌جا می‌آید.</summary>
    public static TheoryData<string> FilterProperties()
    {
        var data = new TheoryData<string>();
        foreach (var name in FilterNames())
            data.Add(name);
        return data;
    }

    private static IEnumerable<string> FilterNames()
        => typeof(RugQuery).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is not null && !NotFilters.Contains(p.Name))
            .Select(p => p.Name);

    /// <summary>یک مقدار غیرپیش‌فرض برای هر نوع فیلتر.</summary>
    internal static object SampleValue(Type type) => (Nullable.GetUnderlyingType(type) ?? type) switch
    {
        var t when t == typeof(string) => "3778",
        var t when t == typeof(bool) => true,
        var t when t == typeof(Guid) => Guid.Parse("0b6f3a52-6c1e-4c39-9a51-2f0f5a1f0c11"),
        var t when t == typeof(DateTimeOffset) => new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero),
        var t when t == typeof(RugStatus) => RugStatus.ReadyForSale,
        var t when t == typeof(RugLocation) => RugLocation.Sold,
        var t => throw new InvalidOperationException($"فیلتر {t.Name} نمونه ندارد؛ این آزمون را برای نوع تازه کامل کنید.")
    };

    [Fact]
    public void RugQuery_DeclaresEveryP2Filter()
    {
        var names = FilterNames().ToHashSet();
        var p2 = new HashSet<string>
        {
            nameof(RugQuery.ShipmentId), nameof(RugQuery.BaleId), nameof(RugQuery.NotBaled), nameof(RugQuery.Location),
            nameof(RugQuery.Serial), nameof(RugQuery.WithoutBatch)
        };
        Assert.Superset(p2, names);
        Assert.Equal(typeof(RugLocation?), typeof(RugQuery).GetProperty(nameof(RugQuery.Location))!.PropertyType);
    }

    [Fact]
    public void RugQuery_Default_HasNoActiveFilters()
        => Assert.False(new RugQuery().HasActiveFilters);

    [Theory]
    [MemberData(nameof(FilterProperties))]
    public void RugQuery_EachFilter_TurnsOnHasActiveFilters(string property)
    {
        var query = new RugQuery();
        var info = typeof(RugQuery).GetProperty(property)!;
        info.SetValue(query, SampleValue(info.PropertyType));
        Assert.True(query.HasActiveFilters, $"{property} در HasActiveFilters نیامده است.");
    }

    [Fact]
    public void RugQuery_Sanitized_TrimsTheSerial_AndDropsABlankOne()
    {
        Assert.Equal("3911 - 3960", new RugQuery { Serial = "  3911 - 3960 " }.Sanitized().Serial);
        Assert.Null(new RugQuery { Serial = "   " }.Sanitized().Serial);
        Assert.False(new RugQuery { Serial = "   " }.Sanitized().HasActiveFilters);
    }

    // ═════════════════════════════════════════════════════════════════════
    // پیشنهاد مقدار
    // ═════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("rug.title", RugValueColumn.Title)]
    [InlineData("rug.origin", RugValueColumn.Origin)]
    [InlineData(" rug.pattern ", RugValueColumn.Pattern)]
    [InlineData("rug.material", RugValueColumn.Material)]
    [InlineData("batch.seller", RugValueColumn.Seller)]
    public void RugValuesQuery_MapsTheBuiltInFieldKeys(string key, RugValueColumn column)
    {
        Assert.True(RugValuesQuery.TryFromFieldKey(key, "  کا ", out var query));
        Assert.Equal(column, query.Column);
        Assert.Null(query.CustomKey);
        Assert.Equal("کا", query.Search);
        Assert.Equal(RugValuesQuery.DefaultTake, query.Take);
    }

    [Theory]
    [InlineData("rug.custom.bg_color", "bg_color")]
    [InlineData("rug.custom.رنگ_متن", "رنگ_متن")]
    [InlineData("rug.custom.c2", "c2")]
    public void RugValuesQuery_MapsCustomFieldKeys(string key, string customKey)
    {
        Assert.True(RugValuesQuery.TryFromFieldKey(key, null, out var query));
        Assert.Equal(RugValueColumn.Custom, query.Column);
        Assert.Equal(customKey, query.CustomKey);
        Assert.Null(query.Search);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("rug.serial")]
    [InlineData("purchase.price")]
    [InlineData("rug.custom.")]
    [InlineData("rug.custom.___")]
    [InlineData("rug.custom.a b")]
    [InlineData("rug.custom.a'b")]
    [InlineData("rug.custom.a.b")]
    [InlineData("RUG.ORIGIN")]
    public void RugValuesQuery_RejectsUnknownOrUnsafeKeys(string? key)
        => Assert.False(RugValuesQuery.TryFromFieldKey(key, null, out _));

    [Fact]
    public void RugValuesQuery_Sanitized_ClampsTake_AndDropsAStrayCustomKey()
    {
        Assert.Equal(RugValuesQuery.DefaultTake, new RugValuesQuery { Column = RugValueColumn.Origin, Take = 0 }.Sanitized().Take);
        Assert.Equal(RugValuesQuery.MaxTake, new RugValuesQuery { Column = RugValueColumn.Origin, Take = 500 }.Sanitized().Take);
        Assert.Equal(7, new RugValuesQuery { Column = RugValueColumn.Origin, Take = 7 }.Sanitized().Take);
        Assert.Null(new RugValuesQuery { Column = RugValueColumn.Origin, CustomKey = "x" }.Sanitized().CustomKey);
        Assert.Null(new RugValuesQuery { Column = RugValueColumn.Origin, Search = " " }.Sanitized().Search);
    }

    [Fact]
    public void SerialMatchType_IsTheWireContract()
    {
        Assert.Equal("exact", SerialMatchType.Exact);
        Assert.Equal("inRange", SerialMatchType.InRange);
    }
}
