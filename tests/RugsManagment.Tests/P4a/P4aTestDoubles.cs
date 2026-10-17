using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Shipments.Ledger;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Tests.P4a;

/// <summary>
/// دیتابیسِ «پیش از این ذخیره» برای contributorها، قفل‌ها و مرحلهٔ محموله، در حافظه. هر خواندن شمرده می‌شود تا آزمون‌ها
/// بتوانند «یک پرس‌وجو برای هر ذخیره» (D.4 «Performance») را هم بسنجند.
/// </summary>
internal sealed class FakeShipmentLedgerRepository : IShipmentLedgerRepository
{
    public List<LinePlacement> Lines { get; } = [];
    public List<ShipmentState> Shipments { get; } = [];
    public List<BaleState> Bales { get; } = [];
    public Dictionary<Guid, RugLockedValues> RugValues { get; } = [];
    public List<string> ExportableKeys { get; } = [];
    public ShipmentLedgerSettings Settings { get; set; } = new(TenantPreferences.DefaultDeclaredValueKeyField, null, BaleTerm.Negleh);
    public Dictionary<string, decimal> Rates { get; } = new(StringComparer.Ordinal);
    public List<OpenLineKey> OpenLines { get; } = [];
    public List<LineValueInput> Inputs { get; } = [];
    public List<LineValueRow> Written { get; } = [];

    public int LinesCalls { get; private set; }
    public int StateCalls { get; private set; }
    public IReadOnlyCollection<Guid>? LastInputRequest { get; private set; }

    public Task<IReadOnlyList<LinePlacement>> LinesAsync(
        Guid tenantId, IReadOnlyCollection<Guid>? rugIds, IReadOnlyCollection<Guid>? baleIds, IReadOnlyCollection<Guid>? shipmentIds,
        CancellationToken cancellationToken)
    {
        LinesCalls++;
        IReadOnlyList<LinePlacement> result = Lines
            .Where(l => (rugIds?.Contains(l.RugId) ?? false) || (baleIds?.Contains(l.BaleId) ?? false)
                        || (shipmentIds?.Contains(l.ShipmentId) ?? false))
            .ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<OpenLineKey>> OpenLinesWithoutOverrideAsync(Guid tenantId, string keyField, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<OpenLineKey>>(OpenLines.ToList());

    public Task<IReadOnlyList<ShipmentState>> ShipmentStatesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> shipmentIds, CancellationToken cancellationToken)
    {
        StateCalls++;
        return Task.FromResult<IReadOnlyList<ShipmentState>>(Shipments.Where(s => shipmentIds.Contains(s.Id)).ToList());
    }

    public Task<IReadOnlyList<BaleState>> BaleStatesAsync(Guid tenantId, IReadOnlyCollection<Guid> baleIds, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<BaleState>>(Bales.Where(b => baleIds.Contains(b.Id)).ToList());

    public Task<IReadOnlyList<RugLockedValues>> RugValuesAsync(Guid tenantId, IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<RugLockedValues>>(rugIds.Where(RugValues.ContainsKey).Select(id => RugValues[id]).ToList());

    public Task<IReadOnlyList<string>> ExportableCustomKeysAsync(Guid tenantId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<string>>(ExportableKeys.ToList());

    public Task<ShipmentLedgerSettings> SettingsAsync(Guid tenantId, CancellationToken cancellationToken) => Task.FromResult(Settings);

    public Task<IReadOnlyDictionary<string, decimal>> RatesAsync(Guid tenantId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyDictionary<string, decimal>>(new Dictionary<string, decimal>(Rates, StringComparer.Ordinal));

    public async Task<(ShipmentLedgerSettings Settings, IReadOnlyDictionary<string, decimal> Rates)> ValuationSourcesAsync(
        Guid tenantId, CancellationToken cancellationToken)
        => (await SettingsAsync(tenantId, cancellationToken), await RatesAsync(tenantId, cancellationToken));

    public Task<IReadOnlyList<LineValueInput>> LineInputsAsync(
        Guid tenantId, IReadOnlyCollection<Guid>? lineIds, string keyField, CancellationToken cancellationToken)
    {
        LastInputRequest = lineIds;
        return Task.FromResult<IReadOnlyList<LineValueInput>>(
            Inputs.Where(i => lineIds is null || lineIds.Contains(i.LineId)).ToList());
    }

    public Task<int> WriteValuesAsync(Guid tenantId, IReadOnlyList<LineValueRow> rows, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Written.AddRange(rows);
        return Task.FromResult(rows.Count);
    }

    // ── ساختن «دیتابیس» ──

    /// <summary>یک محموله با یک نگله و (اختیاری) خط‌هایی برای این فرش‌ها.</summary>
    public (ShipmentState Shipment, BaleState Bale) AddShipment(int number, ShipmentStatus status, int baleNumber = 1, params Guid[] rugIds)
    {
        var shipment = new ShipmentState(Guid.NewGuid(), number, status);
        var bale = new BaleState(Guid.NewGuid(), shipment.Id, baleNumber);
        Shipments.Add(shipment);
        Bales.Add(bale);
        foreach (var rugId in rugIds)
            Lines.Add(new LinePlacement(Guid.NewGuid(), rugId, shipment.Id, number, status, bale.Id, baleNumber, false));
        return (shipment, bale);
    }
}

/// <summary>دیدن پول (بازرگان) یا ندیدنش (انباردار) برای متن پیام قفل.</summary>
internal sealed class FixedMoneyVisibility(bool canSee) : IMoneyVisibility
{
    public bool CanSeeMoney { get; } = canSee;
}

/// <summary>ساختن ChangeSet ساختگی (همان شکلی که ChangeSetBuilder می‌سازد).</summary>
internal static class P4aChanges
{
    public static readonly Guid TestTenant = Guid.Parse("00000000-0000-0000-0000-0000000004a1");

    public static EntityChange Added(object entity) => new(entity, ChangeKind.Added, TestTenant);

    public static EntityChange Unknown(object entity) => new(entity, ChangeKind.Unknown, TestTenant);

    /// <summary>حذف: مقدارهای اصلی همهٔ ستون‌های داده‌شده.</summary>
    public static EntityChange Deleted(object entity, params (string Name, object? Value)[] originals)
        => new(entity, ChangeKind.Deleted, TestTenant, null,
            originals.ToDictionary(o => o.Name, o => o.Value, StringComparer.Ordinal));

    public static EntityChange Modified(object entity, params (string Name, object? Original, object? Current)[] changes)
        => Modified(entity, [], changes);

    /// <summary>ویرایش، با مقدارهای اصلیِ ستون‌های دست‌نخورده (مثل ShipmentId یک خط).</summary>
    public static EntityChange Modified(
        object entity, (string Name, object? Value)[] unchanged, params (string Name, object? Original, object? Current)[] changes)
    {
        var originals = unchanged.ToDictionary(o => o.Name, o => o.Value, StringComparer.Ordinal);
        foreach (var change in changes)
            originals[change.Name] = change.Original;
        return new EntityChange(entity, ChangeKind.Modified, TestTenant,
            changes.ToDictionary(c => c.Name, c => new PropertyChange(c.Name, c.Original, c.Current), StringComparer.Ordinal),
            originals);
    }

    public static LedgerContext Context(params EntityChange[] changes)
        => new(TestTenant, new ChangeSet(changes), DateTimeOffset.UtcNow);

    public static Rug NewRug(decimal length = 2.08m, decimal width = 2.10m)
    {
        var rug = new Rug { TenantId = TestTenant, Sku = "RUG-P4A-" + Guid.NewGuid().ToString("N")[..6] };
        rug.SetIntakeSize(length, width);
        return rug;
    }

    /// <summary>مقدارهای «ذخیره‌شده» یک فرش برای سنجش تغییر نامعلوم.</summary>
    public static RugLockedValues Stored(Rug rug, bool hasSale = false)
        => new(rug.Id, hasSale, new Dictionary<string, object?>(ShipmentLockGuard.CurrentValues(rug)));
}
