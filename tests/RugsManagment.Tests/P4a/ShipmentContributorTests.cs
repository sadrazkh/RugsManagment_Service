using RugsManagment.Application.Features.Shipments.Ledger;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using static RugsManagment.Tests.P4a.P4aChanges;

namespace RugsManagment.Tests.P4a;

/// <summary>
/// contributorهای فاز محموله روی ChangeSetهای ساختگی (spec D.5 ردیف‌های ۷، ۱۳ تا ۲۱ و ۲۶؛ L.1 «Impact contributors»): هر
/// ردیف همان خط‌ها، نگله‌ها و محموله‌های قدیم و تازه را در اثر می‌گذارد، و ردیف‌های «فقط قفل» (شماره و وزن نگله، سربرگ)
/// هیچ اثری ندارند.
/// </summary>
public class ShipmentContributorTests
{
    private readonly FakeShipmentLedgerRepository _db = new();

    private async Task<LedgerContext> RunAsync(IChangeImpactContributor contributor, params EntityChange[] changes)
    {
        var context = Context(changes);
        await contributor.ContributeAsync(context, CancellationToken.None);
        return context;
    }

    private Task<LedgerContext> ShipmentAsync(params EntityChange[] changes) => RunAsync(new ShipmentImpactContributor(_db), changes);

    // ── #13 خط: افزودن، انتقال، خارج کردن، ترتیب ──

    [Fact]
    public async Task LineAdded_ValuesTheLine_AndTouchesItsBaleShipmentAndRug()
    {
        var line = new ShipmentLine { TenantId = TestTenant, ShipmentId = Guid.NewGuid(), BaleId = Guid.NewGuid(), RugId = Guid.NewGuid() };
        var impact = (await ShipmentAsync(Added(line))).Impact;

        Assert.Equal([line.Id], impact.ShipmentLines);
        Assert.Equal([line.ShipmentId], impact.Shipments);
        Assert.Equal([line.BaleId], impact.Bales);
        Assert.Equal([line.RugId], impact.MemberRugs);
    }

    [Fact]
    public async Task LineMovedToAnotherShipment_ListsOldAndNewBaleAndShipment()
    {
        var (oldShipment, oldBale) = (Guid.NewGuid(), Guid.NewGuid());
        var line = new ShipmentLine { TenantId = TestTenant, ShipmentId = Guid.NewGuid(), BaleId = Guid.NewGuid(), RugId = Guid.NewGuid() };
        var impact = (await ShipmentAsync(Modified(line,
            (nameof(ShipmentLine.ShipmentId), oldShipment, line.ShipmentId), (nameof(ShipmentLine.BaleId), oldBale, line.BaleId)))).Impact;

        Assert.Contains(line.Id, impact.ShipmentLines);
        Assert.Equal(new HashSet<Guid> { oldBale, line.BaleId }, impact.Bales);
        Assert.Equal(new HashSet<Guid> { oldShipment, line.ShipmentId }, impact.Shipments);
        Assert.Contains(line.RugId, impact.MemberRugs);
    }

    [Fact]
    public async Task LineRemoved_PropagatesTheRugAsLineUsdChanged()
    {
        var line = new ShipmentLine { TenantId = TestTenant, ShipmentId = Guid.NewGuid(), BaleId = Guid.NewGuid(), RugId = Guid.NewGuid() };
        var context = await ShipmentAsync(Deleted(line,
            (nameof(ShipmentLine.RugId), line.RugId), (nameof(ShipmentLine.ShipmentId), line.ShipmentId),
            (nameof(ShipmentLine.BaleId), line.BaleId)));

        Assert.Empty(context.Impact.ShipmentLines); // ارزشش با cascade می‌رود
        Assert.Contains(line.ShipmentId, context.Impact.Shipments);
        Assert.Contains(line.BaleId, context.Impact.Bales);
        Assert.Contains(line.RugId, context.Impact.MemberRugs);
        Assert.Contains(line.RugId, context.LineUsdChangedRugs); // «به نسبت ارزش اظهاری»: وزن صفر
    }

    [Fact]
    public async Task LineReordered_HasNoImpact()
    {
        var line = new ShipmentLine { TenantId = TestTenant, ShipmentId = Guid.NewGuid(), BaleId = Guid.NewGuid(), RugId = Guid.NewGuid() };
        var context = await ShipmentAsync(Modified(line, (nameof(ShipmentLine.Seq), 1, 3)));
        Assert.True(context.Impact.IsEmpty);
    }

    // ── #14 نرخ دستی ──

    [Fact]
    public async Task LineOverride_RevaluesTheLine()
    {
        var line = new ShipmentLine { TenantId = TestTenant, ShipmentId = Guid.NewGuid(), BaleId = Guid.NewGuid(), RugId = Guid.NewGuid() };
        var impact = (await ShipmentAsync(Modified(line, (nameof(ShipmentLine.UsdPerM2Override), null, 50m)))).Impact;
        Assert.Equal([line.Id], impact.ShipmentLines);
        Assert.Equal([line.ShipmentId], impact.Shipments);
    }

    // ── #15، #15b، #19 ──

    [Theory]
    [InlineData(nameof(Shipment.UsdRate))]
    [InlineData(nameof(Shipment.RevenueAmount))]
    [InlineData(nameof(Shipment.RevenueFx))]
    public async Task UsdRateAndRevenue_TouchTheShipment(string field)
    {
        var shipment = new Shipment { TenantId = TestTenant, Number = 8 };
        var impact = (await ShipmentAsync(Modified(shipment, (field, null, 105_000m)))).Impact;
        Assert.Equal([shipment.Id], impact.Shipments);
        Assert.Empty(impact.ShipmentLines);
    }

    [Theory]
    [InlineData(nameof(Shipment.Title))]
    [InlineData(nameof(Shipment.DocumentDate))]
    [InlineData(nameof(Shipment.Destination))]
    [InlineData(nameof(Shipment.DeclarationNo))]
    [InlineData(nameof(Shipment.CustomsOffice))]
    [InlineData(nameof(Shipment.Notes))]
    public async Task HeaderFields_HaveNoImpact(string field)
    {
        var shipment = new Shipment { TenantId = TestTenant, Number = 8 };
        Assert.True((await ShipmentAsync(Modified(shipment, (field, null, "x")))).Impact.IsEmpty);
    }

    [Fact]
    public async Task BaleRenumberWeightAndNotes_HaveNoImpact()
    {
        var bale = new Bale { TenantId = TestTenant, ShipmentId = Guid.NewGuid(), Number = 2 };
        var context = await ShipmentAsync(
            Modified(bale, (nameof(Bale.Number), 3, 2), (nameof(Bale.WeightKg), null, 120m), (nameof(Bale.Notes), null, "x")));
        Assert.True(context.Impact.IsEmpty);
    }

    // ── #16 قطعی / باز کردن دوباره ──

    [Fact]
    public async Task Finalize_FreezesValues_ReopenRevaluesEveryLineOfTheShipment()
    {
        var shipment = new Shipment { TenantId = TestTenant, Number = 8, Status = ShipmentStatus.Finalized };
        var rugA = Guid.NewGuid();
        var rugB = Guid.NewGuid();
        _db.Lines.Add(new LinePlacement(Guid.NewGuid(), rugA, shipment.Id, 8, ShipmentStatus.Finalized, Guid.NewGuid(), 1, false));
        _db.Lines.Add(new LinePlacement(Guid.NewGuid(), rugB, shipment.Id, 8, ShipmentStatus.Finalized, Guid.NewGuid(), 2, true));

        var finalize = await ShipmentAsync(Modified(shipment, (nameof(Shipment.Status), ShipmentStatus.Open, ShipmentStatus.Finalized)));
        Assert.Empty(finalize.Impact.ShipmentLines);
        Assert.Equal([shipment.Id], finalize.Impact.Shipments);

        shipment.Status = ShipmentStatus.Open;
        var reopen = await ShipmentAsync(Modified(shipment, (nameof(Shipment.Status), ShipmentStatus.Finalized, ShipmentStatus.Open)));
        Assert.Equal(_db.Lines.Select(l => l.LineId).ToHashSet(), reopen.Impact.ShipmentLines);
    }

    // ── #17، #18 حذف ──

    [Fact]
    public async Task BaleAndShipmentDeleted_TouchThemForTheirCostTargets()
    {
        var shipmentId = Guid.NewGuid();
        var bale = new Bale { TenantId = TestTenant, ShipmentId = shipmentId, Number = 4 };
        var shipment = new Shipment { Id = shipmentId, TenantId = TestTenant, Number = 8 };
        var impact = (await ShipmentAsync(
            Deleted(bale, (nameof(Bale.ShipmentId), shipmentId)),
            Deleted(shipment, (nameof(Shipment.Status), ShipmentStatus.Open)))).Impact;

        Assert.Equal([bale.Id], impact.Bales);
        Assert.Equal([shipmentId], impact.Shipments);
    }

    // ── #2b، #3، #7 فرش داخل نگله ──

    [Theory]
    [InlineData(nameof(RugsManagment.Domain.Entities.Rug.Quantity))]
    [InlineData(nameof(RugsManagment.Domain.Entities.Rug.FinalLength))]
    [InlineData(nameof(RugsManagment.Domain.Entities.Rug.IntakeWidth))]
    [InlineData(nameof(RugsManagment.Domain.Entities.Rug.Origin))]
    [InlineData(nameof(RugsManagment.Domain.Entities.Rug.MetadataJson))]
    public async Task RugInAnOpenBale_ValueInputChanged_RevaluesItsLine(string field)
    {
        var rug = NewRug();
        _db.AddShipment(7, ShipmentStatus.Open, 1, rug.Id);
        var impact = (await ShipmentAsync(Modified(rug, (field, null, "x")))).Impact;
        Assert.Equal([_db.Lines.Single().LineId], impact.ShipmentLines);
    }

    [Fact]
    public async Task RugInAFinalizedShipment_IsFrozen_AndRugNotesHaveNoImpact()
    {
        var exported = NewRug();
        var open = NewRug();
        _db.AddShipment(8, ShipmentStatus.Finalized, 1, exported.Id);
        _db.AddShipment(9, ShipmentStatus.Open, 1, open.Id);

        var context = await ShipmentAsync(
            Modified(exported, (nameof(Rug.Origin), "گبه", "کاشان")),
            Modified(open, (nameof(Rug.Notes), null, "یادداشت")));
        Assert.True(context.Impact.IsEmpty);
        Assert.Equal(1, _db.LinesCalls); // یک پرس‌وجو، فقط برای فرشی که ورودی ارزش را عوض کرد
    }

    // ── #20 جدول ──

    [Fact]
    public async Task DeclaredValueRate_RevaluesOpenLinesWithThatKeyOnly()
    {
        var kashan = new OpenLineKey(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "كاشان ");
        var gabbeh = new OpenLineKey(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "گبه");
        _db.OpenLines.AddRange([kashan, gabbeh]);
        var rate = new DeclaredValueRate { TenantId = TestTenant, KeyValue = "کاشان", KeyNormalized = "کاشان", UsdPerM2 = 40m };

        var impact = (await RunAsync(new DeclaredValueImpactContributor(_db), Added(rate))).Impact;
        Assert.Equal([kashan.LineId], impact.ShipmentLines);
        Assert.Equal([kashan.ShipmentId], impact.Shipments);

        var removed = (await RunAsync(new DeclaredValueImpactContributor(_db),
            Deleted(rate, (nameof(DeclaredValueRate.KeyNormalized), "گبه")))).Impact;
        Assert.Equal([gabbeh.LineId], removed.ShipmentLines);
    }

    // ── #21 ترجیحات ──

    [Theory]
    [InlineData(nameof(TenantPreferences.DeclaredValueKeyField), true)]
    [InlineData(nameof(TenantPreferences.DeclaredValueFallbackUsd), true)]
    [InlineData(nameof(TenantPreferences.DeclaredValuesStamp), false)]
    [InlineData(nameof(TenantPreferences.BaleTargetTolerancePercent), false)]
    public async Task Preferences_KeyFieldAndFallback_RevalueEveryOpenLine(string field, bool revalues)
    {
        _db.OpenLines.Add(new OpenLineKey(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "گبه"));
        _db.OpenLines.Add(new OpenLineKey(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null));
        var preferences = TenantPreferences.CreateDefault(TestTenant);

        var impact = (await RunAsync(new PreferencesImpactContributor(_db), Modified(preferences, (field, null, "x")))).Impact;
        Assert.Equal(revalues ? 2 : 0, impact.ShipmentLines.Count);
    }

    // ── #26 فیلد کلید ──

    [Fact]
    public async Task KeyFieldGuard_RefusesDeactivatingTheCustomKeyField_ButNotOthers()
    {
        _db.Settings = _db.Settings with { KeyField = "rug.custom.grade" };
        var guard = new KeyFieldGuard(_db);
        var grade = new CustomFieldDefinition { TenantId = TestTenant, Key = "grade", Label = "درجه", IsActive = false };
        var other = new CustomFieldDefinition { TenantId = TestTenant, Key = "color", Label = "رنگ", IsActive = false };

        var ex = await Assert.ThrowsAsync<DomainConflictException>(() => guard.CheckAsync(
            Context(Modified(grade, (nameof(CustomFieldDefinition.IsActive), true, false))), CancellationToken.None));
        Assert.Contains("«درجه» کلید ارزش گمرکی است", ex.Message);

        await Assert.ThrowsAsync<DomainConflictException>(() => guard.CheckAsync(
            Context(Deleted(grade, (nameof(CustomFieldDefinition.Key), "grade"))), CancellationToken.None));

        await guard.CheckAsync(Context(Modified(other, (nameof(CustomFieldDefinition.IsActive), true, false))), CancellationToken.None);

        // همان ذخیره کلید را هم به محل بافت برمی‌گرداند → آزاد
        var preferences = TenantPreferences.CreateDefault(TestTenant);
        await guard.CheckAsync(Context(
            Modified(preferences, (nameof(TenantPreferences.DeclaredValueKeyField), "rug.custom.grade", "rug.origin")),
            Modified(grade, (nameof(CustomFieldDefinition.IsActive), true, false))), CancellationToken.None);
    }

    [Fact]
    public async Task KeyFieldGuard_IgnoresCustomFieldsWhenTheKeyIsABuiltInField()
    {
        var grade = new CustomFieldDefinition { TenantId = TestTenant, Key = "grade", Label = "درجه", IsActive = false };
        await new KeyFieldGuard(_db).CheckAsync(
            Context(Modified(grade, (nameof(CustomFieldDefinition.IsActive), true, false))), CancellationToken.None);
    }
}
