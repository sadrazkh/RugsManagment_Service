using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Abstractions.Persistence;

/// <summary>کاتالوگ انواع مرحله (سیستمی)</summary>
public interface IProcessStepTypeRepository : IRepository<ProcessStepType>
{
    Task<IReadOnlyList<ProcessStepType>> ListAllOrderedAsync(CancellationToken cancellationToken = default);
    Task<ProcessStepType?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
}
