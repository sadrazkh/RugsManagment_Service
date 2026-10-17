using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Documents.Data;
using RugsManagment.Application.Features.Shipments;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
namespace RugsManagment.Infrastructure.Persistence.Repositories;
/// <summary>فقط دادهٔ همان کارگاه خوانده می‌شود؛ اظهار قطعی از عکس تغییرناپذیر ساخته می‌شود.</summary>
public sealed class DocumentDataRepository(AppDbContext db, IShipmentSettingsReader settings) : IDocumentDataRepository
{
    public async Task<DocumentSource> ShipmentAsync(Guid tenantId, Guid shipmentId, bool includeMoney, CancellationToken ct)
    {
        var shipment = await db.Set<Shipment>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == shipmentId && x.TenantId == tenantId, ct)
            ?? throw new KeyNotFoundException("محموله پیدا نشد.");
        if (shipment.IsFinalized)
        {
            if (shipment.SnapshotJson is null || shipment.SnapshotHash != SnapshotBuilder.Hash(shipment.SnapshotJson))
                throw new AppValidationException("عکس محموله معتبر نیست؛ ابتدا سلامت حساب‌ها را بررسی کنید.");
            var frozen = FromSnapshot(shipmentId, shipment.SnapshotJson);
            // عکس اظهار عمداً مبلغ داخلی ندارد؛ هزینهٔ دیر فقط ستون‌های پولی گزارش هزینه را از دفتر امروز می‌گیرد.
            if (includeMoney)
            {
                var totals = await MoneyAsync(tenantId,frozen.AllLines().Where(l => l.Rug.Id.HasValue).Select(l => l.Rug.Id!.Value).ToArray(),true,ct);
                frozen = frozen with { Bales = frozen.Bales.Select(b => b with { Lines = b.Lines.Select(l => l with {
                    Rug = l.Rug with { Money = l.Rug.Id is { } rugId ? totals.GetValueOrDefault(rugId) : null } }).ToArray() }).ToArray() };
            }
            return new DocumentSource(frozen, shipment.SnapshotHash);
        }
        var prefs = (await settings.GetAsync(tenantId, ct)).Preferences;
        var bales = await db.Set<Bale>().AsNoTracking().Where(x => x.ShipmentId == shipmentId && x.TenantId == tenantId).OrderBy(x => x.Number).ToListAsync(ct);
        var lines = await db.Set<ShipmentLine>().AsNoTracking().Include(x => x.Rug).Include(x => x.Value)
            .Where(x => x.TenantId == tenantId && x.ShipmentId == shipmentId && x.Rug.TenantId == tenantId && x.Rug.DeletedAt == null)
            .OrderBy(x => x.Seq).ThenBy(x => x.Id).ToListAsync(ct);
        var money = await MoneyAsync(tenantId, lines.Select(x => x.RugId).ToArray(), includeMoney, ct);
        var data = new DocumentData(new DocumentShipment(shipment.Id, shipment.Number, shipment.Title, shipment.DocumentDate,
            shipment.Destination, shipment.BuyerName, shipment.DeclarationNo, shipment.CustomsOffice, shipment.UsdRate),
            bales.Select(b => new DocumentBale(b.Number, lines.Where(l => l.BaleId == b.Id).Select(l => new DocumentLine(l.Seq,
                MapRug(l.Rug, money.GetValueOrDefault(l.RugId), $"در {BaleTerms.For(prefs.BaleTerm).BaleOf} {b.Number}"), l.Rug.LengthMeters, l.Rug.WidthMeters, l.Rug.AreaM2,
                l.Value?.UsdPerM2, l.Value?.PieceUsd, l.Value is null ? "missing" : SnapshotBuilder.SourceName(l.Value.RateSource))).ToArray())).ToArray(),
            [], BaleTerms.For(prefs.BaleTerm), Tenant(prefs), true);
        return new DocumentSource(data, SnapshotBuilder.Hash(JsonSerializer.Serialize(data)));
    }
    public async Task<DocumentSource> RugsAsync(Guid tenantId, IReadOnlyCollection<Guid> rugIds, bool includeMoney, CancellationToken ct)
    {
        if (rugIds.Count == 0 || rugIds.Count > 5000) throw new AppValidationException("از ۱ تا ۵۰۰۰ ردیف انتخاب کنید.");
        var ids = rugIds.Distinct().ToArray();
        var rugs = await db.Rugs.AsNoTracking().Where(x => x.TenantId == tenantId && ids.Contains(x.Id) && x.DeletedAt == null)
            .OrderBy(x => x.SerialSort).ThenBy(x => x.Id).ToListAsync(ct);
        if (rugs.Count != ids.Length) throw new KeyNotFoundException("فرش پیدا نشد.");
        var money = await MoneyAsync(tenantId, ids, includeMoney, ct);
        var prefs = (await settings.GetAsync(tenantId, ct)).Preferences;
        var placements = await db.Set<ShipmentLine>().AsNoTracking().Where(x => x.TenantId == tenantId && ids.Contains(x.RugId))
            .Select(x => new { x.RugId,x.Bale.Number,x.Shipment.Status }).ToListAsync(ct);
        var locations = placements.ToDictionary(x => x.RugId,x => x.Status == ShipmentStatus.Finalized ? "صادر شده" : $"در {BaleTerms.For(prefs.BaleTerm).BaleOf} {x.Number}");
        var sold = await db.Set<RugSale>().AsNoTracking().Where(x => x.Rug.TenantId == tenantId && ids.Contains(x.RugId)).Select(x => x.RugId).ToListAsync(ct);
        foreach (var id in sold) locations[id] = "فروخته شده";
        var data = new DocumentData(null, [], rugs.Select((r, i) => new DocumentLine(i + 1, MapRug(r, money.GetValueOrDefault(r.Id),locations.GetValueOrDefault(r.Id,"در انبار")), r.LengthMeters, r.WidthMeters, r.AreaM2)).ToArray(),
            BaleTerms.For(prefs.BaleTerm), Tenant(prefs), false);
        return new DocumentSource(data, SnapshotBuilder.Hash(JsonSerializer.Serialize(data)));
    }
    private async Task<Dictionary<Guid, IReadOnlyDictionary<string, decimal?>>> MoneyAsync(Guid tenantId, Guid[] ids, bool include, CancellationToken ct)
    {
        if (!include) return [];
        var totals = await db.Set<RugCostTotal>().AsNoTracking().Where(x => x.TenantId == tenantId && ids.Contains(x.RugId)).ToListAsync(ct);
        return totals.ToDictionary(x => x.RugId, x => (IReadOnlyDictionary<string, decimal?>)new Dictionary<string, decimal?>
        {
            ["rug.purchaseCost"] = x.PurchaseCost, ["rug.stepCost"] = x.StepCost, ["rug.expenseCost"] = x.ExpenseCost,
            ["rug.totalCost"] = x.Total, ["rug.costPerM2"] = x.CostPerM2, ["rug.costPerPiece"] = x.CostPerPiece, ["rug.revenueShare"] = x.ExportRevenueShare
        });
    }
    private static DocumentTenant Tenant(TenantPreferences p) => new(null, p.ExporterName, p.ExporterCode, p.ExporterAddress, p.ExporterPhone, p.CustomsOffice);
    private static DocumentRug MapRug(Rug r, IReadOnlyDictionary<string, decimal?>? money, string location = "در انبار") => new(r.Id, r.SerialNo, r.Quantity, r.Title, r.Pattern,
        r.Origin, r.Material, r.KnotDensity, r.IntakeLength, r.IntakeWidth, r.FinalLength, r.FinalWidth, r.Sku, r.Notes, location,
        string.IsNullOrWhiteSpace(r.MetadataJson) ? null : Custom(r.MetadataJson), money);
    private static IReadOnlyDictionary<string,string?> Custom(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name,p => p.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : p.Value.ToString());
    }
    public static DocumentData FromSnapshot(Guid shipmentId, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement; var s = root.GetProperty("shipment"); var t = root.GetProperty("terms"); var e = root.GetProperty("exporter");
        var header = new DocumentShipment(shipmentId, s.GetProperty("number").GetInt32(), Text(s,"title"), DateOnly.Parse(Text(s,"documentDate")!),
            Text(s,"destination"), Text(s,"buyerName"), Text(s,"declarationNo"), Text(s,"customsOffice"), Number(s,"usdRate"));
        var bales = root.GetProperty("bales").EnumerateArray().Select(b => new DocumentBale(b.GetProperty("number").GetInt32(),
            b.GetProperty("rows").EnumerateArray().Select(r => new DocumentLine(r.GetProperty("seq").GetInt32(),
                new DocumentRug(Guid.Parse(Text(r,"rugId")!), Text(r,"serial"), r.GetProperty("qty").GetInt32(), Text(r,"title"), Text(r,"pattern"), Text(r,"origin"), Text(r,"material"),
                    r.GetProperty("knotDensity").ValueKind == JsonValueKind.Null ? null : r.GetProperty("knotDensity").GetInt32(), Number(r,"intakeLength"), Number(r,"intakeWidth"),
                    Number(r,"finalLength"), Number(r,"finalWidth"), Text(r,"sku"), Custom: r.GetProperty("custom").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ValueKind == JsonValueKind.Null ? null : p.Value.ToString())),
                Number(r,"finalLength") ?? Number(r,"intakeLength"), Number(r,"finalWidth") ?? Number(r,"intakeWidth"), Number(r,"pieceArea"), Number(r,"usdPerM2"), Number(r,"pieceUsd"), Text(r,"rateSource"))).ToArray(),
            b.GetProperty("pieces").GetInt32(), Number(b,"areaM2"), Number(b,"usdTotal"))).ToArray();
        return new DocumentData(header, bales, [], new BaleTermWords(Text(t,"bale")!,Text(t,"bales")!,Text(t,"baleOf")!,Text(t,"baleFooter")!),
            new DocumentTenant(null,Text(e,"name"),Text(e,"code"),Text(e,"address"),Text(e,"phone"),Text(e,"customsOffice")), false);
    }
    private static string? Text(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.ValueKind != JsonValueKind.Null ? v.GetString() : null;
    private static decimal? Number(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.ValueKind != JsonValueKind.Null ? v.GetDecimal() : null;
}
