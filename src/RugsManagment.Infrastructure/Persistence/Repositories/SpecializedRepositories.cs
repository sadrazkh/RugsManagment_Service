using Microsoft.EntityFrameworkCore;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Infrastructure.Persistence.Repositories;

/// <summary>جستجوی کارگاه با slug — برای جلوگیری از تکرار هنگام ثبت مشتری جدید</summary>
public class TenantRepository(AppDbContext db) : Repository<Tenant>(db), ITenantRepository
{
    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
        => await Db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);
}

/// <summary>کاربر همراه Tenant — برای نمایش نام کارگاه در JWT و DTO</summary>
public class UserRepository(AppDbContext db) : Repository<User>(db), IUserRepository
{
    public override async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await Db.Users.Include(u => u.Tenant).FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => await Db.Users.Include(u => u.Tenant).FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
}

/// <summary>
/// فرش همراه تمام مراحل و نوع مرحله — برای صفحهٔ جزئیات و موتور WorkflowEngine.
/// </summary>
public class RugRepository(AppDbContext db) : Repository<Rug>(db), IRugRepository
{
    public async Task<Rug?> GetWithWorkflowAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        => await Db.Rugs
            .Include(r => r.Batch)
            .Include(r => r.WorkflowSteps.OrderBy(s => s.OrderIndex))
                .ThenInclude(s => s.ProcessStepType)
            .Include(r => r.WorkflowSteps)
                .ThenInclude(s => s.ServiceProvider)
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, cancellationToken);

    public async Task<IReadOnlyList<Rug>> ListByTenantAsync(
        Guid tenantId, RugStatus? status, CancellationToken cancellationToken = default)
    {
        var query = Db.Rugs
            .AsNoTracking()
            .Include(r => r.Batch)
            .Include(r => r.WorkflowSteps)
                .ThenInclude(s => s.ProcessStepType)
            .Where(r => r.TenantId == tenantId);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        return await query.OrderByDescending(r => r.CreatedAt).ToListAsync(cancellationToken);
    }

    /// <summary>SKU خودکار بر اساس تعداد فرش‌های همان کارگاه در ماه جاری</summary>
    public async Task<string> GenerateNextSkuAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var count = await Db.Rugs.CountAsync(r => r.TenantId == tenantId, cancellationToken);
        return $"RUG-{DateTime.UtcNow:yyyyMM}-{count + 1:D4}";
    }
}

public class WorkflowTemplateRepository(AppDbContext db) : Repository<WorkflowTemplate>(db), IWorkflowTemplateRepository
{
    public async Task<WorkflowTemplate?> GetWithStepsAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        => await Db.WorkflowTemplates
            .Include(t => t.Steps.OrderBy(s => s.OrderIndex))
                .ThenInclude(s => s.ProcessStepType)
            .Include(t => t.Steps)
                .ThenInclude(s => s.DefaultServiceProvider)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId, cancellationToken);

    public async Task<IReadOnlyList<WorkflowTemplate>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await Db.WorkflowTemplates
            .AsNoTracking()
            .Include(t => t.Steps.OrderBy(s => s.OrderIndex))
                .ThenInclude(s => s.ProcessStepType)
            .Where(t => t.TenantId == tenantId && t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
}

public class ProcessStepTypeRepository(AppDbContext db) : Repository<ProcessStepType>(db), IProcessStepTypeRepository
{
    public async Task<IReadOnlyList<ProcessStepType>> ListAllOrderedAsync(CancellationToken cancellationToken = default)
        => await Db.ProcessStepTypes.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync(cancellationToken);

    public async Task<ProcessStepType?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => await Db.ProcessStepTypes.FirstOrDefaultAsync(s => s.Code == code, cancellationToken);
}

public class RugBatchRepository(AppDbContext db) : Repository<RugBatch>(db), IRugBatchRepository
{
    public async Task<IReadOnlyList<RugBatch>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await Db.RugBatches.AsNoTracking()
            .Include(b => b.Rugs)
            .Where(b => b.TenantId == tenantId)
            .OrderByDescending(b => b.ReceivedAt ?? b.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<RugBatch?> GetWithRugsAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        => await Db.RugBatches
            .Include(b => b.Rugs)
                .ThenInclude(r => r.WorkflowSteps)
                    .ThenInclude(s => s.ProcessStepType)
            .FirstOrDefaultAsync(b => b.Id == id && b.TenantId == tenantId, cancellationToken);
}

public class ServiceProviderRepository(AppDbContext db) : Repository<ServiceProvider>(db), IServiceProviderRepository
{
    public async Task<IReadOnlyList<ServiceProvider>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await Db.ServiceProviders.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
}
