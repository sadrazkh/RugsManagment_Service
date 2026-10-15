using RugsManagment.Application.Features.Shipments;
using RugsManagment.Application.Features.Shipments.Ledger;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using static RugsManagment.Tests.P4a.P4aChanges;

namespace RugsManagment.Tests.P4a;

/// <summary>
/// قاعده‌های عضویت (spec C.8 «BaleMembershipGuard»، C.3، C.10، B.13، L.1): یک خط برای هر فرش، خط فقط در محمولهٔ باز،
/// نگلهٔ خط مال همان محموله، فرشِ داخل نگله نه به سطل زباله می‌رود و نه فروخته می‌شود، نگله فقط خالی و محموله فقط باز و
/// بی‌خط حذف می‌شود.
/// </summary>
public class BaleMembershipGuardTests
{
    private readonly FakeShipmentLedgerRepository _db = new();
    private readonly BaleMembershipGuard _guard;

    public BaleMembershipGuardTests() => _guard = new BaleMembershipGuard(_db);

    private Task CheckAsync(params EntityChange[] changes) => _guard.CheckAsync(Context(changes), CancellationToken.None);

    private void Stock(Rug rug, bool hasSale = false) => _db.RugValues[rug.Id] = Stored(rug, hasSale);

    private static ShipmentLine NewLine(Guid shipmentId, Guid baleId, Guid rugId)
        => new() { TenantId = TestTenant, ShipmentId = shipmentId, BaleId = baleId, RugId = rugId, Seq = 1 };

    [Fact]
    public async Task AddingAStockRug_ToABaleOfTheSameOpenShipment_Passes()
    {
        var rug = NewRug();
        Stock(rug);
        var (shipment, bale) = _db.AddShipment(7, ShipmentStatus.Open, 3);
        await CheckAsync(Added(NewLine(shipment.Id, bale.Id, rug.Id)));
    }

    [Fact]
    public async Task ARugAlreadyInALine_CannotGetASecond()
    {
        var rug = NewRug();
        Stock(rug);
        _db.AddShipment(7, ShipmentStatus.Open, 3, rug.Id);
        var (other, otherBale) = _db.AddShipment(9, ShipmentStatus.Open, 1);

        var ex = await Assert.ThrowsAsync<DomainConflictException>(() => CheckAsync(Added(NewLine(other.Id, otherBale.Id, rug.Id))));
        Assert.Equal("این فرش در محمولهٔ ۷ / نگلهٔ ۳ است.", ex.Message);
    }

    [Fact]
    public async Task TwoNewLinesForOneRug_InOneSave_AreRejected()
    {
        var rug = NewRug();
        Stock(rug);
        var (shipment, bale) = _db.AddShipment(7, ShipmentStatus.Open, 3);
        await Assert.ThrowsAsync<DomainConflictException>(() => CheckAsync(
            Added(NewLine(shipment.Id, bale.Id, rug.Id)), Added(NewLine(shipment.Id, bale.Id, rug.Id))));
    }

    [Fact]
    public async Task MovingTheExistingLine_Passes()
    {
        var rug = NewRug();
        Stock(rug);
        var (shipment, bale) = _db.AddShipment(7, ShipmentStatus.Open, 3, rug.Id);
        var target = new BaleState(Guid.NewGuid(), shipment.Id, 4);
        _db.Bales.Add(target);
        var existing = _db.Lines.Single();
        var line = new ShipmentLine { Id = existing.LineId, TenantId = TestTenant, ShipmentId = shipment.Id, BaleId = target.Id, RugId = rug.Id };

        await CheckAsync(Modified(line, [(nameof(ShipmentLine.ShipmentId), shipment.Id)], (nameof(ShipmentLine.BaleId), bale.Id, target.Id)));
    }

    [Fact]
    public async Task ALineInAFinalizedShipment_IsALock()
    {
        var rug = NewRug();
        Stock(rug);
        var (shipment, bale) = _db.AddShipment(8, ShipmentStatus.Finalized, 1);
        var ex = await Assert.ThrowsAsync<ShipmentLockedException>(() => CheckAsync(Added(NewLine(shipment.Id, bale.Id, rug.Id))));
        Assert.Equal(8, ex.ShipmentNumber);
    }

    [Fact]
    public async Task TheLinesBale_MustBelongToTheLinesShipment()
    {
        var rug = NewRug();
        Stock(rug);
        var (shipment, _) = _db.AddShipment(7, ShipmentStatus.Open, 1);
        var (_, foreignBale) = _db.AddShipment(9, ShipmentStatus.Open, 1);

        var ex = await Assert.ThrowsAsync<DomainConflictException>(() => CheckAsync(Added(NewLine(shipment.Id, foreignBale.Id, rug.Id))));
        Assert.Equal("baleId", ex.Field);
    }

    [Fact]
    public async Task ABaleCreatedInTheSameSave_IsAccepted()
    {
        var rug = NewRug();
        Stock(rug);
        var (shipment, _) = _db.AddShipment(7, ShipmentStatus.Open, 1);
        var bale = new Bale { TenantId = TestTenant, ShipmentId = shipment.Id, Number = 2 };
        await CheckAsync(Added(bale), Added(NewLine(shipment.Id, bale.Id, rug.Id)));
    }

    [Fact]
    public async Task TrashedOrSoldRugs_CannotBeAdded()
    {
        var (shipment, bale) = _db.AddShipment(7, ShipmentStatus.Open, 1);

        var trashed = NewRug();
        trashed.DeletedAt = DateTimeOffset.UtcNow;
        Stock(trashed);
        Assert.Equal(Application.Common.Messages.LineRugTrashed.Text, (await Assert.ThrowsAsync<DomainConflictException>(
            () => CheckAsync(Added(NewLine(shipment.Id, bale.Id, trashed.Id))))).Message);

        var sold = NewRug();
        Stock(sold, hasSale: true);
        var ex = await Assert.ThrowsAsync<DomainConflictException>(() => CheckAsync(Added(NewLine(shipment.Id, bale.Id, sold.Id))));
        Assert.Equal("این فرش فروخته شده و در نگله نمی‌رود.", ex.Message);
    }

    [Fact]
    public async Task TrashingABaledRug_IsRefused_WithTheBaleNumber()
    {
        var rug = NewRug();
        _db.AddShipment(7, ShipmentStatus.Open, 12, rug.Id);
        rug.DeletedAt = DateTimeOffset.UtcNow;

        var ex = await Assert.ThrowsAsync<DomainConflictException>(
            () => CheckAsync(Modified(rug, (nameof(Rug.DeletedAt), null, rug.DeletedAt))));
        Assert.Equal("اول این فرش را از نگلهٔ ۱۲ خارج کنید.", ex.Message);
    }

    [Fact]
    public async Task SellingABaledRug_IsRefused()
    {
        var rug = NewRug();
        _db.AddShipment(7, ShipmentStatus.Open, 12, rug.Id);

        var ex = await Assert.ThrowsAsync<DomainConflictException>(
            () => CheckAsync(Added(new RugSale { TenantId = TestTenant, RugId = rug.Id, BuyerName = "خریدار" })));
        Assert.Equal("این فرش در نگلهٔ ۱۲ است؛ اول آن را از نگله خارج کنید.", ex.Message);
    }

    [Fact]
    public async Task RemovingTheLineAndTrashing_InOneSave_Passes()
    {
        var rug = NewRug();
        var (shipment, bale) = _db.AddShipment(7, ShipmentStatus.Open, 12, rug.Id);
        var existing = _db.Lines.Single();
        var line = new ShipmentLine { Id = existing.LineId, TenantId = TestTenant, ShipmentId = shipment.Id, BaleId = bale.Id, RugId = rug.Id };
        rug.DeletedAt = DateTimeOffset.UtcNow;

        await CheckAsync(
            Deleted(line, (nameof(ShipmentLine.ShipmentId), shipment.Id), (nameof(ShipmentLine.RugId), rug.Id)),
            Modified(rug, (nameof(Rug.DeletedAt), null, rug.DeletedAt)));
    }

    [Fact]
    public async Task ABaleIsDeletedOnlyWhenEmpty()
    {
        var rug = NewRug();
        var (shipment, full) = _db.AddShipment(7, ShipmentStatus.Open, 5, rug.Id);
        var empty = new BaleState(Guid.NewGuid(), shipment.Id, 6);
        _db.Bales.Add(empty);

        var ex = await Assert.ThrowsAsync<DomainConflictException>(() => CheckAsync(
            Deleted(new Bale { Id = full.Id, TenantId = TestTenant, ShipmentId = shipment.Id, Number = 5 }, (nameof(Bale.ShipmentId), shipment.Id))));
        Assert.Equal("نگلهٔ ۵ خالی نیست؛ اول فرش‌هایش را خارج کنید.", ex.Message);

        await CheckAsync(Deleted(new Bale { Id = empty.Id, TenantId = TestTenant, ShipmentId = shipment.Id, Number = 6 },
            (nameof(Bale.ShipmentId), shipment.Id)));
    }

    [Fact]
    public async Task AShipmentIsDeletedOnlyWhenOpenAndLineFree()
    {
        var rug = NewRug();
        var (withLines, _) = _db.AddShipment(7, ShipmentStatus.Open, 1, rug.Id);
        var (empty, _) = _db.AddShipment(9, ShipmentStatus.Open, 1);

        await Assert.ThrowsAsync<DomainConflictException>(() => CheckAsync(Deleted(
            new Shipment { Id = withLines.Id, TenantId = TestTenant, Number = 7 }, (nameof(Shipment.Status), ShipmentStatus.Open))));
        await Assert.ThrowsAsync<DomainConflictException>(() => CheckAsync(Deleted(
            new Shipment { Id = empty.Id, TenantId = TestTenant, Number = 9 }, (nameof(Shipment.Status), ShipmentStatus.Finalized))));

        await CheckAsync(Deleted(new Shipment { Id = empty.Id, TenantId = TestTenant, Number = 9 }, (nameof(Shipment.Status), ShipmentStatus.Open)));
    }

    [Fact]
    public async Task WithAdlTerm_TheRefusalSaysAdl()
    {
        _db.Settings = _db.Settings with { BaleTerm = BaleTerm.Adl };
        var rug = NewRug();
        _db.AddShipment(7, ShipmentStatus.Open, 12, rug.Id);
        rug.DeletedAt = DateTimeOffset.UtcNow;

        var ex = await Assert.ThrowsAsync<DomainConflictException>(
            () => CheckAsync(Modified(rug, (nameof(Rug.DeletedAt), null, rug.DeletedAt))));
        Assert.Equal("اول این فرش را از عدل ۱۲ خارج کنید.", ex.Message);
    }
}
