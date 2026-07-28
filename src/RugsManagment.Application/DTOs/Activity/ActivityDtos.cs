using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.DTOs.Activity;

public record ActivityEntryDto(
    Guid Id,
    string UserName,
    AuditAction Action,
    string ActionLabel,
    string EntityType,
    Guid? EntityId,
    string? EntityLabel,
    string Summary,
    DateTimeOffset CreatedAt);

public record ActivityQuery
{
    public AuditAction? Action { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 30;
}

public record ActivityPageDto(
    IReadOnlyList<ActivityEntryDto> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}
