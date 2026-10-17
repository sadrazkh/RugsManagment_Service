namespace RugsManagment.Application.Abstractions.Persistence;
public sealed record ExportSetupState(bool Preset, bool Serials, bool HasRates, IReadOnlyList<string> MissingRates, bool Merchant, bool TestExport, bool SerialLabel);
public interface IExportSetupRepository
{
    Task<ExportSetupState> ReadAsync(Guid tenantId, string keyField, CancellationToken ct);
    Task<bool> HasGridAsync(Guid tenantId, string gridId, CancellationToken ct);
}
