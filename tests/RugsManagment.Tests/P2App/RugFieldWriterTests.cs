using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Rugs;
using RugsManagment.Application.Fields;
using RugsManagment.Application.Seed;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using static RugsManagment.Tests.P2App.P2Data;

namespace RugsManagment.Tests.P2App;

/// <summary>
/// نویسندهٔ فیلدهای فرش (spec G.2، G.6، I.3؛ L.1 «PurchaseMath — Grid purchase columns»): شماره و تعداد با هم، اندازهٔ اول و
/// نهایی، فیلدهای سفارشی، خانه‌های خرید، پول فقط Business، همه‌یا‌هیچ، و قاعدهٔ قدیمی «قیمت خرید» (LM-02).
/// </summary>
public class RugFieldWriterTests
{
    private readonly P2MemoryRepository<CustomFieldDefinition> _fields = new(
        new CustomFieldDefinition { TenantId = TenantId, Key = "bg_color", Label = "رنگ متن", FieldType = CustomFieldType.Text },
        new CustomFieldDefinition
        {
            TenantId = TenantId, Key = "grade", Label = "درجه", FieldType = CustomFieldType.Select, OptionsJson = """["اعلا","معمولی"]"""
        },
        new CustomFieldDefinition { TenantId = TenantId, Key = "old", Label = "قدیمی", FieldType = CustomFieldType.Text, IsActive = false });

    private readonly P2MemoryRepository<CostCategory> _categories = PurchaseCategories();
    private readonly P2MemoryRepository<RugPurchaseLine> _lines = new();

    private RugFieldWriter Writer(bool business = true) => new(_fields, _categories, _lines, business ? P2Money.Business : P2Money.Operator);

    private static Rug NewRug() => new() { TenantId = TenantId, Sku = "RUG-202610-0009" };

    private CostCategory Category(string seedKey) => _categories.Items.Values.Single(c => c.SeedKey == seedKey);

    // ── شماره و تعداد ───────────────────────────────────────────────────

    [Fact]
    public async Task NewLot_SerialAndQuantity_AndLotArea()
    {
        var rug = NewRug();
        var result = await Writer().ApplyAsync(rug, Values(
            (RugFieldKeys.Serial, "3911 - 3960"), (RugFieldKeys.Quantity, 50),
            (RugFieldKeys.IntakeLength, 0.40m), (RugFieldKeys.IntakeWidth, 0.40m)), Context(isNew: true));

        Assert.Equal("3911 - 3960", rug.SerialNo);
        Assert.Equal(3911, rug.SerialFrom);
        Assert.Equal(3960, rug.SerialTo);
        Assert.Equal(50, rug.Quantity);
        Assert.Equal(0.16m, rug.AreaM2);
        Assert.Equal(8.00m, rug.TotalAreaM2);
        Assert.False(result.NeedsAutoSerial);
        Assert.Contains(RugFieldKeys.Serial, result.ChangedKeys);
    }

    [Theory]
    [InlineData("۳۷۷۸", "3778")]
    [InlineData("3911–3960", "3911 - 3960")]
    [InlineData("3911 تا 3960", "3911 - 3960")]
    [InlineData("A-12", "A-12")]
    public async Task Serial_TypedForms_AreNormalised(string typed, string stored)
    {
        var rug = Rug();
        await Writer().ApplyAsync(rug, Values((RugFieldKeys.Serial, typed)), Context(isNew: false));
        Assert.Equal(stored, rug.SerialNo);
    }

    [Fact]
    public async Task Serial_AsAJsonNumber_IsAccepted()
    {
        var rug = Rug();
        await Writer().ApplyAsync(rug, Values((RugFieldKeys.Serial, 3778)), Context(isNew: false));
        Assert.Equal("3778", rug.SerialNo);
    }

    [Fact]
    public async Task Serial_ShorterThanQuantity_Is400_OnTheSerialField()
    {
        var rug = NewRug();
        var error = await Assert.ThrowsAsync<AppValidationException>(() => Writer().ApplyAsync(rug, Values(
            (RugFieldKeys.Serial, "3911"), (RugFieldKeys.Quantity, 50),
            (RugFieldKeys.IntakeLength, 0.4m), (RugFieldKeys.IntakeWidth, 0.4m)), Context(isNew: true)));

        Assert.Contains("۵۰", error.Errors[RugFieldKeys.Serial][0]);
        Assert.Null(rug.SerialNo);
        Assert.Equal(1, rug.Quantity);
    }

    [Fact]
    public async Task Serial_Reversed_UsesTheDomainMessage()
    {
        var error = await Assert.ThrowsAsync<AppValidationException>(() =>
            Writer().ApplyAsync(Rug(), Values((RugFieldKeys.Serial, "3960-3911")), Context(isNew: false)));
        Assert.Equal(SerialNumber.ReversedErrorMessage, error.Errors[RugFieldKeys.Serial][0]);
    }

    [Fact]
    public async Task Quantity_Alone_MustStillBeCoveredByTheSerial()
    {
        var rug = Rug(serial: "3911 - 3960", quantity: 50);
        await Writer().ApplyAsync(rug, Values((RugFieldKeys.Quantity, 30)), Context(isNew: false));
        Assert.Equal(30, rug.Quantity);
        Assert.Equal("3911 - 3960", rug.SerialNo); // بازه می‌تواند از تعداد بلندتر باشد

        await Assert.ThrowsAsync<AppValidationException>(() =>
            Writer().ApplyAsync(rug, Values((RugFieldKeys.Quantity, 51)), Context(isNew: false)));
        Assert.Equal(30, rug.Quantity);
    }

    [Fact]
    public async Task Quantity_BelowOne_IsTheQuantityMessage()
    {
        var error = await Assert.ThrowsAsync<AppValidationException>(() =>
            Writer().ApplyAsync(Rug(), Values((RugFieldKeys.Quantity, 0)), Context(isNew: false)));
        Assert.Equal(RugsManagment.Domain.Entities.Rug.QuantityErrorMessage, error.Errors[RugFieldKeys.Quantity][0]);
    }

    [Fact]
    public async Task AutoMode_NewRug_IgnoresTheTypedSerial_AndAsksForAutoAssignment()
    {
        var rug = NewRug();
        var result = await Writer().ApplyAsync(rug, Values(
            (RugFieldKeys.Serial, "9999"), (RugFieldKeys.Quantity, 3),
            (RugFieldKeys.IntakeLength, 1m), (RugFieldKeys.IntakeWidth, 1m)), Context(isNew: true, SerialMode.Auto));

        Assert.True(result.NeedsAutoSerial);
        Assert.Null(rug.SerialNo);
        Assert.Equal(3, rug.Quantity);

        // ویرایش فرش موجود در همان حالت شماره را می‌پذیرد (اصلاح دستی)
        var existing = Rug();
        var edit = await Writer().ApplyAsync(existing, Values((RugFieldKeys.Serial, "5000")), Context(isNew: false, SerialMode.Auto));
        Assert.False(edit.NeedsAutoSerial);
        Assert.Equal("5000", existing.SerialNo);
    }

    // ── اندازه‌ها ────────────────────────────────────────────────────────

    [Fact]
    public async Task NewRug_NeedsBothIntakeSides()
    {
        var error = await Assert.ThrowsAsync<AppValidationException>(() =>
            Writer().ApplyAsync(NewRug(), Values((RugFieldKeys.Title, "گبه")), Context(isNew: true)));
        Assert.True(error.Errors.ContainsKey(RugFieldKeys.IntakeLength));
        Assert.True(error.Errors.ContainsKey(RugFieldKeys.IntakeWidth));
    }

    [Theory]
    [InlineData(31)]
    [InlineData(0.04)]
    [InlineData(2.085)]
    public async Task IntakeSide_OutOfTheHardLimits_IsTheDimensionMessage(double side)
    {
        var error = await Assert.ThrowsAsync<AppValidationException>(() =>
            Writer().ApplyAsync(Rug(), Values((RugFieldKeys.IntakeLength, (decimal)side)), Context(isNew: false)));
        Assert.Equal(Dimension.ErrorMessage, error.Errors[RugFieldKeys.IntakeLength][0]);
    }

    [Fact]
    public async Task IntakeSide_FromPersianText_KeepsTheOtherSide()
    {
        var rug = Rug(2.08m, 2.10m);
        await Writer().ApplyAsync(rug, Values((RugFieldKeys.IntakeLength, "۳٫۳۵")), Context(isNew: false));
        Assert.Equal(3.35m, rug.IntakeLength);
        Assert.Equal(2.10m, rug.IntakeWidth);
        Assert.Equal(3.35m, rug.LengthMeters);
    }

    [Fact]
    public async Task FinalSize_OverridesTheEffectivePair_AndClearReverts()
    {
        var rug = Rug(3.35m, 2.52m);
        await Writer().ApplyAsync(rug, Values((RugFieldKeys.FinalLength, 3.30m), (RugFieldKeys.FinalWidth, 2.50m)), Context(isNew: false));
        Assert.Equal(3.30m, rug.LengthMeters);
        Assert.Equal(8.25m, rug.AreaM2);
        Assert.Equal(3.35m, rug.IntakeLength); // اندازهٔ اول دست نمی‌خورد
        Assert.NotNull(rug.FinalMeasuredAt);

        await Writer().ApplyAsync(rug, Values((RugFieldKeys.FinalLength, null), (RugFieldKeys.FinalWidth, null)), Context(isNew: false));
        Assert.False(rug.HasFinalSize);
        Assert.Equal(3.35m, rug.LengthMeters);
    }

    [Fact]
    public async Task FinalSize_HalfAPair_Is400()
    {
        var error = await Assert.ThrowsAsync<AppValidationException>(() =>
            Writer().ApplyAsync(Rug(), Values((RugFieldKeys.FinalLength, 2m)), Context(isNew: false)));
        Assert.Equal(Messages.SizePairRequired.Text, error.Errors[RugFieldKeys.FinalLength][0]);
    }

    // ── متن، کلید ناشناخته، همه‌یا‌هیچ ───────────────────────────────────

    [Fact]
    public async Task Text_IsNormalised_AndEmptyClears()
    {
        var rug = Rug();
        rug.Pattern = "قدیمی";
        await Writer().ApplyAsync(rug, Values((RugFieldKeys.Origin, "  كاشان  "), (RugFieldKeys.Pattern, "")), Context(isNew: false));
        Assert.Equal("کاشان", rug.Origin);
        Assert.Null(rug.Pattern);
    }

    [Fact]
    public async Task UnknownAndReadOnlyKeys_Are400()
    {
        var error = await Assert.ThrowsAsync<AppValidationException>(() =>
            Writer().ApplyAsync(Rug(), Values(("rug.nope", 1), (RugFieldKeys.AreaM2, 5)), Context(isNew: false)));
        Assert.True(error.Errors.ContainsKey("rug.nope"));
        Assert.Contains("نوشتنی نیست", error.Errors[RugFieldKeys.AreaM2][0]);
    }

    [Fact]
    public async Task OneBadField_ChangesNothing()
    {
        var rug = Rug(serial: "3778");
        rug.Title = "قبلی";
        await Assert.ThrowsAsync<AppValidationException>(() => Writer().ApplyAsync(rug, Values(
            (RugFieldKeys.Title, "تازه"), (RugFieldKeys.Serial, "4000"), (RugFieldKeys.IntakeWidth, 99m)), Context(isNew: false)));

        Assert.Equal("قبلی", rug.Title);
        Assert.Equal("3778", rug.SerialNo);
        Assert.Equal(2.10m, rug.IntakeWidth);
    }

    // ── فیلدهای سفارشی ──────────────────────────────────────────────────

    [Fact]
    public async Task CustomFields_WriteMetadata_ByType()
    {
        var rug = Rug();
        rug.MetadataJson = """{"keep":"x","bg_color":"قدیم"}""";
        await Writer().ApplyAsync(rug, Values(("rug.custom.bg_color", "سرمه‌ای"), ("rug.custom.grade", "اعلا")), Context(isNew: false));

        using var doc = System.Text.Json.JsonDocument.Parse(rug.MetadataJson!);
        // متن از PersianText.Normalize می‌گذرد (B.1)، همان یکسان‌سازی جستجو و KeyNormalize
        Assert.Equal(PersianText.Normalize("سرمه‌ای"), doc.RootElement.GetProperty("bg_color").GetString());
        Assert.Equal("اعلا", doc.RootElement.GetProperty("grade").GetString());
        Assert.Equal("x", doc.RootElement.GetProperty("keep").GetString()); // کلید دیگر دست نمی‌خورد

        await Writer().ApplyAsync(rug, Values(("rug.custom.bg_color", null)), Context(isNew: false));
        using var cleared = System.Text.Json.JsonDocument.Parse(rug.MetadataJson!);
        Assert.False(cleared.RootElement.TryGetProperty("bg_color", out _));
    }

    [Fact]
    public async Task CustomFields_UnknownInactiveOrBadOption_Are400()
    {
        var error = await Assert.ThrowsAsync<AppValidationException>(() => Writer().ApplyAsync(Rug(), Values(
            ("rug.custom.grade", "بد"), ("rug.custom.old", "x"), ("rug.custom.none", "x")), Context(isNew: false)));
        Assert.Contains("«بد»", error.Errors["rug.custom.grade"][0]);
        Assert.True(error.Errors.ContainsKey("rug.custom.old"));
        Assert.True(error.Errors.ContainsKey("rug.custom.none"));
    }

    // ── پول ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(RugFieldKeys.TargetSalePrice)]
    [InlineData("purchase.price")]
    public async Task MoneyKey_FromANonBusinessUser_Is403_EvenWhenNull(string key)
    {
        var rug = Rug();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Writer(business: false).ApplyAsync(rug, Values((RugFieldKeys.Title, "x"), (key, null)), Context(isNew: false)));
        Assert.Null(rug.Title);
    }

    [Fact]
    public async Task TargetSalePrice_RespectsTheMoneyScale()
    {
        var rug = Rug();
        await Writer().ApplyAsync(rug, Values((RugFieldKeys.TargetSalePrice, 9_000_000m)), Context(isNew: false));
        Assert.Equal(9_000_000m, rug.TargetSalePrice);

        await Assert.ThrowsAsync<AppValidationException>(() =>
            Writer().ApplyAsync(rug, Values((RugFieldKeys.TargetSalePrice, 10.5m)), Context(isNew: false, scale: 0)));
    }

    // ── خانه‌های خرید (G.2) ─────────────────────────────────────────────

    [Fact]
    public async Task PurchaseCell_AddsTheCategoryLine_WithTheUnitChip()
    {
        var rug = Rug(0.40m, 0.40m, 50, "3911 - 3960");
        await Writer().ApplyAsync(rug, Values(
            ("purchase.price", new { rate = 300_000m, basis = (int)CostBasis.PerPiece }),
            ("purchase.broker", 2m)), Context(isNew: false));

        var price = Assert.Single(_lines.Added, l => l.CategoryId == Category(CostCategorySeedContributor.PriceKey).Id);
        Assert.Equal(CostBasis.PerPiece, price.Basis);
        Assert.Equal(300_000m, price.Rate);
        Assert.Equal("قیمت خرید", price.Label);
        Assert.Equal(rug.Id, price.RugId);

        var broker = Assert.Single(_lines.Added, l => l.CategoryId == Category(CostCategorySeedContributor.BrokerKey).Id);
        Assert.Equal(CostBasis.Percent, broker.Basis);
        Assert.Equal(2m, broker.Rate);
    }

    [Fact]
    public async Task PurchaseCell_NumberOnly_KeepsTheExistingBasis_AndNullDeletes()
    {
        var rug = Rug();
        var line = new RugPurchaseLine
        {
            TenantId = TenantId, RugId = rug.Id, CategoryId = Category(CostCategorySeedContributor.PriceKey).Id,
            Label = "قیمت خرید", Basis = CostBasis.Total, Rate = 40_000_000m, Amount = 40_000_000m
        };
        _lines.Items[line.Id] = line;

        await Writer().ApplyAsync(rug, Values(("purchase.price", 41_000_000m)), Context(isNew: false));
        Assert.Equal(CostBasis.Total, line.Basis); // سطر قدیمی M2 «کل» می‌ماند، نه هر متر
        Assert.Equal(41_000_000m, line.Rate);

        await Writer().ApplyAsync(rug, Values(("purchase.price", null)), Context(isNew: false));
        Assert.Contains(line, _lines.Removed);
    }

    [Fact]
    public async Task PurchaseCell_TwoLinesOfTheCategory_IsReadOnly()
    {
        var rug = Rug();
        var categoryId = Category(CostCategorySeedContributor.PriceKey).Id;
        foreach (var rate in new[] { 1m, 2m })
        {
            var l = new RugPurchaseLine { TenantId = TenantId, RugId = rug.Id, CategoryId = categoryId, Label = "قیمت خرید", Rate = rate };
            _lines.Items[l.Id] = l;
        }

        var error = await Assert.ThrowsAsync<AppValidationException>(() =>
            Writer().ApplyAsync(rug, Values(("purchase.price", 5m)), Context(isNew: false)));
        Assert.Contains("چند سطر", error.Errors["purchase.price"][0]);
    }

    [Fact]
    public async Task PurchaseCell_PercentOutOfRange_AndUnknownCategory_Are400()
    {
        var error = await Assert.ThrowsAsync<AppValidationException>(() => Writer().ApplyAsync(Rug(), Values(
            ("purchase.broker", 120m), ("purchase.cat:" + Guid.NewGuid().ToString("D"), 1m)), Context(isNew: false)));
        Assert.Equal(Messages.PurchasePercentRange.Text, error.Errors["purchase.broker"][0]);
        Assert.Equal(2, error.Errors.Count);
    }

    // ── قاعدهٔ قدیمی «قیمت خرید» (I.3، LM-02) ──────────────────────────

    [Fact]
    public async Task LegacyPurchaseCost_NoLines_CreatesOneTotalLine()
    {
        var rug = Rug();
        Assert.True(await Writer().ApplyLegacyPurchaseCostAsync(rug, 40_000_000m, Context(isNew: false)));

        var line = Assert.Single(_lines.Added);
        Assert.Equal(CostBasis.Total, line.Basis);
        Assert.Equal(40_000_000m, line.Rate);
        Assert.Equal(RugPurchaseLine.LegacyPurchaseLabel, line.Label);
        Assert.Equal(Category(CostCategorySeedContributor.PriceKey).Id, line.CategoryId);
    }

    [Fact]
    public async Task LegacyPurchaseCost_OneTotalLine_UpdatesItsRate()
    {
        var rug = Rug();
        var line = new RugPurchaseLine { TenantId = TenantId, RugId = rug.Id, Label = "قیمت خرید", Basis = CostBasis.Total, Rate = 1000m, Amount = 1000m };
        _lines.Items[line.Id] = line;

        Assert.False(await Writer().ApplyLegacyPurchaseCostAsync(rug, 1000m, Context(isNew: false)));
        Assert.True(await Writer().ApplyLegacyPurchaseCostAsync(rug, 1200m, Context(isNew: false)));
        Assert.Equal(1200m, line.Rate);
        Assert.Empty(_lines.Added);
    }

    [Fact]
    public async Task LegacyPurchaseCost_OneTotalLine_ItsAmountBack_IsAnUnchangedRoundTrip()
    {
        // نرخ ۱۰۰۰٫۵ در رقم اعشار ۰ مبلغ ۱۰۰۱ دارد؛ فرم قدیمی همان ۱۰۰۱ را (RugDto.PurchaseCost) پس می‌فرستد
        var rug = Rug();
        var line = new RugPurchaseLine { TenantId = TenantId, RugId = rug.Id, Label = "قیمت خرید", Basis = CostBasis.Total, Rate = 1000.5m, Amount = 1001m };
        _lines.Items[line.Id] = line;

        Assert.False(await Writer().ApplyLegacyPurchaseCostAsync(rug, 1001m, Context(isNew: false)));
        Assert.Equal(1000.5m, line.Rate);
    }

    [Fact]
    public async Task LegacyPurchaseCost_MultiLine_EqualSumIsIgnored_OtherwiseConflict()
    {
        var rug = Rug();
        var perM2 = new RugPurchaseLine { TenantId = TenantId, RugId = rug.Id, Label = "قیمت خرید", Basis = CostBasis.PerM2, Rate = 9_000_000m, Amount = 39_330_000m };
        var broker = new RugPurchaseLine { TenantId = TenantId, RugId = rug.Id, Label = "دلالی", Basis = CostBasis.Percent, Rate = 2m, Amount = 786_600m };
        _lines.Items[perM2.Id] = perM2;
        _lines.Items[broker.Id] = broker;

        // رفت‌وبرگشت فرم قدیمی (همان جمعی که RugDto.PurchaseCost گزارش کرد): هیچ سطری دست نمی‌خورد
        Assert.False(await Writer().ApplyLegacyPurchaseCostAsync(rug, 40_116_600m, Context(isNew: false)));
        Assert.Equal(9_000_000m, perM2.Rate);

        var conflict = await Assert.ThrowsAsync<DomainConflictException>(() =>
            Writer().ApplyLegacyPurchaseCostAsync(rug, 1m, Context(isNew: false)));
        Assert.Equal(Messages.PurchaseMultiLine.Text, conflict.Message);
        Assert.Equal(2, _lines.Items.Count);
    }

    [Fact]
    public async Task LegacyPurchaseCost_FromANonBusinessUser_Is403()
        => await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Writer(business: false).ApplyLegacyPurchaseCostAsync(Rug(), 1m, Context(isNew: false)));
}
