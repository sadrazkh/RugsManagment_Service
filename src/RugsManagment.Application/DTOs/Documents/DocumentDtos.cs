using System.Text.Json;
using RugsManagment.Domain.Enums;
namespace RugsManagment.Application.DTOs.Documents;
public sealed record DocumentTemplateDto(Guid Id, string Name, DocumentKind Kind, bool IsDefault, int Revision, bool UsesMoneyFields, JsonElement Layout, uint Version);
public sealed record SaveDocumentTemplateRequest(string Name, JsonElement Layout, uint? Version = null);
public sealed record RevisionDto(int Revision, DateTimeOffset SavedAt, Guid? SavedBy);
public sealed record ExportRunDto(Guid Id, DateTimeOffset At, string User, string Template, int Revision, ExportFormat Format, bool IsDraft, string DataHash, string? FileSha256);
public sealed record ExpressionRequest(string? Expression = null, JsonElement? Layout = null, string? ColumnKey = null, Guid? ShipmentId = null,
    string? Expr = null, string? Kind = null, IReadOnlyList<string>? ColumnKeys = null, Guid? SampleShipmentId = null);
public sealed record PresetRequest(int? SerialStart = null, BaleTerm BaleTerm = BaleTerm.Negleh, bool ConfirmSampleRates = false);
public sealed record SetupStepDto(string Key, string Label, bool Required, bool Done, string Href, IReadOnlyList<string>? Missing = null);
public sealed record SetupStatusDto(bool Ready, int Remaining, IReadOnlyList<SetupStepDto> Steps);
