using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.Features.Rugs;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using static RugsManagment.Tests.P2App.P2Data;

namespace RugsManagment.Tests.P2App;

/// <summary>
/// برش دسته (spec D.9، L.1 «RugSplitService»): شماره خودکار یا نوشته‌شده، سطرهای «کل» و مبلغ‌های مرحله با «بزرگ‌ترین
/// باقی‌مانده» به نسبت تعداد، سطرهای «هر متر/هر تخته/درصد» کپی، عکس‌ها با فرش مبدأ، ممنوع وقتی فروش دارد.
/// </summary>
public class RugSplitServiceTests
{
    private readonly P2MemoryRepository<RugWorkflowStep> _steps = new();
    private readonly P2MemoryRepository<RugPurchaseLine> _lines = new();
    private readonly P2AuditLog _audit = new();
    private readonly List<Rug> _addedRugs = [];

    private RugSplitService Service() => new(new AddOnlyRugRepository(_addedRugs), _steps, _lines, new P2SkuGenerator(), _audit);

    private static RugSplitContext Ctx => new(TenantId, 0, new DateTimeOffset(2026, 10, 4, 8, 0, 0, TimeSpan.Zero));

    private static Rug Lot(string? serial = "3911 - 3960", int quantity = 50)
    {
        var rug = Rug(0.40m, 0.40m, quantity, serial);
        rug.Title = "گبه";
        rug.Origin = "گبه";
        rug.BatchId = Guid.NewGuid();
        rug.MetadataJson = """{"bg_color":"قرمز"}""";
        return rug;
    }

    // ── پیش‌شرط‌ها و شماره‌ها ───────────────────────────────────────────

    [Fact]
    public void Plan_Automatic_WhenTheRangeEqualsTheQuantity()
    {
        var (kept, taken) = RugSplitService.Plan(Lot(), new SplitRugRequest(20, null, null, 1));
        Assert.Equal("3911 - 3940", kept!.Display);
        Assert.Equal("3941 - 3960", taken!.Display);
    }

    [Fact]
    public void Plan_Unnumbered_StaysUnnumbered()
    {
        var (kept, taken) = RugSplitService.Plan(Lot(serial: null), new SplitRugRequest(5, null, null, 1));
        Assert.Null(kept);
        Assert.Null(taken);
    }

    [Fact]
    public void Plan_RangeLongerThanTheQuantity_NeedsTypedSerials()
    {
        var lot = Lot(serial: "3911 - 3970", quantity: 50);
        var error = Assert.Throws<AppValidationException>(() => RugSplitService.Plan(lot, new SplitRugRequest(20, null, null, 1)));
        Assert.Equal(Messages.SplitSerialsRequired.Text, error.Message);

        var (kept, taken) = RugSplitService.Plan(lot, new SplitRugRequest(20, "3911 - 3940", "۳۹۵۱ تا ۳۹۷۰", 1));
        Assert.Equal("3911 - 3940", kept!.Display);
        Assert.Equal("3951 - 3970", taken!.Display);
    }

    [Fact]
    public void Plan_TypedSerial_MustCoverItsPart()
    {
        var error = Assert.Throws<AppValidationException>(() =>
            RugSplitService.Plan(Lot(serial: "A-12"), new SplitRugRequest(20, "3911 - 3940", "3941 - 3950", 1)));
        Assert.True(error.Errors.ContainsKey("serialTo"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(60)]
    public void Plan_CountOutOfRange_Is400(int quantity)
    {
        var error = Assert.Throws<AppValidationException>(() => RugSplitService.Plan(Lot(), new SplitRugRequest(quantity, null, null, 1)));
        Assert.Contains("۴۹", error.Errors["quantity"][0]);
    }

    [Fact]
    public void Plan_NotALot_Or_Sold_IsRefused()
    {
        Assert.Throws<AppValidationException>(() => RugSplitService.Plan(Rug(quantity: 1), new SplitRugRequest(1, null, null, 1)));

        var sold = Lot();
        sold.Sale = new RugSale { TenantId = TenantId, RugId = sold.Id, BuyerName = "x", SalePrice = 1 };
        var conflict = Assert.Throws<DomainConflictException>(() => RugSplitService.Plan(sold, new SplitRugRequest(1, null, null, 1)));
        Assert.Equal(Messages.SplitSold.Text, conflict.Message);
    }

    // ── برش ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Split_CopiesTheRug_AndSplitsTheMoneyByQuantity()
    {
        var lot = Lot();
        lot.SetFinalSize(0.39m, 0.40m, DateTimeOffset.UtcNow, null);
        lot.TargetSalePrice = 1_000_001m;
        var total = Line(lot, CostBasis.Total, 15_000_001m);
        var perPiece = Line(lot, CostBasis.PerPiece, 300_000m);
        var broker = Line(lot, CostBasis.Percent, 2m);
        var step = new RugWorkflowStep
        {
            RugId = lot.Id, ProcessStepTypeId = Guid.NewGuid(), OrderIndex = 0, Status = WorkflowStepStatus.Completed,
            CalculatedCost = 1_000m, Adjustment = -101m, PricedLength = 0.40m, PricedWidth = 0.40m, PricedQuantity = 50
        };
        lot.WorkflowSteps.Add(step);

        var created = await Service().SplitAsync(lot, new SplitRugRequest(20, null, null, 1), Ctx);

        // فرش‌ها
        Assert.Equal(30, lot.Quantity);
        Assert.Equal("3911 - 3940", lot.SerialNo);
        Assert.Equal(20, created.Quantity);
        Assert.Equal("3941 - 3960", created.SerialNo);
        Assert.Contains(created, _addedRugs);
        Assert.NotEqual(lot.Sku, created.Sku);
        Assert.Equal((0.40m, 0.40m), (created.IntakeLength, created.IntakeWidth));
        Assert.Equal((0.39m, 0.40m), (created.FinalLength!.Value, created.FinalWidth!.Value));
        Assert.Equal((lot.Title, lot.Origin, lot.BatchId, lot.MetadataJson), (created.Title, created.Origin, created.BatchId, created.MetadataJson));
        Assert.Empty(created.Images);

        // «کل» با LR: ۱۵٬۰۰۰٬۰۰۱ × ۳۰/۵۰ و ۲۰/۵۰ → واحد اضافه به باقی‌ماندهٔ بزرگ‌تر
        Assert.Equal(9_000_001m, total.Rate);
        var newLines = _lines.Added.Where(l => l.RugId == created.Id).ToList();
        Assert.Equal(6_000_000m, newLines.Single(l => l.Basis == CostBasis.Total).Rate);
        Assert.Equal(15_000_001m, total.Rate + newLines.Single(l => l.Basis == CostBasis.Total).Rate);

        // هر تخته و درصد کپی می‌شوند (با تعداد تازه دوباره حساب می‌شوند)
        Assert.Equal(300_000m, perPiece.Rate);
        Assert.Equal(300_000m, newLines.Single(l => l.Basis == CostBasis.PerPiece).Rate);
        Assert.Equal(2m, newLines.Single(l => l.Basis == CostBasis.Percent).Rate);
        Assert.Equal(broker.CategoryId, newLines.Single(l => l.Basis == CostBasis.Percent).CategoryId);

        // قیمت هدف کل ردیف است
        Assert.Equal(1_000_001m, lot.TargetSalePrice + created.TargetSalePrice);

        // مرحله: مبلغ محاسبه‌شده و تعدیل تقسیم، عکس تعداد به هر بخش
        var copy = Assert.Single(_steps.Added);
        Assert.Equal(created.Id, copy.RugId);
        Assert.Equal((600m, 400m), (step.CalculatedCost!.Value, copy.CalculatedCost!.Value));
        Assert.Equal((-61m, -40m), (step.Adjustment!.Value, copy.Adjustment!.Value));
        Assert.Equal((30, 20), (step.PricedQuantity!.Value, copy.PricedQuantity!.Value));
        Assert.Equal(step.Status, copy.Status);
        Assert.Equal(1_000m - 101m, step.EffectiveCost + copy.EffectiveCost);

        // تاریخچه: یکی روی هر فرش
        Assert.Equal(2, _audit.Entries.Count(e => e.Action == AuditAction.RugSplit));
        Assert.Contains(_audit.Entries, e => e.EntityId == created.Id && e.Summary == "جداشده از ۳۹۱۱ - ۳۹۶۰");
    }

    [Fact]
    public void ScaleOf_CountsRealDecimals()
    {
        Assert.Equal(0, RugSplitService.ScaleOf(1500m));
        Assert.Equal(1, RugSplitService.ScaleOf(1500.50m)); // صفر انتهایی رقم واقعی نیست
        Assert.Equal(2, RugSplitService.ScaleOf(1500.55m));
        Assert.Equal(1, RugSplitService.ScaleOf(-0.5m));
        Assert.Equal(6, RugSplitService.ScaleOf(1.12345678m));
    }

    private RugPurchaseLine Line(Rug rug, CostBasis basis, decimal rate)
    {
        var line = new RugPurchaseLine { TenantId = TenantId, RugId = rug.Id, Label = basis.ToString(), Basis = basis, Rate = rate, CategoryId = Guid.NewGuid() };
        _lines.Items[line.Id] = line;
        return line;
    }

    /// <summary>فقط AddAsync لازم است؛ بقیه در این آزمون صدا زده نمی‌شوند.</summary>
    private sealed class AddOnlyRugRepository(List<Rug> added) : NotSupportedRugRepository
    {
        public override Task AddAsync(Rug entity, CancellationToken cancellationToken = default)
        {
            added.Add(entity);
            return Task.CompletedTask;
        }
    }
}
