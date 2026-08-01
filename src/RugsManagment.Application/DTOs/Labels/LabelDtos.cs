using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.DTOs.Labels;

public record LabelTemplateDto(
    Guid Id,
    string Name,
    int WidthMm,
    int HeightMm,
    LabelMode Mode,
    string? ElementsJson,
    string? HtmlContent);

public record SaveLabelTemplateRequest(
    string Name,
    int WidthMm,
    int HeightMm,
    LabelMode Mode,
    string? ElementsJson,
    string? HtmlContent);
