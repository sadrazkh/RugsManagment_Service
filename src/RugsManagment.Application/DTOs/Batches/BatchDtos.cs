namespace RugsManagment.Application.DTOs.Batches;

public record RugBatchDto(
    Guid Id,
    string Name,
    string? Description,
    DateTimeOffset? ReceivedAt,
    int RugCount,
    string? CurrentStepSummary,
    // spec I.2: فروشنده و شمارهٔ ورودی؛ دنبالهٔ اختیاری تا سازنده‌های موضعیِ قدیمی نشکنند
    string? SellerName = null,
    int? Number = null);

public record CreateRugBatchRequest(string Name, string? Description, DateTimeOffset? ReceivedAt);

public record UpdateRugBatchRequest(string Name, string? Description, DateTimeOffset? ReceivedAt);

public record BatchRugIdsRequest(IReadOnlyList<Guid> RugIds);
