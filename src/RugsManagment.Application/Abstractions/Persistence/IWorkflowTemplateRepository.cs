using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Abstractions.Persistence;

/// <summary>قالب‌های مسیر هر کارگاه</summary>
public interface IWorkflowTemplateRepository : IRepository<WorkflowTemplate>
{
    Task<WorkflowTemplate?> GetWithStepsAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowTemplate>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
