using Microsoft.EntityFrameworkCore;
using RugsManagment.Application.Features.Import;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.Application.DTOs.Import;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace RugsManagment.Infrastructure.Persistence.Repositories;

public sealed class ImportJobRepository(AppDbContext db) : IImportJobRepository
{
    public Task<ImportJob?> FindAsync(Guid tenantId, Guid id, CancellationToken ct)
        => db.Set<ImportJob>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
    public async Task<IReadOnlyList<ImportJob>> ListAsync(Guid tenantId, CancellationToken ct)
        => await db.Set<ImportJob>().Where(x => x.TenantId == tenantId).OrderByDescending(x => x.Number).Take(200).ToListAsync(ct);
    public Task<ImportJob?> DuplicateAsync(Guid tenantId, string sha, Guid exceptId, CancellationToken ct)
        => db.Set<ImportJob>().Where(x => x.TenantId == tenantId && x.FileSha256 == sha && x.Id != exceptId).OrderByDescending(x => x.Number).FirstOrDefaultAsync(ct);
    public Task<ImportMappingPreset?> MappingAsync(Guid tenantId, string signature, CancellationToken ct)
        => db.Set<ImportMappingPreset>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.HeaderSignature == signature, ct);
    public async Task<IReadOnlyList<Rug>> RugsAsync(Guid tenantId, CancellationToken ct)
        => await db.Rugs.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).ToListAsync(ct);
    public async Task<IReadOnlyList<ShipmentLine>> LinesAsync(Guid tenantId, IReadOnlyList<Guid> rugIds, CancellationToken ct)
        => await db.Set<ShipmentLine>().IgnoreQueryFilters().Include(x => x.Shipment).Where(x => x.TenantId == tenantId && rugIds.Contains(x.RugId)).ToListAsync(ct);
    public async Task<IReadOnlyList<string>> UndoBlockersAsync(Guid tenantId, ImportJob job, IReadOnlyList<Guid> rugIds, IReadOnlyList<Guid> baleIds, CancellationToken ct)
    {
        var ids = await db.Set<CostEntryTarget>().Where(t => t.TenantId == tenantId &&
            (t.RugId != null && rugIds.Contains(t.RugId.Value) || t.BaleId != null && baleIds.Contains(t.BaleId.Value)))
            .Select(t => t.EntryId).ToListAsync(ct);
        ids.AddRange(await db.Set<CostEntryPin>().Where(t => t.TenantId == tenantId && rugIds.Contains(t.RugId)).Select(t => t.EntryId).ToListAsync(ct));
        var count = await db.Set<CostEntry>().CountAsync(e => e.TenantId == tenantId && e.DeletedAt == null &&
            (ids.Contains(e.Id) || job.ShipmentId != null && e.ShipmentId == job.ShipmentId || job.BatchId != null && e.BatchId == job.BatchId), ct);
        var blockers = new List<string>();
        if (count > 0) blockers.Add($"{Application.Common.PersianText.ToPersianDigits(count.ToString(System.Globalization.CultureInfo.InvariantCulture))} هزینه به این ورود وصل است.");
        if (job.ShipmentId is { } sid && await db.Set<Shipment>().AnyAsync(s => s.TenantId == tenantId && s.Id == sid && s.Status != ShipmentStatus.Open, ct))
            blockers.Add("محموله قطعی شده است؛ اول آن را دوباره باز کنید.");
        if (job.ShipmentId is { } shipment && await db.Set<ShipmentLine>().IgnoreQueryFilters().AnyAsync(l => l.TenantId == tenantId && l.ShipmentId == shipment && !rugIds.Contains(l.RugId), ct))
            blockers.Add("پس از ورود، فرش دیگری به محموله اضافه شده است.");
        if (job.BatchId is { } batch && await db.Rugs.IgnoreQueryFilters().AnyAsync(r => r.TenantId == tenantId && r.BatchId == batch && !rugIds.Contains(r.Id), ct))
            blockers.Add("پس از ورود، فرش دیگری به این ورودی اضافه شده است.");
        return blockers;
    }
    public async Task AddAsync<T>(T entity, CancellationToken ct) where T : BaseEntity => await db.Set<T>().AddAsync(entity, ct);
    public void Remove<T>(T entity) where T : BaseEntity => db.Set<T>().Remove(entity);
    public Task<Bale?> BaleAsync(Guid tenantId, Guid id, CancellationToken ct)
        => db.Set<Bale>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
    public Task<Shipment?> ShipmentAsync(Guid tenantId, Guid id, CancellationToken ct)
        => db.Set<Shipment>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
    public Task<RugBatch?> BatchAsync(Guid tenantId, Guid id, CancellationToken ct)
        => db.RugBatches.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
    public async Task LockUndoAsync(Guid tenantId, ImportJob job, IReadOnlyList<Guid> rugIds, CancellationToken ct)
    {
        var ids = rugIds.ToArray();
        // قفل فرش، افزودن فرزند را هم تا پایان برگشت نگه می‌دارد (قفل FK). قفل فرزند مانع ویرایش هم‌زمان آن است.
        await db.Database.SqlQuery<Guid>($"""SELECT "Id" AS "Value" FROM "Rugs" WHERE "TenantId"={tenantId} AND "Id"=ANY({ids}) ORDER BY "Id" FOR UPDATE""").ToListAsync(ct);
        await db.Database.SqlQuery<Guid>($"""SELECT "Id" AS "Value" FROM "RugImages" WHERE "TenantId"={tenantId} AND "RugId"=ANY({ids}) ORDER BY "Id" FOR UPDATE""").ToListAsync(ct);
        await db.Database.SqlQuery<Guid>($"""SELECT "Id" AS "Value" FROM "RugWorkflowSteps" WHERE "RugId"=ANY({ids}) AND "RugId" IN (SELECT "Id" FROM "Rugs" WHERE "TenantId"={tenantId}) ORDER BY "Id" FOR UPDATE""").ToListAsync(ct);
        await db.Database.SqlQuery<Guid>($"""SELECT "Id" AS "Value" FROM "RugPurchaseLines" WHERE "TenantId"={tenantId} AND "RugId"=ANY({ids}) ORDER BY "Id" FOR UPDATE""").ToListAsync(ct);
        await db.Database.SqlQuery<Guid>($"""SELECT "Id" AS "Value" FROM "RugSales" WHERE "TenantId"={tenantId} AND "RugId"=ANY({ids}) ORDER BY "Id" FOR UPDATE""").ToListAsync(ct);
        if (job.ShipmentId is { } shipment)
        {
            await db.Database.SqlQuery<Guid>($"""SELECT "Id" AS "Value" FROM "Shipments" WHERE "TenantId"={tenantId} AND "Id"={shipment} FOR UPDATE""").ToListAsync(ct);
            await db.Database.SqlQuery<Guid>($"""SELECT "Id" AS "Value" FROM "Bales" WHERE "TenantId"={tenantId} AND "ShipmentId"={shipment} ORDER BY "Id" FOR UPDATE""").ToListAsync(ct);
            await db.Database.SqlQuery<Guid>($"""SELECT "Id" AS "Value" FROM "ShipmentLines" WHERE "TenantId"={tenantId} AND "ShipmentId"={shipment} ORDER BY "Id" FOR UPDATE""").ToListAsync(ct);
        }
        if (job.BatchId is { } batch)
            await db.Database.SqlQuery<Guid>($"""SELECT "Id" AS "Value" FROM "RugBatches" WHERE "TenantId"={tenantId} AND "Id"={batch} FOR UPDATE""").ToListAsync(ct);
    }
    public async Task<ImportRelatedState> RelatedAsync(Guid tenantId, ImportJob job, IReadOnlyList<Guid> rugIds, CancellationToken ct)
    {
        uint? shipment = null; uint? batch = null;
        if (job.ShipmentId is { } sid && await ShipmentAsync(tenantId, sid, ct) is { } s) shipment = Version(s);
        if (job.BatchId is { } bid && await BatchAsync(tenantId, bid, ct) is { } b) batch = Version(b);
        var bales = job.ShipmentId is { } sh ? await db.Set<Bale>().Where(b => b.TenantId == tenantId && b.ShipmentId == sh).ToListAsync(ct) : [];
        var lines = await db.Set<ShipmentLine>().IgnoreQueryFilters().Where(l => l.TenantId == tenantId && rugIds.Contains(l.RugId)).ToListAsync(ct);
        var children = rugIds.ToDictionary(id => id, _ => new List<string>());
        void Add<T>(IEnumerable<T> entities, Func<T, Guid> parent) where T : BaseEntity
        {
            foreach (var entity in entities)
            {
                var scalars = db.Entry(entity).Metadata.GetProperties().Where(p => p.PropertyInfo != null)
                    .OrderBy(p => p.Name, StringComparer.Ordinal).ToDictionary(p => p.Name, p => p.PropertyInfo!.GetValue(entity));
                children[parent(entity)].Add(typeof(T).Name + ":" + JsonSerializer.Serialize(scalars));
            }
        }
        Add(await db.RugImages.AsNoTracking().IgnoreQueryFilters().Where(x => x.TenantId == tenantId && rugIds.Contains(x.RugId)).ToListAsync(ct), x => x.RugId);
        Add(await db.RugWorkflowSteps.AsNoTracking().IgnoreQueryFilters().Where(x => x.Rug.TenantId == tenantId && rugIds.Contains(x.RugId)).ToListAsync(ct), x => x.RugId);
        Add(await db.Set<RugPurchaseLine>().AsNoTracking().IgnoreQueryFilters().Where(x => x.TenantId == tenantId && rugIds.Contains(x.RugId)).ToListAsync(ct), x => x.RugId);
        Add(await db.RugSales.AsNoTracking().IgnoreQueryFilters().Where(x => x.TenantId == tenantId && rugIds.Contains(x.RugId)).ToListAsync(ct), x => x.RugId);
        return new(shipment, batch, bales.ToDictionary(x => x.Id, Version), lines.ToDictionary(x => x.Id, Version),
            children.ToDictionary(x => x.Key, x => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", x.Value.Order(StringComparer.Ordinal)))))));
    }
    private uint Version(object entity) => (uint)(db.Entry(entity).Property("xmin").CurrentValue ?? 0u);
}
