using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.DTOs.Batches;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Services;

public interface IRugBatchService
{
    Task<IReadOnlyList<RugBatchDto>> ListAsync(Guid tenantId, CancellationToken ct = default);
    Task<RugBatchDto> CreateAsync(Guid tenantId, CreateRugBatchRequest request, CancellationToken ct = default);
    Task<RugBatchDto> UpdateAsync(Guid tenantId, Guid id, UpdateRugBatchRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task AssignRugsAsync(Guid tenantId, Guid batchId, BatchRugIdsRequest request, CancellationToken ct = default);
    Task RemoveRugsAsync(Guid tenantId, Guid batchId, BatchRugIdsRequest request, CancellationToken ct = default);
}

public sealed class RugBatchService(
    IRugBatchRepository batches,
    IRugRepository rugs,
    IUnitOfWork unitOfWork) : IRugBatchService
{
    public async Task<IReadOnlyList<RugBatchDto>> ListAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = await batches.ListByTenantAsync(tenantId, ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<RugBatchDto> CreateAsync(Guid tenantId, CreateRugBatchRequest request, CancellationToken ct = default)
    {
        var batch = new RugBatch
        {
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Description = request.Description,
            ReceivedAt = request.ReceivedAt ?? DateTimeOffset.UtcNow
        };
        await batches.AddAsync(batch, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(batch);
    }

    public async Task<RugBatchDto> UpdateAsync(Guid tenantId, Guid id, UpdateRugBatchRequest request, CancellationToken ct = default)
    {
        var batch = await batches.GetWithRugsAsync(id, tenantId, ct)
            ?? throw new KeyNotFoundException("گروه یافت نشد.");
        batch.Name = request.Name.Trim();
        batch.Description = request.Description;
        batch.ReceivedAt = request.ReceivedAt;
        batch.UpdatedAt = DateTimeOffset.UtcNow;
        batches.Update(batch);
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(batch);
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var batch = await batches.GetWithRugsAsync(id, tenantId, ct)
            ?? throw new KeyNotFoundException("گروه یافت نشد.");
        foreach (var rug in batch.Rugs)
        {
            rug.BatchId = null;
            rugs.Update(rug);
        }
        batches.Remove(batch);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task AssignRugsAsync(Guid tenantId, Guid batchId, BatchRugIdsRequest request, CancellationToken ct = default)
    {
        _ = await batches.GetByIdAsync(batchId, ct)
            ?? throw new KeyNotFoundException("گروه یافت نشد.");
        foreach (var rugId in request.RugIds.Distinct())
        {
            var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
                ?? throw new KeyNotFoundException($"فرش {rugId} یافت نشد.");
            rug.BatchId = batchId;
            rug.UpdatedAt = DateTimeOffset.UtcNow;
            rugs.Update(rug);
        }
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task RemoveRugsAsync(Guid tenantId, Guid batchId, BatchRugIdsRequest request, CancellationToken ct = default)
    {
        foreach (var rugId in request.RugIds.Distinct())
        {
            var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
                ?? throw new KeyNotFoundException($"فرش {rugId} یافت نشد.");
            if (rug.BatchId == batchId)
            {
                rug.BatchId = null;
                rug.UpdatedAt = DateTimeOffset.UtcNow;
                rugs.Update(rug);
            }
        }
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static RugBatchDto ToDto(RugBatch batch)
    {
        var stepGroups = batch.Rugs
            .Select(r => r.WorkflowSteps.FirstOrDefault(s => s.Status == WorkflowStepStatus.InProgress)?.ProcessStepType?.NameFa)
            .Where(n => n is not null)
            .GroupBy(n => n!)
            .Select(g => $"{g.Count()}× {g.Key}")
            .ToList();
        return new RugBatchDto(
            batch.Id,
            batch.Name,
            batch.Description,
            batch.ReceivedAt,
            batch.Rugs.Count,
            stepGroups.Count > 0 ? string.Join("، ", stepGroups) : null);
    }
}
