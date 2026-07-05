using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.DTOs.CustomFields;

public record CustomFieldDefinitionDto(
    Guid Id,
    string Key,
    string Label,
    CustomFieldType FieldType,
    string? OptionsJson,
    bool IsRequired,
    int SortOrder,
    bool IsActive);

public record CreateCustomFieldRequest(
    string Key,
    string Label,
    CustomFieldType FieldType,
    string? OptionsJson,
    bool IsRequired,
    int SortOrder);

public record UpdateCustomFieldRequest(
    string Label,
    CustomFieldType FieldType,
    string? OptionsJson,
    bool IsRequired,
    int SortOrder,
    bool IsActive);
