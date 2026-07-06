using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.DTOs.Labels;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Services;

/// <summary>مدیریت قالب‌های برچسب هر کارگاه — همیشه محدود به tenantId.</summary>
public interface ILabelTemplateService
{
    Task<IReadOnlyList<LabelTemplateDto>> ListAsync(Guid tenantId, CancellationToken ct = default);
    Task<LabelTemplateDto?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<LabelTemplateDto> CreateAsync(Guid tenantId, SaveLabelTemplateRequest request, CancellationToken ct = default);
    Task<LabelTemplateDto> UpdateAsync(Guid tenantId, Guid id, SaveLabelTemplateRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default);
}

public sealed class LabelTemplateService(
    IRepository<LabelTemplate> labels,
    IUnitOfWork unitOfWork) : ILabelTemplateService
{
    public async Task<IReadOnlyList<LabelTemplateDto>> ListAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = await labels.ListAsync(l => l.TenantId == tenantId, ct);
        return list.OrderBy(l => l.Name).Select(ToDto).ToList();
    }

    public async Task<LabelTemplateDto?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var label = await labels.GetByIdAsync(id, ct);
        return label is null || label.TenantId != tenantId ? null : ToDto(label);
    }

    public async Task<LabelTemplateDto> CreateAsync(Guid tenantId, SaveLabelTemplateRequest request, CancellationToken ct = default)
    {
        var label = new LabelTemplate { TenantId = tenantId };
        Apply(label, request);
        await labels.AddAsync(label, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(label);
    }

    public async Task<LabelTemplateDto> UpdateAsync(Guid tenantId, Guid id, SaveLabelTemplateRequest request, CancellationToken ct = default)
    {
        var label = await GetOwnedAsync(tenantId, id, ct);
        Apply(label, request);
        label.UpdatedAt = DateTimeOffset.UtcNow;
        labels.Update(label);
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(label);
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var label = await GetOwnedAsync(tenantId, id, ct);
        labels.Remove(label);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<LabelTemplate> GetOwnedAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var label = await labels.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("قالب برچسب یافت نشد.");
        if (label.TenantId != tenantId)
            throw new UnauthorizedAccessException("این قالب متعلق به کارگاه شما نیست.");
        return label;
    }

    private static void Apply(LabelTemplate label, SaveLabelTemplateRequest r)
    {
        label.Name = string.IsNullOrWhiteSpace(r.Name) ? "برچسب بدون نام" : r.Name.Trim();
        label.WidthMm = Math.Clamp(r.WidthMm, 20, 300);
        label.HeightMm = Math.Clamp(r.HeightMm, 20, 300);
        label.Mode = r.Mode;
        label.ElementsJson = r.ElementsJson;
        label.HtmlContent = r.HtmlContent;
    }

    private static LabelTemplateDto ToDto(LabelTemplate l) =>
        new(l.Id, l.Name, l.WidthMm, l.HeightMm, l.Mode, l.ElementsJson, l.HtmlContent);
}
