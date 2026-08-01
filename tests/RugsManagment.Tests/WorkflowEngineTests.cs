using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Tests;

/// <summary>
/// موتور گردش کار — قلب سامانه. اشتباه اینجا یعنی فرش در مرحلهٔ غلط گیر می‌کند
/// یا تاریخچه‌اش از بین می‌رود.
/// </summary>
public class WorkflowEngineTests
{
    /// <summary>کاتالوگ درون‌حافظه‌ای، تا تست به دیتابیس وابسته نباشد.</summary>
    private sealed class FakeStepTypes : IProcessStepTypeLookup
    {
        private readonly Dictionary<Guid, ProcessStepType> _byId = [];

        public ProcessStepType Add(string name, decimal rate = 100_000m)
        {
            var t = new ProcessStepType
            {
                NameFa = name,
                NameEn = name,
                Code = name,
                DefaultPricingModel = StepPricingModel.Fixed,
                DefaultUnitRate = rate
            };
            _byId[t.Id] = t;
            return t;
        }

        public Task<ProcessStepType> GetRequiredAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_byId[id]);
    }

    private readonly FakeStepTypes _catalog = new();
    private readonly WorkflowEngine _sut;

    private readonly ProcessStepType _wash;
    private readonly ProcessStepType _stretch;
    private readonly ProcessStepType _repair;

    public WorkflowEngineTests()
    {
        _sut = new WorkflowEngine(new CostCalculationService(), _catalog);
        _wash = _catalog.Add("قالیشویی", 200_000m);
        _stretch = _catalog.Add("دارکشی", 150_000m);
        _repair = _catalog.Add("رفوگری", 300_000m);
    }

    private static Rug NewRug() => new() { WidthMeters = 2m, LengthMeters = 3m, Sku = "RUG-TEST-0001" };

    private static AdvanceStepRequest Advance(decimal? manualCost = null, bool complete = true)
        => new(null, manualCost, null, null, null, null, null, complete, null);

    private async Task<Rug> RugWithPath(params ProcessStepType[] types)
        => await RugWithPath(optional: false, types);

    private async Task<Rug> RugWithPath(bool optional, params ProcessStepType[] types)
    {
        var rug = NewRug();
        var steps = types
            .Select(t => new CustomWorkflowStepRequest(t.Id, optional, null, null))
            .ToList();

        await _sut.BuildCustomWorkflowAsync(rug, steps);
        return rug;
    }

    // ── ساخت مسیر ──

    [Fact]
    public async Task BuildCustomWorkflow_ActivatesOnlyTheFirstStep()
    {
        var rug = await RugWithPath(_wash, _stretch, _repair);

        Assert.Equal(3, rug.WorkflowSteps.Count);
        Assert.Equal(WorkflowStepStatus.InProgress, Ordered(rug)[0].Status);
        Assert.All(Ordered(rug).Skip(1), s => Assert.Equal(WorkflowStepStatus.Pending, s.Status));
        Assert.Equal(RugStatus.InProgress, rug.Status);
    }

    [Fact]
    public async Task BuildCustomWorkflow_NumbersStepsInOrder()
    {
        var rug = await RugWithPath(_wash, _stretch, _repair);
        Assert.Equal([0, 1, 2], Ordered(rug).Select(s => s.OrderIndex));
    }

    // ── حرکت رو به جلو ──

    [Fact]
    public async Task Advance_CompletesCurrentAndActivatesNext()
    {
        var rug = await RugWithPath(_wash, _stretch);
        await _sut.AdvanceStepAsync(rug, Ordered(rug)[0].Id, Advance());

        Assert.Equal(WorkflowStepStatus.Completed, Ordered(rug)[0].Status);
        Assert.Equal(WorkflowStepStatus.InProgress, Ordered(rug)[1].Status);
        Assert.NotNull(Ordered(rug)[0].CompletedAt);
    }

    [Fact]
    public async Task Advance_OnLastStep_MarksRugReadyForSale()
    {
        var rug = await RugWithPath(_wash);
        await _sut.AdvanceStepAsync(rug, Ordered(rug)[0].Id, Advance());

        Assert.Equal(RugStatus.ReadyForSale, rug.Status);
    }

    [Fact]
    public async Task Advance_AlreadyClosedStep_IsRejected()
    {
        var rug = await RugWithPath(_wash, _stretch);
        var first = Ordered(rug)[0];
        await _sut.AdvanceStepAsync(rug, first.Id, Advance());

        // این محافظ است که بازپخش صف آفلاین را ایمن می‌کند
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AdvanceStepAsync(rug, first.Id, Advance()));

        Assert.Contains("قبلاً بسته شده", ex.Message);
    }

    [Fact]
    public async Task Advance_UnknownStepId_IsRejected()
    {
        var rug = await RugWithPath(_wash);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AdvanceStepAsync(rug, Guid.NewGuid(), Advance()));
    }

    // ── رد کردن و بازگشت ──

    [Fact]
    public async Task Skip_MarksStepSkippedAndMovesOn()
    {
        var rug = await RugWithPath(optional: true, _wash, _stretch);
        await _sut.SkipStepAsync(rug, Ordered(rug)[0].Id);

        Assert.Equal(WorkflowStepStatus.Skipped, Ordered(rug)[0].Status);
        Assert.Equal(WorkflowStepStatus.InProgress, Ordered(rug)[1].Status);
    }

    [Fact]
    public async Task Skip_MandatoryStep_IsRejected()
    {
        // رد کردن مرحلهٔ اجباری باید ممنوع باشد، وگرنه فرش بدون قالیشویی «آماده» می‌شود
        var rug = await RugWithPath(_wash);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.SkipStepAsync(rug, Ordered(rug)[0].Id));

        Assert.Contains("اختیاری", ex.Message);
    }

    [Fact]
    public async Task GoBack_ReopensPreviousStep()
    {
        var rug = await RugWithPath(_wash, _stretch);
        await _sut.AdvanceStepAsync(rug, Ordered(rug)[0].Id, Advance());

        await _sut.GoBackStepAsync(rug);

        Assert.Equal(WorkflowStepStatus.InProgress, Ordered(rug)[0].Status);
        Assert.Equal(WorkflowStepStatus.Pending, Ordered(rug)[1].Status);
        Assert.Null(Ordered(rug)[0].CompletedAt);
    }

    [Fact]
    public async Task GoBack_FromReadyForSale_ReopensLastStep()
    {
        var rug = await RugWithPath(_wash);
        await _sut.AdvanceStepAsync(rug, Ordered(rug)[0].Id, Advance());
        Assert.Equal(RugStatus.ReadyForSale, rug.Status);

        await _sut.GoBackStepAsync(rug);

        Assert.Equal(RugStatus.InProgress, rug.Status);
        Assert.Equal(WorkflowStepStatus.InProgress, Ordered(rug)[0].Status);
    }

    // ── ویرایش مسیر ──

    [Fact]
    public async Task UpdatePath_KeepsCompletedStepsAndReplacesTheRest()
    {
        var rug = await RugWithPath(_wash, _stretch);
        await _sut.AdvanceStepAsync(rug, Ordered(rug)[0].Id, Advance(manualCost: 50_000m));

        // مسیر باقی‌مانده با یک مرحلهٔ متفاوت جایگزین می‌شود
        await _sut.UpdateWorkflowPathAsync(rug, [new CustomWorkflowStepRequest(_repair.Id, false, null, null)]);

        var steps = Ordered(rug);
        Assert.Equal(2, steps.Count);
        // تاریخچهٔ مرحلهٔ تمام‌شده باید دست‌نخورده بماند
        Assert.Equal(WorkflowStepStatus.Completed, steps[0].Status);
        Assert.Equal(_wash.Id, steps[0].ProcessStepTypeId);
        Assert.Equal(_repair.Id, steps[1].ProcessStepTypeId);
    }

    // ── محاسبهٔ هزینه روی کل فرش ──

    [Fact]
    public async Task CalculateRugCosts_CountsCompletedAndInProgressSteps()
    {
        // قاعدهٔ سامانه: کارِ شروع‌شده تعهد ایجاد کرده، پس هزینه‌اش شمرده می‌شود.
        // دارکشی نرخ ثابت ۱۵۰٬۰۰۰ دارد و بعد از پیشبرد، «در حال انجام» است.
        var rug = await RugWithPath(_wash, _stretch);
        rug.PurchaseCost = 1_000_000m;

        await _sut.AdvanceStepAsync(rug, Ordered(rug)[0].Id, Advance(manualCost: 250_000m));

        var costs = _sut.CalculateRugCosts(rug);

        Assert.Equal(400_000m, costs.TotalProcessCost);   // ۲۵۰٬۰۰۰ تمام‌شده + ۱۵۰٬۰۰۰ در جریان
        Assert.Equal(1_400_000m, costs.TotalInvestment);
    }

    [Fact]
    public async Task CalculateRugCosts_IgnoresStepsStillPending()
    {
        // مرحله‌ای که هنوز نوبتش نرسیده نباید در سرمایه‌گذاری بیاید
        var rug = await RugWithPath(_wash, _stretch, _repair);

        var costs = _sut.CalculateRugCosts(rug);

        // فقط قالیشویی (در جریان) شمرده می‌شود، نه دارکشی و رفوگریِ در صف
        Assert.Equal(200_000m, costs.TotalProcessCost);
    }

    [Fact]
    public async Task CalculateRugCosts_SkippedStepCostsNothing()
    {
        var rug = await RugWithPath(optional: true, _wash, _stretch);
        await _sut.SkipStepAsync(rug, Ordered(rug)[0].Id);

        var costs = _sut.CalculateRugCosts(rug);

        // فقط دارکشی (که حالا در جریان است) — قالیشویی رد شده و پولی بابتش داده نمی‌شود
        Assert.Equal(150_000m, costs.TotalProcessCost);
    }

    [Fact]
    public async Task CalculateRugCosts_EstimatedMargin_UsesTargetPrice()
    {
        var rug = await RugWithPath(_wash);
        rug.PurchaseCost = 1_000_000m;
        rug.TargetSalePrice = 1_800_000m;

        await _sut.AdvanceStepAsync(rug, Ordered(rug)[0].Id, Advance(manualCost: 200_000m));

        var costs = _sut.CalculateRugCosts(rug);
        Assert.Equal(600_000m, costs.EstimatedMargin);
    }

    [Fact]
    public async Task CalculateRugCosts_WithoutTargetPrice_HasNoMargin()
    {
        var rug = await RugWithPath(_wash);
        var costs = _sut.CalculateRugCosts(rug);
        Assert.Null(costs.EstimatedMargin);
    }

    // ── بازمحاسبهٔ وضعیت ──

    [Fact]
    public void ResolveStatus_NoSteps_IsDraft()
        => Assert.Equal(RugStatus.Draft, _sut.ResolveStatusFromWorkflow(NewRug()));

    [Fact]
    public async Task ResolveStatus_PartiallyDone_IsInProgress()
    {
        var rug = await RugWithPath(_wash, _stretch);
        await _sut.AdvanceStepAsync(rug, Ordered(rug)[0].Id, Advance());

        Assert.Equal(RugStatus.InProgress, _sut.ResolveStatusFromWorkflow(rug));
    }

    [Fact]
    public async Task ResolveStatus_AllSettled_IsReadyForSale()
    {
        var rug = await RugWithPath(_wash, _stretch);
        await _sut.AdvanceStepAsync(rug, Ordered(rug)[0].Id, Advance());
        await _sut.AdvanceStepAsync(rug, Ordered(rug)[1].Id, Advance());

        Assert.Equal(RugStatus.ReadyForSale, _sut.ResolveStatusFromWorkflow(rug));
    }

    [Fact]
    public async Task ResolveStatus_SkippedCountsAsSettled()
    {
        var rug = await RugWithPath(optional: true, _wash);
        await _sut.SkipStepAsync(rug, Ordered(rug)[0].Id);

        Assert.Equal(RugStatus.ReadyForSale, _sut.ResolveStatusFromWorkflow(rug));
    }

    private static List<RugWorkflowStep> Ordered(Rug rug)
        => rug.WorkflowSteps.OrderBy(s => s.OrderIndex).ToList();
}
