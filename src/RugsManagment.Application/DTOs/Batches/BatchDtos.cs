namespace RugsManagment.Application.DTOs.Batches;

public record RugBatchDto(
    Guid Id,
    string Name,
    string? Description,
    DateTimeOffset? ReceivedAt,
    int RugCount,
    string? CurrentStepSummary);

public record CreateRugBatchRequest(string Name, string? Description, DateTimeOffset? ReceivedAt);

public record UpdateRugBatchRequest(string Name, string? Description, DateTimeOffset? ReceivedAt);

public record BatchRugIdsRequest(IReadOnlyList<Guid> RugIds);
