using RugsManagment.Application.Features.Shipments;
using RugsManagment.Application.Features.Shipments.Ledger;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using static RugsManagment.Tests.P4a.P4aChanges;

namespace RugsManagment.Tests.P4a;

/// <summary>
/// قفل محمولهٔ قطعی (spec C.8، L.1 «Guards»): هر ستون قفل‌دار با یک ChangeSet که <b>هیچ اثری</b> ندارد (ذخیرهٔ «فقط قفل»،
/// D.5 #8، #15b، #19) رد می‌شود — مسیرش هر چه باشد (PUT فرش، ویرایش گروهی با «بایگانی»، گردش کار، فروش، سطل زباله، نگله،
/// سربرگ). ستون‌های آزاد (یادداشت، قیمت هدف، مبلغ مرحله، یادداشت محموله، درآمد) و خودِ «قطعی» و «باز کردن دوباره» رد
/// نمی‌شوند.
/// </summary>
public class ShipmentLockGuardTests
{
    private readonly FakeShipmentLedgerRepository _db = new();
    private readonly Rug _rug = NewRug();
    private readonly ShipmentState _shipment;
    private readonly BaleState _bale;

    public ShipmentLockGuardTests()
    {
        (_shipment, _bale) = _db.AddShipment(8, ShipmentStatus.Finalized, 12, _rug.Id);
        _db.RugValues[_rug.Id] = Stored(_rug);
    }

    private ShipmentLockGuard Guard(bool business = true) => new(_db, [new FixedMoneyVisibility(business)]);

    private async Task<ShipmentLockedException> RejectedAsync(params EntityChange[] changes)
    {
        var context = Context(changes);
        var ex = await Assert.ThrowsAsync<ShipmentLockedException>(() => Guard().CheckAsync(context, CancellationToken.None));
        Assert.True(context.Impact.IsEmpty); // قفل بی هیچ اثری هم اجرا می‌شود
        Assert.Equal(_shipment.Id, ex.ShipmentId);
        Assert.Equal(8, ex.ShipmentNumber);
        return ex;
    }

    private async Task PassesAsync(params EntityChange[] changes)
        => await Guard().CheckAsync(Context(changes), CancellationToken.None);

    // ── فرش ─────────────────────────────────────────────────────────────

    public static TheoryData<string, object?, object?> LockedRugFields => new()
    {
        { nameof(Rug.IntakeLength), 2.08m, 2.10m },
        { nameof(Rug.IntakeWidth), 2.10m, 2.00m },
        { nameof(Rug.FinalLength), null, 2.05m },
        { nameof(Rug.FinalWidth), 2.10m, 2.00m },
        { nameof(Rug.FinalMeasuredAt), null, DateTimeOffset.UtcNow },
        { nameof(Rug.FinalMeasuredBy), null, Guid.NewGuid() },
        { nameof(Rug.Quantity), 1, 2 },
        { nameof(Rug.SerialNo), "3778", "3779" },
        { nameof(Rug.SerialFrom), 3778, 3779 },
        { nameof(Rug.Title), "گبه", "قالی" },
        { nameof(Rug.Origin), "گبه", "کاشان" },
        { nameof(Rug.Pattern), "ساده", "لچک ترنج" },  // ویرایش نقشه
        { nameof(Rug.Material), "پشم", "ابریشم" },
        { nameof(Rug.KnotDensity), 40, 50 },
        { nameof(Rug.Status), RugStatus.Exported, RugStatus.Archived }, // BulkUpdateFields با «بایگانی»
        { nameof(Rug.DeletedAt), null, DateTimeOffset.UtcNow },       // سطل زباله
        { nameof(Rug.WorkflowTemplateId), null, Guid.NewGuid() },      // تغییر مسیر گردش کار
        { nameof(Rug.CurrentStepIndex), 0, 1 }                          // پیش بردن
    };

    [Theory]
    [MemberData(nameof(LockedRugFields))]
    public async Task EachLockedRugField_IsRejected_WithAnOtherwiseEmptyImpact(string field, object? original, object? current)
    {
        var ex = await RejectedAsync(Modified(_rug, (field, original, current)));
        Assert.Equal("rug." + char.ToLowerInvariant(field[0]) + field[1..], ex.Field);
        Assert.Contains("۸", ex.Message);
        Assert.Contains("باز کردن دوباره", ex.Message);
    }

    [Fact]
    public async Task OperatorGetsTheMerchantMessage_NotReopen()
    {
        var context = Context(Modified(_rug, (nameof(Rug.Origin), "گبه", "کاشان")));
        var ex = await Assert.ThrowsAsync<ShipmentLockedException>(() => Guard(business: false).CheckAsync(context, CancellationToken.None));
        Assert.Equal("این محموله قطعی شده؛ برای تغییر به بازرگان بگویید.", ex.Message);
    }

    [Theory]
    [InlineData(nameof(RugsManagment.Domain.Entities.Rug.Notes), "یادداشت", "یادداشت تازه")]
    [InlineData(nameof(RugsManagment.Domain.Entities.Rug.TargetSalePrice), null, 1000)]
    [InlineData(nameof(RugsManagment.Domain.Entities.Rug.BatchId), null, "b")]
    public async Task FreeRugFields_Pass(string field, object? original, object? current)
    {
        current = current is "b" ? Guid.NewGuid() : current is int i ? (decimal)i : current;
        await PassesAsync(Modified(_rug, (field, original, current)));
    }

    [Fact]
    public async Task ExportableCustomField_IsLocked_OtherCustomKeysAreFree()
    {
        _db.ExportableKeys.Add("bg_color");

        await PassesAsync(Modified(_rug, (nameof(Rug.MetadataJson), """{"bg_color":"لاکی","inner":"x"}""", """{"bg_color":"لاکی","inner":"y"}""")));
        await RejectedAsync(Modified(_rug, (nameof(Rug.MetadataJson), """{"bg_color":"لاکی"}""", """{"bg_color":"سرمه‌ای"}""")));
    }

    [Fact]
    public async Task CustomDeclaredValueKeyField_IsLockedEvenWhenNotExportable()
    {
        _db.Settings = _db.Settings with { KeyField = "rug.custom.grade" };
        await RejectedAsync(Modified(_rug, (nameof(Rug.MetadataJson), """{"grade":"A"}""", """{"grade":"B"}""")));
    }

    [Fact]
    public async Task UnknownRug_IsComparedWithTheDatabase()
    {
        // وصل‌شده بی بارگذاری: فقط یادداشت عوض شده → آزاد
        _rug.Notes = "فقط یادداشت";
        await PassesAsync(Unknown(_rug));

        // همان، ولی محل بافت با دیتابیس فرق دارد → قفل
        _rug.Origin = "کاشان";
        await RejectedAsync(Unknown(_rug));
    }

    [Fact]
    public async Task RugsOutsideAFinalizedShipment_AreFree()
    {
        var open = NewRug();
        var stock = NewRug();
        _db.AddShipment(9, ShipmentStatus.Open, 1, open.Id);

        await PassesAsync(
            Modified(open, (nameof(Rug.Origin), "گبه", "کاشان")),
            Modified(stock, (nameof(Rug.Status), RugStatus.InProgress, RugStatus.Archived)));
    }

    [Fact]
    public async Task ManyRugs_AreResolvedWithOneLineQuery()
    {
        var others = Enumerable.Range(0, 20).Select(_ => NewRug()).ToList();
        await PassesAsync(others.Select(r => Modified(r, (nameof(Rug.Origin), "a", "b"))).ToArray());
        Assert.Equal(1, _db.LinesCalls);
    }

    // ── گردش کار و فروش ─────────────────────────────────────────────────

    [Fact]
    public async Task WorkflowAdvance_GoBack_AndPathEdit_AreRejected()
    {
        var step = new RugWorkflowStep { RugId = _rug.Id, Status = WorkflowStepStatus.InProgress, OrderIndex = 0 };
        await RejectedAsync(Modified(step, (nameof(RugWorkflowStep.Status), WorkflowStepStatus.InProgress, WorkflowStepStatus.Completed)));
        await RejectedAsync(Modified(step, (nameof(RugWorkflowStep.Status), WorkflowStepStatus.Completed, WorkflowStepStatus.InProgress)));
        await RejectedAsync(Added(new RugWorkflowStep { RugId = _rug.Id, OrderIndex = 1 }));
        await RejectedAsync(Deleted(step, (nameof(RugWorkflowStep.RugId), _rug.Id)));
        await RejectedAsync(Modified(step, (nameof(RugWorkflowStep.OrderIndex), 0, 2)));
    }

    [Fact]
    public async Task StepCostOnly_IsAllowed_LateProviderPricing()
    {
        var step = new RugWorkflowStep { RugId = _rug.Id, Status = WorkflowStepStatus.Completed };
        await PassesAsync(
            Modified(step, (nameof(RugWorkflowStep.CalculatedCost), null, 85_000m)),
            Modified(step, (nameof(RugWorkflowStep.Adjustment), null, 5_000m)));
    }

    [Fact]
    public async Task Sale_IsRejected()
    {
        var ex = await RejectedAsync(Added(new RugSale { RugId = _rug.Id, TenantId = TestTenant, BuyerName = "خریدار" }));
        Assert.Equal("rug.sale", ex.Field);
    }

    // ── نگله و خط ───────────────────────────────────────────────────────

    [Fact]
    public async Task BaleRenumberWeightCreateAndDelete_AreRejected()
    {
        var bale = new Bale { Id = _bale.Id, TenantId = TestTenant, ShipmentId = _shipment.Id, Number = 12 };
        var shipmentOf = (nameof(Bale.ShipmentId), (object?)_shipment.Id);

        Assert.Equal("bale", (await RejectedAsync(Modified(bale, [shipmentOf], (nameof(Bale.Number), 12, 1)))).Field);
        await RejectedAsync(Modified(bale, [shipmentOf], (nameof(Bale.WeightKg), null, 120.5m)));
        await RejectedAsync(Added(new Bale { TenantId = TestTenant, ShipmentId = _shipment.Id, Number = 13 }));
        await RejectedAsync(Deleted(bale, shipmentOf));
    }

    [Fact]
    public async Task BaleNotes_AreFree()
    {
        var bale = new Bale { Id = _bale.Id, TenantId = TestTenant, ShipmentId = _shipment.Id, Number = 12 };
        await PassesAsync(Modified(bale, [(nameof(Bale.ShipmentId), _shipment.Id)], (nameof(Bale.Notes), null, "طناب دوبل")));
    }

    [Fact]
    public async Task EveryLineOp_IsRejected()
    {
        var line = new ShipmentLine { TenantId = TestTenant, ShipmentId = _shipment.Id, BaleId = _bale.Id, RugId = _rug.Id, Seq = 1 };
        (string, object?)[] of = [(nameof(ShipmentLine.ShipmentId), _shipment.Id)];

        await RejectedAsync(Added(new ShipmentLine { TenantId = TestTenant, ShipmentId = _shipment.Id, BaleId = _bale.Id, RugId = Guid.NewGuid() }));
        await RejectedAsync(Deleted(line, of));                                                             // remove
        await RejectedAsync(Modified(line, of, (nameof(ShipmentLine.BaleId), Guid.NewGuid(), _bale.Id)));  // move
        await RejectedAsync(Modified(line, of, (nameof(ShipmentLine.Seq), 1, 2)));                           // order
        var rate = await RejectedAsync(Modified(line, of, (nameof(ShipmentLine.UsdPerM2Override), null, 50m)));
        Assert.Equal("line", rate.Field);
    }

    [Fact]
    public async Task MovingALineOutOfAFinalizedShipment_IsRejected_ByItsOriginalShipment()
    {
        var (open, openBale) = _db.AddShipment(9, ShipmentStatus.Open);
        var line = new ShipmentLine { TenantId = TestTenant, ShipmentId = open.Id, BaleId = openBale.Id, RugId = _rug.Id };
        await RejectedAsync(Modified(line,
            (nameof(ShipmentLine.ShipmentId), _shipment.Id, open.Id), (nameof(ShipmentLine.BaleId), _bale.Id, openBale.Id)));
    }

    // ── سربرگ ───────────────────────────────────────────────────────────

    public static TheoryData<string, object?, object?> LockedHeaderFields => new()
    {
        { nameof(Shipment.Title), "اظهار ۸", "اظهار تازه" },
        { nameof(Shipment.DocumentDate), new DateOnly(2026, 9, 6), new DateOnly(2026, 9, 7) },
        { nameof(Shipment.Destination), null, "هامبورگ" },
        { nameof(Shipment.BuyerName), null, "خریدار" },
        { nameof(Shipment.DeclarationNo), null, "۱۲۳" },
        { nameof(Shipment.CustomsOffice), null, "گمرک" },
        { nameof(Shipment.UsdRate), 105_000m, 106_000m }
    };

    [Theory]
    [MemberData(nameof(LockedHeaderFields))]
    public async Task EachLockedHeaderField_IsRejected(string field, object? original, object? current)
    {
        var shipment = FinalizedShipment();
        var ex = await RejectedAsync(Modified(shipment, WasFinalized, (field, original, current)));
        Assert.Equal(char.ToLowerInvariant(field[0]) + field[1..], ex.Field);
        Assert.Contains("محمولهٔ ۸ قطعی شده", ex.Message);
    }

    [Theory]
    [InlineData(nameof(Shipment.Notes))]
    [InlineData(nameof(Shipment.TargetBaleAreaM2))]
    [InlineData(nameof(Shipment.RevenueAmount))]
    [InlineData(nameof(Shipment.RevenueFx))]
    public async Task FreeHeaderFields_Pass(string field)
    {
        var shipment = FinalizedShipment();
        object? value = field == nameof(Shipment.Notes) ? "کرایه دیر رسید" : 13m;
        await PassesAsync(Modified(shipment, WasFinalized, (field, null, value)));
    }

    [Fact]
    public async Task DeletingAFinalizedShipment_IsRejected()
        => await RejectedAsync(Deleted(FinalizedShipment(), (nameof(Shipment.Status), ShipmentStatus.Finalized)));

    // ── «قطعی» و «باز کردن دوباره» خودشان ─────────────────────────────

    [Fact]
    public async Task FinalizeChangeSet_Passes()
    {
        var shipment = new Shipment { Id = Guid.NewGuid(), TenantId = TestTenant, Number = 9, Status = ShipmentStatus.Finalized };
        var rug = NewRug();
        _db.Shipments.Add(new ShipmentState(shipment.Id, 9, ShipmentStatus.Open));
        _db.Lines.Add(new LinePlacement(Guid.NewGuid(), rug.Id, shipment.Id, 9, ShipmentStatus.Open, Guid.NewGuid(), 1, false));

        await PassesAsync(
            Modified(shipment,
                (nameof(Shipment.Status), ShipmentStatus.Open, ShipmentStatus.Finalized),
                (nameof(Shipment.FinalizeCount), 0, 1),
                (nameof(Shipment.FinalizedAt), null, DateTimeOffset.UtcNow),
                (nameof(Shipment.SnapshotJson), null, "{}"),
                (nameof(Shipment.SnapshotHash), null, new string('a', 64))),
            Modified(rug, (nameof(Rug.Status), RugStatus.ReadyForSale, RugStatus.Exported)));
    }

    [Fact]
    public async Task ReopenChangeSet_Passes_ButNotWithAnotherHeaderChange()
    {
        var shipment = FinalizedShipment();
        EntityChange[] Reopen(params (string, object?, object?)[] extra) =>
        [
            Modified(shipment, [
                (nameof(Shipment.Status), ShipmentStatus.Finalized, ShipmentStatus.Open),
                (nameof(Shipment.SnapshotJson), "{}", null),
                (nameof(Shipment.SnapshotHash), new string('a', 64), null),
                (nameof(Shipment.LastSnapshotJson), null, "{}"),
                .. extra]),
            Modified(_rug, (nameof(Rug.Status), RugStatus.Exported, RugStatus.ReadyForSale))
        ];

        await PassesAsync(Reopen());
        await RejectedAsync(Reopen((nameof(Shipment.Title), "اظهار ۸", "دیگر")));
    }

    [Fact]
    public async Task ReopenOfAnotherShipment_DoesNotUnlockThisRug()
    {
        var (other, _) = _db.AddShipment(10, ShipmentStatus.Finalized);
        var otherShipment = new Shipment { Id = other.Id, TenantId = TestTenant, Number = 10, Status = ShipmentStatus.Open };
        await RejectedAsync(
            Modified(otherShipment, (nameof(Shipment.Status), ShipmentStatus.Finalized, ShipmentStatus.Open)),
            Modified(_rug, (nameof(Rug.Status), RugStatus.Exported, RugStatus.ReadyForSale)));
    }

    /// <summary>مقدار اصلیِ وضعیت (ChangeSetBuilder مقدار اصلی همهٔ ستون‌ها را می‌دهد، نه فقط عوض‌شده‌ها).</summary>
    private static readonly (string, object?)[] WasFinalized = [(nameof(Shipment.Status), ShipmentStatus.Finalized)];

    private Shipment FinalizedShipment()
        => new() { Id = _shipment.Id, TenantId = TestTenant, Number = 8, Status = ShipmentStatus.Finalized };
}
