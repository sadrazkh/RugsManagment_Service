using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Shipments;
using RugsManagment.Application.Features.Shipments;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Tests.P4a;

/// <summary>
/// جدول ارزش گمرکی (spec C.5، L.1 «DeclaredValue»): افزودن بازرگان فقط کلیدی را می‌پذیرد که بعد از KeyNormalize نیست،
/// نرخ منفی ۴۰۰ است، مهر کهنه ۴۰۹، و پاسخ می‌گوید چند خط باز با همان کلید دوباره ارزش‌گذاری شد. جایگزینی کامل همهٔ
/// ردیف‌ها را با هم می‌سنجد و فقط کلیدهای عوض‌شده را می‌شمارد.
/// </summary>
public class DeclaredValueServiceTests
{
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-0000-0000-0000000004a2");

    private readonly RateRepository _shipments = new();
    private readonly FakeTenantPreferencesRepository _preferences = new() { Row = TenantPreferences.CreateDefault(Tenant) };
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeAuditLog _audit = new();
    private readonly DeclaredValueService _service;

    public DeclaredValueServiceTests()
    {
        _preferences.Row!.DeclaredValuesStamp = 4;
        _service = new DeclaredValueService(_shipments, _preferences, new ShipmentSettingsReader(_preferences), _unitOfWork, _audit);
    }

    private void Existing(string key, decimal usd)
        => _shipments.Rates.Add(new DeclaredValueRate
        {
            TenantId = Tenant, KeyValue = key, KeyNormalized = DeclaredValueMath.KeyNormalize(key), UsdPerM2 = usd
        });

    [Fact]
    public async Task Add_RefusesAKeyThatAlreadyExists_AfterKeyNormalize()
    {
        Existing("نائين 9 لا", 50m);

        var ex = await Assert.ThrowsAsync<DomainConflictException>(
            () => _service.AddAsync(Tenant, new AddDeclaredValueRequest("نائین ۹ لا", 60m, 4)));

        Assert.Equal("keyValue", ex.Field);
        Assert.Contains("نائین 9 لا", ex.Message);
        Assert.Single(_shipments.Rates);
        Assert.Equal(4, _preferences.Row!.DeclaredValuesStamp);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact]
    public async Task Add_RejectsANegativeRate()
    {
        var ex = await Assert.ThrowsAsync<AppValidationException>(
            () => _service.AddAsync(Tenant, new AddDeclaredValueRequest("کاشان", -0.01m, 4)));

        Assert.Equal([Messages.DeclaredNegative.Text], ex.Errors["usdPerM2"]);
        Assert.Empty(_shipments.Rates);
    }

    [Fact]
    public async Task Add_WithAStaleStamp_IsAConflict_CarryingTheCurrentStamp()
    {
        var ex = await Assert.ThrowsAsync<StaleStampException>(
            () => _service.AddAsync(Tenant, new AddDeclaredValueRequest("کاشان", 40m, 3)));

        Assert.Equal(4, ex.CurrentStamp);
        Assert.Equal("stamp", ex.Field);
        Assert.Empty(_shipments.Rates);
    }

    [Fact]
    public async Task Add_ANewKey_BumpsTheStamp_AndCountsTheOpenLinesWithThatKey()
    {
        _shipments.OpenLineKeys.AddRange(["کاشان", "كاشان", " کاشان ", "قم", null]);

        var result = await _service.AddAsync(Tenant, new AddDeclaredValueRequest("  کاشان ", 40m, 4));

        Assert.Equal(new DeclaredValuesWriteResultDto(3, 5), result);
        var rate = Assert.Single(_shipments.Rates);
        Assert.Equal(("کاشان", "کاشان", 40m), (rate.KeyValue, rate.KeyNormalized, rate.UsdPerM2));
        Assert.Equal(5, _preferences.Row!.DeclaredValuesStamp);
        Assert.Equal(1, _unitOfWork.Saves);
        Assert.Equal(AuditAction.DeclaredValuesChanged, Assert.Single(_audit.Records).Action);
    }

    [Fact]
    public async Task Replace_ValidatesEveryRowTogether_AndRefusesADuplicateKey()
    {
        var ex = await Assert.ThrowsAsync<AppValidationException>(() => _service.ReplaceAsync(Tenant, new ReplaceDeclaredValuesRequest(
        [
            new DeclaredValueRateDto(null, "کاشان", 40m),
            new DeclaredValueRateDto(null, "كاشان", 50m),
            new DeclaredValueRateDto(null, "قم", -1m),
            new DeclaredValueRateDto(null, " ", 10m)
        ], 4)));

        Assert.Equal(["rates[1].keyValue", "rates[2].usdPerM2", "rates[3].keyValue"], ex.Errors.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact]
    public async Task Replace_AddsUpdatesAndRemoves_AndCountsOnlyTheLinesOfChangedKeys()
    {
        Existing("گبه", 45m);
        Existing("قم", 30m);
        Existing("تبریز", 20m);
        _shipments.OpenLineKeys.AddRange(["گبه", "گبه", "کاشان", "قم", "تبریز"]);

        var result = await _service.ReplaceAsync(Tenant, new ReplaceDeclaredValuesRequest(
        [
            new DeclaredValueRateDto(null, "گبه", 45m),      // بی تغییر
            new DeclaredValueRateDto(null, "تبریز", 25m),    // ویرایش
            new DeclaredValueRateDto(null, "کاشان", 60m)     // تازه
        ], 4));

        // کلیدهای عوض‌شده: تبریز، کاشان، قم (حذف) → سه خط باز
        Assert.Equal(new DeclaredValuesWriteResultDto(3, 5), result);
        Assert.Equal(["تبریز", "کاشان", "گبه"], _shipments.Rates.Select(r => r.KeyValue).Order(StringComparer.Ordinal));
        Assert.Equal(25m, _shipments.Rates.Single(r => r.KeyValue == "تبریز").UsdPerM2);
        Assert.Equal(1, _unitOfWork.Saves);
    }

    [Fact]
    public async Task Replace_WithAStaleStamp_IsAConflict()
    {
        await Assert.ThrowsAsync<StaleStampException>(() =>
            _service.ReplaceAsync(Tenant, new ReplaceDeclaredValuesRequest([new DeclaredValueRateDto(null, "گبه", 45m)], 5)));
        Assert.Empty(_shipments.Rates);
    }

    /// <summary>فقط آنچه سرویس جدول لازم دارد؛ بقیه پرتاب می‌کند.</summary>
    private sealed class RateRepository : IShipmentRepository
    {
        public List<DeclaredValueRate> Rates { get; } = [];
        public List<string?> OpenLineKeys { get; } = [];

        public Task<IReadOnlyList<DeclaredValueRate>> ListRatesTrackedAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DeclaredValueRate>>(Rates.Where(r => r.TenantId == tenantId).ToList());

        public Task<IReadOnlyList<DeclaredValueRate>> ListRatesAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => ListRatesTrackedAsync(tenantId, cancellationToken);

        public Task AddRateAsync(DeclaredValueRate rate, CancellationToken cancellationToken = default)
        {
            Rates.Add(rate);
            return Task.CompletedTask;
        }

        public void RemoveRate(DeclaredValueRate rate) => Rates.Remove(rate);

        public Task<IReadOnlyList<string?>> OpenLineKeyValuesAsync(Guid tenantId, string keyField, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string?>>(OpenLineKeys.ToList());

        // ── نالازم ──
        public Task<Shipment?> GetTrackedAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Bale?> GetBaleTrackedAsync(Guid tenantId, Guid baleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Bale?> FindBaleByNumberTrackedAsync(Guid tenantId, Guid shipmentId, int number, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Bale>> ListBalesTrackedAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ShipmentLine?> GetLineTrackedAsync(Guid tenantId, Guid lineId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ShipmentLine?> GetLineOfRugTrackedAsync(Guid tenantId, Guid rugId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ShipmentLine>> ListLinesOfBaleTrackedAsync(Guid tenantId, Guid baleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Rug>> ListLineRugsTrackedAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddShipmentAsync(Shipment shipment, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddBaleAsync(Bale bale, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddLineAsync(ShipmentLine line, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void RemoveShipment(Shipment shipment) => throw new NotSupportedException();
        public void RemoveBale(Bale bale) => throw new NotSupportedException();
        public void RemoveLine(ShipmentLine line) => throw new NotSupportedException();
        public Task<int> MaxBaleNumberAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> MaxSeqAsync(Guid tenantId, Guid baleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ShipmentHeaderData?> GetHeaderAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ShipmentListData>> ListAsync(Guid tenantId, ShipmentStatus? status, string keyField, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<BaleData>> ListBalesAsync(Guid tenantId, Guid shipmentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ShipmentRowData>> ListRowsAsync(Guid tenantId, Guid shipmentId, string keyField, Guid? baleId = null, IReadOnlyCollection<Guid>? lineIds = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RugLineState?> RugStateAsync(Guid tenantId, Guid rugId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RugPlacementData>> PlacementsAsync(Guid tenantId, IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> TenantsOfRugsAsync(IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CustomFieldInfo>> CustomFieldsAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ShipmentListData>> ListOpenForHomeAsync(Guid tenantId, string keyField, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<(int Pieces, decimal AreaM2)> StockAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
