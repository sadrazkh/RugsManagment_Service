using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Pricing;
using RugsManagment.Application.Features.Rugs;
using RugsManagment.Application.Fields;
using RugsManagment.Application.Ledger;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.Tests.P2App;
using static RugsManagment.Tests.P2App.P2Data;

namespace RugsManagment.Tests.P2Fix;

/// <summary>
/// یافته‌های تأییدشدهٔ بازبینی فاز ۲ (p2-review.json) که بی دیتابیس آزموده می‌شوند: کلید پولی با فاصلهٔ دو سر از
/// انباردار (۴۰۳)، مبلغ سطر خرید که از ستون numeric(18,2) بیرون می‌زند (۴۰۰ روی همان خانه، نه ۵۰۰)، پیش‌نمایش هزینهٔ
/// مرحله روی دسته (قیمت یک تخته × تعداد، کف صفر روی کل ردیف)، مبلغ دستی و تعدیل مرحله در MoneyScale، و میانگین هزینهٔ
/// مرحله در تحلیل با گرد کردن نصفه-دور-از-صفر.
/// </summary>
public class P2ReviewFixTests
{
    private readonly P2MemoryRepository<CustomFieldDefinition> _fields = new();
    private readonly P2MemoryRepository<CostCategory> _categories = PurchaseCategories();
    private readonly P2MemoryRepository<RugPurchaseLine> _lines = new();

    private RugFieldWriter Writer(bool business = true)
        => new(_fields, _categories, _lines, business ? P2Money.Business : P2Money.Operator);

    // ── #11 (high): کلید پولی با فاصله/تب از غیر Business ──────────────────

    [Theory]
    [InlineData(" rug.targetSalePrice")]
    [InlineData("rug.targetSalePrice ")]
    [InlineData("\tpurchase.price")]
    [InlineData(" purchase.price")]
    [InlineData("purchase.price\n")]
    public void PaddedMoneyKey_FromAnOperator_IsRejectedByTheGuard(string key)
    {
        Assert.Throws<UnauthorizedAccessException>(() => Writer(business: false).EnsureMayWrite(Values((key, 777m))));
        Assert.Throws<UnauthorizedAccessException>(() => Writer(business: false).EnsureMayWrite(Values((key, null))));
    }

    [Fact]
    public async Task PaddedTargetSalePrice_FromAnOperator_Is403_AndWritesNothing()
    {
        var rug = Rug();
        rug.TargetSalePrice = 5_000_000m;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Writer(business: false).ApplyAsync(rug, Values((" rug.targetSalePrice", 777m)), Context(isNew: false)));
        Assert.Equal(5_000_000m, rug.TargetSalePrice);
    }

    [Fact]
    public async Task PaddedPurchaseKey_FromAnOperator_Is403_AndNeitherAddsNorDeletesALine()
    {
        var rug = Rug();
        var price = _categories.Items.Values.Single(c => c.SeedKey == Application.Seed.CostCategorySeedContributor.PriceKey);
        var line = new RugPurchaseLine
        {
            TenantId = TenantId, RugId = rug.Id, CategoryId = price.Id, Label = "قیمت خرید",
            Basis = CostBasis.Total, Rate = 9_000_000m, Amount = 9_000_000m
        };
        _lines.Items[line.Id] = line;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Writer(business: false).ApplyAsync(rug, Values((" purchase.price", null)), Context(isNew: false)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Writer(business: false).ApplyAsync(Rug(), Values(
                (RugFieldKeys.IntakeLength, 1m), (RugFieldKeys.IntakeWidth, 1m),
                ("\tpurchase.price", new { rate = 1234m, basis = (int)CostBasis.Total })), Context(isNew: true)));

        Assert.Empty(_lines.Removed);
        Assert.Empty(_lines.Added);
    }

    [Fact]
    public async Task PaddedKey_FromABusinessUser_StillWrites()
    {
        // رفتار کاربر مجاز عوض نشده: کلید با فاصله همان کلید است
        var rug = Rug();
        await Writer().ApplyAsync(rug, Values((" rug.targetSalePrice", 777m), ("rug.title ", "گبه")), Context(isNew: false));
        Assert.Equal(777m, rug.TargetSalePrice);
        Assert.Equal("گبه", rug.Title);
    }

    // ── #13: مبلغ سطر خرید بیرون از numeric(18,2) ──────────────────────────

    [Fact]
    public async Task PerM2Rate_OnA30By30Rug_WhoseAmountOverflows_Is400OnTheCell()
    {
        var error = await Assert.ThrowsAsync<AppValidationException>(() => Writer().ApplyAsync(NewRug(), Values(
            (RugFieldKeys.IntakeLength, 30m), (RugFieldKeys.IntakeWidth, 30m),
            ("purchase.price", new { rate = RugFieldWriter.MaxRate, basis = (int)CostBasis.PerM2 })), Context(isNew: true)));

        Assert.Equal(Messages.PurchaseAmountTooLarge.Text, Assert.Single(error.Errors["purchase.price"]));
        Assert.Empty(_lines.Added);
    }

    [Fact]
    public async Task PerPieceRate_TimesALargeLot_WhoseAmountOverflows_Is400OnTheCell()
    {
        var error = await Assert.ThrowsAsync<AppValidationException>(() => Writer().ApplyAsync(NewRug(), Values(
            (RugFieldKeys.Serial, "1 - 1000"), (RugFieldKeys.Quantity, 1000),
            (RugFieldKeys.IntakeLength, 1m), (RugFieldKeys.IntakeWidth, 1m),
            ("purchase.price", new { rate = 10_000_000_000_000m, basis = (int)CostBasis.PerPiece })), Context(isNew: true)));

        Assert.True(error.Errors.ContainsKey("purchase.price"));
    }

    [Fact]
    public async Task QuantityChange_ThatWouldOverflowAKeptLine_Is400OnTheQuantity()
    {
        var rug = Rug(1m, 1m);
        var line = new RugPurchaseLine
        {
            TenantId = TenantId, RugId = rug.Id, Label = "کرایه", Basis = CostBasis.PerPiece,
            Rate = 10_000_000_000_000m, Amount = 10_000_000_000_000m
        };
        _lines.Items[line.Id] = line;

        var error = await Assert.ThrowsAsync<AppValidationException>(() =>
            Writer().ApplyAsync(rug, Values((RugFieldKeys.Quantity, 1000)), Context(isNew: false)));

        Assert.True(error.Errors.ContainsKey(RugFieldKeys.Quantity));
        Assert.Equal(1, rug.Quantity);
    }

    [Fact]
    public async Task NormalLargeValues_StillPass()
    {
        // ۳۰×۳۰ با نرخ متری ۱۰ میلیون (۹ میلیارد) و سقف نرخ روی «کل» درست‌اند
        var rug = NewRug();
        await Writer().ApplyAsync(rug, Values(
            (RugFieldKeys.IntakeLength, 30m), (RugFieldKeys.IntakeWidth, 30m),
            ("purchase.price", new { rate = 10_000_000m, basis = (int)CostBasis.PerM2 })), Context(isNew: true));
        Assert.Single(_lines.Added);

        Assert.Null(RugFieldWriter.CheckAmount(CostBasis.Total, RugFieldWriter.MaxRate, 900m, 10_000));
        Assert.Null(RugFieldWriter.CheckAmount(CostBasis.Percent, 100m, 900m, 10_000));
        Assert.NotNull(RugFieldWriter.CheckAmount(CostBasis.PerM2, RugFieldWriter.MaxRate, 900m, 10_000));
    }

    // ── #3: پیش‌نمایش هزینهٔ مرحله روی دسته ────────────────────────────────

    [Fact]
    public void Preview_OfALot_IsPiecePriceTimesQuantity_WithTheDiscountOnTheLine()
    {
        var sut = new CostCalculationService();

        var result = sut.Preview(new PricingPreviewRequest(
            0.40m, 0.40m, StepPricingModel.PerSquareMeter, 85_000m, null, null, Adjustment: -10_000m, Quantity: 50));

        Assert.Equal(680_000m, result.Base);
        Assert.Equal(670_000m, result.Total);
        Assert.Contains("50 تخته", result.Formula);
        Assert.Contains("13,600", result.Formula);

        // کف صفر روی کل ردیف، نه یک تخته: تخفیف ۲۰ هزار از ۶۸۰ هزار → ۶۶۰ هزار (پیش‌تر ۰)
        var discounted = sut.Preview(new PricingPreviewRequest(
            0.40m, 0.40m, StepPricingModel.PerSquareMeter, 85_000m, null, null, Adjustment: -20_000m, Quantity: 50));
        Assert.Equal(660_000m, discounted.Total);
    }

    [Fact]
    public void Preview_OnThePricedSnapshot_EqualsTheSavedStepCost()
    {
        var sut = new CostCalculationService();
        var type = new ProcessStepType { NameFa = "شستشو", Code = "wash", DefaultPricingModel = StepPricingModel.PerSquareMeter, DefaultUnitRate = 90_000m };
        var rug = Rug(2.08m, 2.10m);
        var step = new RugWorkflowStep { ProcessStepTypeId = type.Id, ProcessStepType = type };
        step.SnapshotPricingSize(2.08m, 2.10m, 1);
        rug.SetFinalSize(2.00m, 2.00m, DateTimeOffset.UtcNow, null); // اندازهٔ امروز عوض شد؛ عکس همان ۴٫۳۷ است

        var saved = sut.CalculateStepCost(rug, type, null, null, null, step);
        var preview = sut.Preview(new PricingPreviewRequest(
            step.PricedWidth!.Value, step.PricedLength!.Value, StepPricingModel.PerSquareMeter, 90_000m, null, null,
            Quantity: step.PricedQuantity!.Value));

        Assert.Equal(393_300m, saved);
        Assert.Equal(saved, preview.Total);
    }

    [Fact]
    public void Preview_Combined_AndManual_OnALot()
    {
        var sut = new CostCalculationService();
        const string json = """{"items":[{"model":0,"rate":50000},{"model":1,"rate":10000}]}""";

        var combined = sut.Preview(new PricingPreviewRequest(2m, 3m, StepPricingModel.Combined, null, json, null, Quantity: 3));
        Assert.Equal(330_000m, combined.Total); // (۵۰٬۰۰۰ + ۶ × ۱۰٬۰۰۰) × ۳

        var manual = sut.Preview(new PricingPreviewRequest(2m, 3m, StepPricingModel.PerSquareMeter, 1m, null, ManualCost: 15_000m, Quantity: 50));
        Assert.Equal(15_000m, manual.Total); // مبلغ دستی برای کل ردیف است، × تعداد نمی‌شود
    }

    // ── #5: مبلغ دستی و تعدیل مرحله در MoneyScale ─────────────────────────

    [Theory]
    [InlineData(0, 100.125, null)]
    [InlineData(0, 1250.5, null)]
    [InlineData(0, null, 0.333)]
    [InlineData(2, 100.125, null)]
    public async Task StepMoney_WithMoreDecimalsThanMoneyScale_Is400(int scale, double? manual, double? adjustment)
    {
        var (engine, rug, step) = await RugWithWashAsync(scale);
        var request = new AdvanceStepRequest(null, (decimal?)manual, null, null, null, null, null,
            MarkCompleted: true, Adjustment: (decimal?)adjustment);

        var error = await Assert.ThrowsAsync<AppValidationException>(() => engine.AdvanceStepAsync(rug, step.Id, request));
        Assert.True(error.Errors.ContainsKey(manual is null ? "adjustment" : "manualCostOverride"));
        Assert.NotEqual(WorkflowStepStatus.Completed, step.Status);
    }

    [Theory]
    [InlineData(0, 100, -50)]
    [InlineData(2, 100.13, -0.5)]
    public async Task StepMoney_WithinMoneyScale_IsAccepted(int scale, double manual, double adjustment)
    {
        var (engine, rug, step) = await RugWithWashAsync(scale);
        await engine.UpdateStepPricingAsync(rug, step.Id,
            new AdvanceStepRequest(null, (decimal)manual, null, null, null, null, null, MarkCompleted: false, Adjustment: (decimal)adjustment));
        Assert.Equal((decimal)manual, step.ManualCostOverride);
        Assert.Equal((decimal)adjustment, step.Adjustment);
    }

    // ── #8: میانگین هزینهٔ مرحله در تحلیل ─────────────────────────────────

    [Fact]
    public async Task Analytics_AverageStepCost_RoundsHalfAwayFromZero()
    {
        var type = new ProcessStepType { NameFa = "شستشو", Code = "wash" };
        var rug = Rug();
        foreach (var cost in new[] { 100m, 101m })
        {
            rug.WorkflowSteps.Add(new RugWorkflowStep
            {
                RugId = rug.Id, ProcessStepTypeId = type.Id, ProcessStepType = type,
                Status = WorkflowStepStatus.Completed, CalculatedCost = cost
            });
        }

        var service = new AnalyticsService(new OneTenantRugs(rug), new WorkflowEngine(new CostCalculationService(), new NoStepTypes()));
        var report = await service.GetAsync(TenantId);

        var wash = Assert.Single(report.StepBreakdown);
        Assert.Equal(101m, wash.AverageCost); // ۱۰۰٫۵ → ۱۰۱ (بانکی ۱۰۰ می‌داد)
    }

    // ── کمک‌ها ────────────────────────────────────────────────────────────

    private static Rug NewRug() => new() { TenantId = TenantId, Sku = "RUG-202610-0042" };

    private static async Task<(WorkflowEngine Engine, Rug Rug, RugWorkflowStep Step)> RugWithWashAsync(int scale)
    {
        var types = new OneStepType();
        var engine = new WorkflowEngine(new CostCalculationService(), types, new ScaleOnlyTotals(scale));
        var rug = Rug();
        await engine.BuildCustomWorkflowAsync(rug, [new CustomWorkflowStepRequest(types.Wash.Id, false, null, null)]);
        return (engine, rug, rug.WorkflowSteps.Single());
    }

    private sealed class OneStepType : IProcessStepTypeLookup
    {
        public ProcessStepType Wash { get; } = new()
        {
            NameFa = "شستشو", NameEn = "wash", Code = "wash", DefaultPricingModel = StepPricingModel.PerSquareMeter, DefaultUnitRate = 85_000m
        };

        public Task<ProcessStepType> GetRequiredAsync(Guid id, CancellationToken ct = default)
            => id == Wash.Id ? Task.FromResult(Wash) : throw new KeyNotFoundException();
    }

    private sealed class NoStepTypes : IProcessStepTypeLookup
    {
        public Task<ProcessStepType> GetRequiredAsync(Guid id, CancellationToken ct = default) => throw new KeyNotFoundException();
    }

    /// <summary>فقط رقم اعشار کارگاه؛ هیچ جمع ذخیره‌شده‌ای نیست.</summary>
    private sealed class ScaleOnlyTotals(int scale) : IRugCostTotalReader
    {
        public RugCostTotalSnapshot? Find(Guid tenantId, Guid rugId) => null;
        public Task PreloadAsync(IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PreloadTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public int MoneyScale(Guid tenantId) => scale;
        public Task<int> MoneyScaleAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(scale);
        public void Invalidate(IEnumerable<Guid> rugIds) { }
        public void Clear() { }
    }

    private sealed class OneTenantRugs(params Rug[] rugs) : NotSupportedRugRepository
    {
        public override Task<IReadOnlyList<Rug>> ListByTenantAsync(Guid tenantId, RugStatus? status, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Rug>>(rugs.Where(r => r.TenantId == tenantId).ToList());
    }
}
