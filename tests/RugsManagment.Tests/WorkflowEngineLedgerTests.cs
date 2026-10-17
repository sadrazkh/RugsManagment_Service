using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Ledger;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Tests;

/// <summary>
/// موتور گردش کار از فاز ۲ (spec B.5، C.10، D.3، D.4، E17): عکس اندازهٔ قیمت‌گذاری، قیمت روی دسته، تقدم «فروخته‌شده» و
/// «صادر شده» بر وضعیت گردش کار، خواندن جمع ذخیره‌شده (جای ۱ از هفت جای «سرمایهٔ کل») و رد مرحلهٔ کارگاه دیگر در مسیر قالب.
/// </summary>
public class WorkflowEngineLedgerTests
{
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    private sealed class FakeStepTypes : IProcessStepTypeLookup
    {
        private readonly Dictionary<Guid, ProcessStepType> _byId = [];

        public ProcessStepType Add(StepPricingModel model, decimal rate, Guid? tenantId = null)
        {
            var t = new ProcessStepType
            {
                NameFa = "شستشو", NameEn = "wash", Code = "wash", TenantId = tenantId,
                DefaultPricingModel = model, DefaultUnitRate = rate
            };
            _byId[t.Id] = t;
            return t;
        }

        public Task<ProcessStepType> GetRequiredAsync(Guid id, CancellationToken ct = default)
            => _byId.TryGetValue(id, out var t) ? Task.FromResult(t) : throw new KeyNotFoundException();
    }

    /// <summary>خوانندهٔ جمع درون‌حافظه با رقم اعشار دلخواه.</summary>
    private sealed class FakeTotals(int scale = 0) : IRugCostTotalReader
    {
        public Dictionary<Guid, RugCostTotalSnapshot> Rows { get; } = [];

        public RugCostTotalSnapshot? Find(Guid tenantId, Guid rugId) => Rows.GetValueOrDefault(rugId);

        public Task PreloadAsync(IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PreloadTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public int MoneyScale(Guid tenantId) => scale;

        public Task<int> MoneyScaleAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(scale);

        public void Invalidate(IEnumerable<Guid> rugIds) { }

        public void Clear() => Rows.Clear();
    }

    private readonly FakeStepTypes _types = new();

    private WorkflowEngine Engine(FakeTotals? totals = null) => new(new CostCalculationService(), _types, totals);

    private static Rug Lot(int quantity = 50)
    {
        var rug = new Rug { TenantId = TenantId, Sku = "RUG-T-0001" };
        rug.SetIntakeSize(0.40m, 0.40m);
        rug.SetSerialAndQuantity(Domain.Common.SerialNumber.Parse("3911 - 3960"), quantity);
        return rug;
    }

    private static AdvanceStepRequest Complete(bool refresh = false)
        => new(null, null, null, null, null, null, null, MarkCompleted: true, RefreshPricedSize: refresh);

    private async Task<(Rug Rug, RugWorkflowStep Step)> LotWithWashAsync(WorkflowEngine engine, decimal rate = 85_000m)
    {
        var type = _types.Add(StepPricingModel.PerSquareMeter, rate);
        var rug = Lot();
        await engine.BuildCustomWorkflowAsync(rug, [new CustomWorkflowStepRequest(type.Id, false, null, null)]);
        return (rug, rug.WorkflowSteps.Single());
    }

    // ── قیمت روی دسته و عکس اندازه ──

    [Fact]
    public async Task Advance_PricesTheLotPerPiece_AndTakesTheSizeSnapshot()
    {
        var engine = Engine();
        var (rug, step) = await LotWithWashAsync(engine);

        await engine.AdvanceStepAsync(rug, step.Id, Complete());

        // D.7 #4: ۸۵٬۰۰۰ × ۰٫۱۶ = ۱۳٬۶۰۰ برای هر تخته × ۵۰
        Assert.Equal(680_000m, step.CalculatedCost);
        Assert.Equal((0.40m, 0.40m, 50), (step.PricedLength, step.PricedWidth, step.PricedQuantity));
    }

    [Fact]
    public async Task Repricing_KeepsTheSnapshot_UnlessFreshSizeIsAsked()
    {
        var engine = Engine();
        var (rug, step) = await LotWithWashAsync(engine);
        await engine.UpdateStepPricingAsync(rug, step.Id, Complete() with { MarkCompleted = false });

        // اندازه بعد از قیمت‌گذاری عوض شد (اندازهٔ نهایی کوچک‌تر)
        rug.SetFinalSize(0.30m, 0.30m, DateTimeOffset.UtcNow, null);

        await engine.UpdateStepPricingAsync(rug, step.Id, Complete() with { MarkCompleted = false });
        Assert.Equal(680_000m, step.CalculatedCost);

        await engine.UpdateStepPricingAsync(rug, step.Id, Complete(refresh: true) with { MarkCompleted = false });
        // ۸۵٬۰۰۰ × ۰٫۰۹ = ۷٬۶۵۰ × ۵۰
        Assert.Equal(382_500m, step.CalculatedCost);
        Assert.Equal(0.30m, step.PricedLength);
    }

    [Fact]
    public async Task Pricing_UsesTheTenantMoneyScale()
    {
        var engine = Engine(new FakeTotals(scale: 2));
        var (rug, step) = await LotWithWashAsync(engine, rate: 45.55m);

        await engine.AdvanceStepAsync(rug, step.Id, Complete());

        // ۴۵٫۵۵ × ۰٫۱۶ = ۷٫۲۸۸ → ۷٫۲۹ × ۵۰ (رقم اعشار ۲)؛ با رقم ۰ همان ۳۵۰ می‌شد
        Assert.Equal(364.50m, step.CalculatedCost);
    }

    // ── تقدم وضعیت (C.10) ──

    [Fact]
    public async Task AdvancingASoldRug_KeepsItSold()
    {
        var engine = Engine();
        var (rug, step) = await LotWithWashAsync(engine);
        rug.Sale = new RugSale { RugId = rug.Id, TenantId = TenantId, SalePrice = 1m };
        rug.Status = RugStatus.Sold;

        await engine.AdvanceStepAsync(rug, step.Id, Complete());

        Assert.Equal(RugStatus.Sold, rug.Status);
    }

    [Fact]
    public async Task TheWorkflowNeverOverwritesExported()
    {
        var engine = Engine();
        var (rug, step) = await LotWithWashAsync(engine);
        rug.Status = RugStatus.Exported;

        await engine.AdvanceStepAsync(rug, step.Id, Complete());
        Assert.Equal(RugStatus.Exported, rug.Status);

        await engine.GoBackStepAsync(rug);
        Assert.Equal(RugStatus.Exported, rug.Status);
    }

    // ── جمع ذخیره‌شده (D.4 جای ۱) ──

    [Fact]
    public void CalculateRugCosts_ReadsTheStoredTotal()
    {
        var totals = new FakeTotals();
        var rug = Lot();
        rug.TargetSalePrice = 20_000_000m;
        totals.Rows[rug.Id] = new RugCostTotalSnapshot(rug.Id, 15_300_000m, 0m, 4_524_306m, 19_824_306m, 2_478_038m, 396_486m, null, LedgerVersion.Current);

        var costs = Engine(totals).CalculateRugCosts(rug);

        Assert.True(costs.Computed);
        Assert.Equal(19_824_306m, costs.TotalInvestment);
        Assert.Equal(15_300_000m, costs.PurchaseCost);
        Assert.Equal(4_524_306m, costs.ExpenseCost);
        Assert.Equal(2_478_038m, costs.CostPerM2);
        Assert.Equal(396_486m, costs.CostPerPiece);
        Assert.Equal(175_694m, costs.EstimatedMargin);
    }

    [Fact]
    public void CalculateRugCosts_WithoutAStoredRow_UsesTheSameRulesInMemory()
    {
        // D.7 B: ۳۰۰٬۰۰۰ هر تخته × ۵۰ + دلالی ۲٪ = ۱۵٬۳۰۰٬۰۰۰
        var rug = Lot();
        rug.PurchaseLines.Add(new RugPurchaseLine { Basis = CostBasis.PerPiece, Rate = 300_000m, Label = "price", SortOrder = 0 });
        rug.PurchaseLines.Add(new RugPurchaseLine { Basis = CostBasis.Percent, Rate = 2m, Label = "broker", SortOrder = 1 });

        var costs = Engine(new FakeTotals()).CalculateRugCosts(rug);

        Assert.False(costs.Computed);
        Assert.Equal(15_300_000m, costs.PurchaseCost);
        Assert.Equal(15_300_000m, costs.TotalInvestment);
        Assert.Equal(1_912_500m, costs.CostPerM2);   // ÷ ۸٫۰۰ م²
        Assert.Equal(306_000m, costs.CostPerPiece); // ÷ ۵۰
    }

    // ── E17: مسیر قالب ──

    [Fact]
    public async Task TemplateWithAForeignStepType_IsRefused_AndLeavesThePathAlone()
    {
        var foreign = _types.Add(StepPricingModel.Fixed, 1m, tenantId: Guid.NewGuid());
        var template = new WorkflowTemplate
        {
            TenantId = TenantId,
            Steps = [new WorkflowTemplateStep { ProcessStepTypeId = foreign.Id, ProcessStepType = foreign, OrderIndex = 0 }]
        };
        var rug = Lot();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Engine().InitializeWorkflowFromTemplateAsync(rug, template, null));
        Assert.Empty(rug.WorkflowSteps);
        Assert.Null(rug.WorkflowTemplateId);
    }

    [Fact]
    public async Task TemplateWithAForeignProvider_DropsTheProvider()
    {
        var own = _types.Add(StepPricingModel.Fixed, 1m);
        var foreignProvider = new ServiceProvider { TenantId = Guid.NewGuid(), Name = "x" };
        var ownProvider = new ServiceProvider { TenantId = TenantId, Name = "y" };
        var template = new WorkflowTemplate
        {
            TenantId = TenantId,
            Steps =
            [
                new WorkflowTemplateStep
                {
                    ProcessStepTypeId = own.Id, ProcessStepType = own, OrderIndex = 0,
                    DefaultServiceProviderId = foreignProvider.Id, DefaultServiceProvider = foreignProvider
                },
                new WorkflowTemplateStep
                {
                    ProcessStepTypeId = own.Id, ProcessStepType = own, OrderIndex = 1,
                    DefaultServiceProviderId = ownProvider.Id, DefaultServiceProvider = ownProvider
                }
            ]
        };
        var rug = Lot();

        await Engine().InitializeWorkflowFromTemplateAsync(rug, template, null);

        var steps = rug.WorkflowSteps.OrderBy(s => s.OrderIndex).ToList();
        Assert.Null(steps[0].ServiceProviderId);
        Assert.Equal(ownProvider.Id, steps[1].ServiceProviderId);
    }
}
