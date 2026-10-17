namespace RugsManagment.Application.Abstractions.Services;
public interface IImportFileStorage
{
    Task<string> SaveAsync(Guid tenantId, Guid jobId, Stream input, CancellationToken ct = default);
    Task<Stream> OpenAsync(Guid tenantId, Guid jobId, CancellationToken ct = default);
    Task DeleteAsync(Guid tenantId, Guid jobId, CancellationToken ct = default);
}
