using RugsManagment.Domain.Enums;
namespace RugsManagment.Application.DTOs.Import;

public sealed record ImportDuplicateDto(Guid Id, int Number);
public sealed record ImportJobDto(Guid Id, int Number, string FileName, ImportStatus Status,
    IReadOnlyList<string> Sheets, int? HeaderRow, IReadOnlyDictionary<string, int>? Mapping, ImportMode Mode,
    string? TitleText, DateOnly? TitleDate, ImportDuplicateDto? DuplicateOf, DateTimeOffset CreatedAt, uint Version,
    PreviewImportRequest? LastPreview = null);
public sealed record ImportOptions(ImportMode Mode = ImportMode.Declaration,
    ExistingSerialPolicy ExistingSerialPolicy = ExistingSerialPolicy.Attach, bool ConvertCentimeters = false);
public sealed record PreviewImportRequest(string Sheet, IReadOnlyDictionary<string, int>? Mapping = null,
    ImportOptions? Options = null, IReadOnlyDictionary<int, Dictionary<string, string?>>? Overrides = null, int? HeaderRow = null,
    IReadOnlyList<int>? SkippedRows = null);
public sealed record CommitImportRequest(bool SaveMapping, uint Version);
public sealed record ImportIssueDto(string Code, string Severity, string Message, string? Field = null,
    IReadOnlyList<int>? RelatedRows = null);
public sealed record ImportCountsDto(int Lines, int Pieces, int Bales, decimal AreaM2, decimal UsdTotal,
    int Errors, int Warnings, int Attach, int Skip);
public sealed record ImportRowDto(int SheetRow, string Action, int? Bale, Dictionary<string, string?> Values,
    IReadOnlyList<ImportIssueDto> Issues, Guid? ExistingRugId = null, uint? ExistingVersion = null);
public sealed record IssueSummaryDto(string Code, string Severity, string Message, int Count);
public sealed record ReconciliationRowDto(int? Bale, int Pieces, int? FooterPieces, int? SummaryPieces,
    decimal Area, decimal? FooterArea, decimal? SummaryArea, decimal Usd, decimal? FooterUsd, decimal? SummaryUsd, bool Ok);
public sealed record ImportPreviewDto(ImportCountsDto Counts, IReadOnlyList<ReconciliationRowDto> Reconciliation,
    IReadOnlyList<ImportRowDto> Rows, IReadOnlyList<IssueSummaryDto> IssuesByCode,
    IReadOnlyDictionary<string, decimal> SuggestedRates, uint Version);
public sealed record ImportResultDto(int CreatedRugs, int AttachedRugs, Guid? ShipmentId,
    int? ShipmentNumber, Guid? BatchId, int SkippedRows);
public sealed record ImportUndoDto(bool Ok, IReadOnlyList<string> Blockers);
public sealed record ImportedRugState(Guid Id, uint Version, bool Created, decimal? FinalLength,
    decimal? FinalWidth, DateTimeOffset? FinalMeasuredAt, Guid? FinalMeasuredBy, DateTimeOffset? UpdatedAt = null);
public sealed record ImportUndoState(ImportResultDto Result, IReadOnlyList<ImportedRugState> Rugs,
    IReadOnlyList<Guid> BaleIds, IReadOnlyList<Guid> LineIds, ImportRelatedState? Related = null);
public sealed record ImportRelatedState(uint? ShipmentVersion, uint? BatchVersion,
    IReadOnlyDictionary<Guid, uint> BaleVersions, IReadOnlyDictionary<Guid, uint> LineVersions,
    IReadOnlyDictionary<Guid, string> RugChildren);
