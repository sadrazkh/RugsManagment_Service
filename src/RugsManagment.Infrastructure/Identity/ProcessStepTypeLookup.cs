using RugsManagment.Application.Services;
using RugsManagment.Domain.Entities;
using RugsManagment.Infrastructure.Persistence;

namespace RugsManagment.Infrastructure.Identity;

/// <summary>بارگذاری ProcessStepType از دیتابیس برای موتور مسیر سفارشی</summary>
public sealed class ProcessStepTypeLookup(AppDbContext db) : IProcessStepTypeLookup
{
    public async Task<ProcessStepType> GetRequiredAsync(Guid id, CancellationToken cancellationToken = default)
        => await db.ProcessStepTypes.FindAsync([id], cancellationToken)
            ?? throw new KeyNotFoundException($"نوع مرحله {id} یافت نشد.");
}
