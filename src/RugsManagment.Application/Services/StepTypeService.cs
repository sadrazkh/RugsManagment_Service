using System.Text.Json;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Workflows;
using RugsManagment.Application.Mapping;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Services;

/// <summary>
/// کاتالوگ انواع مرحلهٔ یک کارگاه.
///
/// مرحله‌های سیستمی (قالیشویی، رفوگری، …) برای همه مشترک‌اند و کارگاه فقط می‌بیندشان؛
/// در کنارشان می‌تواند مرحله‌های اختصاصی خودش را بسازد و ویرایش کند.
/// </summary>
public interface IStepTypeService
{
    Task<IReadOnlyList<ProcessStepTypeDto>> ListAsync(
        Guid tenantId, bool onlyActive = true, CancellationToken ct = default);

    Task<ProcessStepTypeDto> CreateAsync(
        Guid tenantId, SaveProcessStepTypeRequest request, CancellationToken ct = default);

    Task<ProcessStepTypeDto> UpdateAsync(
        Guid tenantId, Guid id, SaveProcessStepTypeRequest request, CancellationToken ct = default);

    /// <summary>حذف مرحلهٔ اختصاصی؛ اگر جایی استفاده شده باشد فقط غیرفعال می‌شود.</summary>
    Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default);
}

public sealed class StepTypeService(
    IProcessStepTypeRepository stepTypes,
    IRepository<WorkflowTemplateStep> templateSteps,
    IRepository<RugWorkflowStep> rugSteps,
    IAuditLog audit,
    IUnitOfWork unitOfWork) : IStepTypeService
{
    public async Task<IReadOnlyList<ProcessStepTypeDto>> ListAsync(
        Guid tenantId, bool onlyActive = true, CancellationToken ct = default)
    {
        var list = await stepTypes.ListForTenantAsync(tenantId, onlyActive, ct);
        return list.Select(t => t.ToDto()).ToList();
    }

    public async Task<ProcessStepTypeDto> CreateAsync(
        Guid tenantId, SaveProcessStepTypeRequest request, CancellationToken ct = default)
    {
        Validate(request);

        var existing = await stepTypes.ListForTenantAsync(tenantId, onlyActive: false, ct);
        var name = PersianText.Normalize(request.NameFa.Trim());

        if (existing.Any(s => string.Equals(s.NameFa, name, StringComparison.Ordinal)))
            throw new InvalidOperationException("مرحله‌ای با همین نام از قبل وجود دارد.");

        var stepType = new ProcessStepType
        {
            TenantId = tenantId,
            // کد از نام ساخته می‌شود؛ کاربر کارگاه نباید درگیر کد انگلیسی شود
            Code = await GenerateCodeAsync(tenantId, name, existing, ct),
            NameFa = name,
            NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? name : request.NameEn.Trim(),
            Icon = string.IsNullOrWhiteSpace(request.Icon) ? "workflow" : request.Icon.Trim(),
            SortOrder = request.SortOrder,
            DefaultPricingModel = request.DefaultPricingModel,
            DefaultUnitRate = request.DefaultUnitRate,
            ExpectedDurationDays = request.ExpectedDurationDays,
            FieldSchemaJson = NormalizeSchema(request.FieldSchemaJson),
            IsActive = request.IsActive
        };

        await stepTypes.AddAsync(stepType, ct);
        audit.Record(AuditAction.Created, nameof(ProcessStepType), stepType.Id,
            $"نوع مرحلهٔ «{stepType.NameFa}» ساخته شد.", stepType.NameFa);

        await unitOfWork.SaveChangesAsync(ct);
        return stepType.ToDto();
    }

    public async Task<ProcessStepTypeDto> UpdateAsync(
        Guid tenantId, Guid id, SaveProcessStepTypeRequest request, CancellationToken ct = default)
    {
        Validate(request);

        var stepType = await stepTypes.GetForTenantAsync(id, tenantId, ct)
            ?? throw new KeyNotFoundException("نوع مرحله یافت نشد.");

        EnsureOwnedByTenant(stepType);

        stepType.NameFa = PersianText.Normalize(request.NameFa.Trim());
        stepType.NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? stepType.NameFa : request.NameEn.Trim();
        stepType.Icon = string.IsNullOrWhiteSpace(request.Icon) ? "workflow" : request.Icon.Trim();
        stepType.SortOrder = request.SortOrder;
        stepType.DefaultPricingModel = request.DefaultPricingModel;
        stepType.DefaultUnitRate = request.DefaultUnitRate;
        stepType.ExpectedDurationDays = request.ExpectedDurationDays;
        stepType.FieldSchemaJson = NormalizeSchema(request.FieldSchemaJson);
        stepType.IsActive = request.IsActive;
        stepType.UpdatedAt = DateTimeOffset.UtcNow;

        stepTypes.Update(stepType);
        audit.Record(AuditAction.Updated, nameof(ProcessStepType), stepType.Id,
            $"نوع مرحلهٔ «{stepType.NameFa}» ویرایش شد.", stepType.NameFa);

        await unitOfWork.SaveChangesAsync(ct);
        return stepType.ToDto();
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var stepType = await stepTypes.GetForTenantAsync(id, tenantId, ct)
            ?? throw new KeyNotFoundException("نوع مرحله یافت نشد.");

        EnsureOwnedByTenant(stepType);

        // اگر در قالبی یا روی فرشی استفاده شده، حذف فیزیکی تاریخچه را می‌شکند
        var usedInTemplates = (await templateSteps.ListAsync(s => s.ProcessStepTypeId == id, ct)).Count;
        var usedOnRugs = (await rugSteps.ListAsync(s => s.ProcessStepTypeId == id, ct)).Count;

        if (usedInTemplates > 0 || usedOnRugs > 0)
        {
            stepType.IsActive = false;
            stepType.UpdatedAt = DateTimeOffset.UtcNow;
            stepTypes.Update(stepType);

            audit.Record(AuditAction.Updated, nameof(ProcessStepType), stepType.Id,
                $"نوع مرحلهٔ «{stepType.NameFa}» چون در حال استفاده بود غیرفعال شد (نه حذف).", stepType.NameFa);
        }
        else
        {
            stepTypes.Remove(stepType);
            audit.Record(AuditAction.Deleted, nameof(ProcessStepType), stepType.Id,
                $"نوع مرحلهٔ «{stepType.NameFa}» حذف شد.", stepType.NameFa);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    // ─────────────────────────────────────────────────────────

    private static void EnsureOwnedByTenant(ProcessStepType stepType)
    {
        if (stepType.TenantId is null)
            throw new InvalidOperationException(
                "مرحله‌های پیش‌فرض سامانه قابل ویرایش نیستند. برای تغییر، یک مرحلهٔ اختصاصی بسازید.");
    }

    private static void Validate(SaveProcessStepTypeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NameFa))
            throw new InvalidOperationException("نام مرحله الزامی است.");

        if (request.DefaultUnitRate < 0)
            throw new InvalidOperationException("نرخ پیش‌فرض نمی‌تواند منفی باشد.");

        if (request.ExpectedDurationDays is < 1 or > 365)
            throw new InvalidOperationException("مدت معمول باید بین ۱ تا ۳۶۵ روز باشد.");

        if (request.FieldSchemaJson is not null && !StepFieldSchema.IsValid(request.FieldSchemaJson))
            throw new InvalidOperationException("اسکیمای فیلدهای مرحله معتبر نیست.");
    }

    /// <summary>
    /// کد یکتا از روی نام. نام فارسی است، پس اگر حرف لاتینی نماند از پیشوند ثابت
    /// به‌علاوهٔ شماره استفاده می‌شود — کد فقط شناسهٔ داخلی است و به کاربر نشان داده نمی‌شود.
    /// </summary>
    private async Task<string> GenerateCodeAsync(
        Guid tenantId, string name, IReadOnlyList<ProcessStepType> existing, CancellationToken ct)
    {
        var slug = new string(name.Where(char.IsAsciiLetterOrDigit).ToArray()).ToLowerInvariant();
        if (slug.Length == 0) slug = "step";
        if (slug.Length > 30) slug = slug[..30];

        var taken = existing.Select(s => s.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(slug)) return slug;

        for (var i = 2; i < 1000; i++)
        {
            var candidate = $"{slug}-{i}";
            if (!taken.Contains(candidate)) return candidate;
        }

        return $"{slug}-{Guid.NewGuid():N}"[..40];
    }

    /// <summary>اسکیمای خالی به null تبدیل می‌شود تا ستون jsonb بی‌دلیل «[]» نگیرد.</summary>
    private static string? NormalizeSchema(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() == 0
                ? null
                : json;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
