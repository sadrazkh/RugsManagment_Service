using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.DTOs.CustomFields;
using RugsManagment.Application.Mapping;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Services;

/// <summary>مدیریت تعریف فیلدهای سفارشی هر کارگاه — همیشه محدود به tenantId.</summary>
public interface ICustomFieldService
{
    Task<IReadOnlyList<CustomFieldDefinitionDto>> ListAsync(Guid tenantId, bool onlyActive, CancellationToken ct = default);
    Task<CustomFieldDefinitionDto> CreateAsync(Guid tenantId, CreateCustomFieldRequest request, CancellationToken ct = default);
    Task<CustomFieldDefinitionDto> UpdateAsync(Guid tenantId, Guid id, UpdateCustomFieldRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default);
}

public sealed class CustomFieldService(
    IRepository<CustomFieldDefinition> fields,
    IUnitOfWork unitOfWork) : ICustomFieldService
{
    public async Task<IReadOnlyList<CustomFieldDefinitionDto>> ListAsync(Guid tenantId, bool onlyActive, CancellationToken ct = default)
    {
        var list = await fields.ListAsync(f => f.TenantId == tenantId && (!onlyActive || f.IsActive), ct);
        return list.OrderBy(f => f.SortOrder).ThenBy(f => f.Label).Select(f => f.ToDto()).ToList();
    }

    public async Task<CustomFieldDefinitionDto> CreateAsync(Guid tenantId, CreateCustomFieldRequest request, CancellationToken ct = default)
    {
        var key = NormalizeKey(request.Key);
        var existing = await fields.ListAsync(f => f.TenantId == tenantId && f.Key == key, ct);
        if (existing.Count > 0)
            throw new InvalidOperationException("فیلدی با این کلید قبلاً تعریف شده است.");

        var field = new CustomFieldDefinition
        {
            TenantId = tenantId,
            Key = key,
            Label = request.Label.Trim(),
            FieldType = request.FieldType,
            OptionsJson = request.OptionsJson,
            IsRequired = request.IsRequired,
            SortOrder = request.SortOrder,
            IsActive = true
        };
        await fields.AddAsync(field, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return field.ToDto();
    }

    public async Task<CustomFieldDefinitionDto> UpdateAsync(Guid tenantId, Guid id, UpdateCustomFieldRequest request, CancellationToken ct = default)
    {
        var field = await GetOwnedAsync(tenantId, id, ct);
        field.Label = request.Label.Trim();
        field.FieldType = request.FieldType;
        field.OptionsJson = request.OptionsJson;
        field.IsRequired = request.IsRequired;
        field.SortOrder = request.SortOrder;
        field.IsActive = request.IsActive;
        field.UpdatedAt = DateTimeOffset.UtcNow;
        fields.Update(field);
        await unitOfWork.SaveChangesAsync(ct);
        return field.ToDto();
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var field = await GetOwnedAsync(tenantId, id, ct);
        fields.Remove(field);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<CustomFieldDefinition> GetOwnedAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var field = await fields.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("فیلد یافت نشد.");
        if (field.TenantId != tenantId)
            throw new UnauthorizedAccessException("این فیلد متعلق به کارگاه شما نیست.");
        return field;
    }

    private static string NormalizeKey(string key)
    {
        var cleaned = new string(key.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        if (string.IsNullOrWhiteSpace(cleaned) || cleaned.All(c => c == '_'))
            throw new InvalidOperationException("کلید فیلد نامعتبر است.");
        return cleaned;
    }
}
