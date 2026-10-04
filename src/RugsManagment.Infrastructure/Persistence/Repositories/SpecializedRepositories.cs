using Microsoft.EntityFrameworkCore;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Infrastructure.Persistence.Repositories;

/// <summary>جستجوی کارگاه با slug — برای جلوگیری از تکرار هنگام ثبت مشتری جدید</summary>
public class TenantRepository(AppDbContext db) : Repository<Tenant>(db), ITenantRepository
{
    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
        => await Db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);
}

/// <summary>کاربر همراه Tenant — برای نمایش نام کارگاه در کوکی ورود و DTO</summary>
public class UserRepository(AppDbContext db) : Repository<User>(db), IUserRepository
{
    public override async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await Db.Users.Include(u => u.Tenant).FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => await Db.Users.Include(u => u.Tenant).FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
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

    public async Task<IReadOnlyList<ProcessStepType>> ListForTenantAsync(
        Guid tenantId, bool onlyActive = true, CancellationToken cancellationToken = default)
    {
        var query = Db.ProcessStepTypes
            .AsNoTracking()
            // سیستمی (بدون کارگاه) + اختصاصی همین کارگاه
            .Where(s => s.TenantId == null || s.TenantId == tenantId);

        if (onlyActive)
            query = query.Where(s => s.IsActive);

        return await query
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.NameFa)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProcessStepType?> GetForTenantAsync(
        Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        => await Db.ProcessStepTypes
            .FirstOrDefaultAsync(
                s => s.Id == id && (s.TenantId == null || s.TenantId == tenantId),
                cancellationToken);

    /// <summary>فقط برای مرحله‌های سیستمی — در seed استفاده می‌شود.</summary>
    public async Task<ProcessStepType?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => await Db.ProcessStepTypes.FirstOrDefaultAsync(
            s => s.Code == code && s.TenantId == null, cancellationToken);
}
