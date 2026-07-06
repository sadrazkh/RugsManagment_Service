using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.Mapping;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Services;

/// <summary>قرارداد عملیات CRUD و پیش بردن مرحله روی فرش‌ها</summary>
public interface IRugManagementService
{
    Task<IReadOnlyList<RugDto>> ListAsync(Guid tenantId, RugStatus? status, CancellationToken ct = default);
    Task<RugDto?> GetAsync(Guid tenantId, Guid rugId, CancellationToken ct = default);
    Task<RugDto> CreateAsync(Guid tenantId, CreateRugRequest request, CancellationToken ct = default);
    Task<RugDto> UpdateAsync(Guid tenantId, Guid rugId, UpdateRugRequest request, CancellationToken ct = default);
    Task<RugDto> AdvanceStepAsync(Guid tenantId, Guid rugId, Guid stepId, AdvanceRugStepRequest request, CancellationToken ct = default);
    Task<RugDto> UpdateStepPricingAsync(Guid tenantId, Guid rugId, Guid stepId, AdvanceRugStepRequest request, CancellationToken ct = default);
    Task<RugDto> SkipStepAsync(Guid tenantId, Guid rugId, Guid stepId, CancellationToken ct = default);
    Task<RugDto> GoBackStepAsync(Guid tenantId, Guid rugId, CancellationToken ct = default);
    Task<RugDto> ActivateStepAsync(Guid tenantId, Guid rugId, Guid stepId, CancellationToken ct = default);
    Task<RugDto> UpdateWorkflowAsync(Guid tenantId, Guid rugId, UpdateRugWorkflowRequest request, CancellationToken ct = default);
    Task<RugDto> ApplyTemplateAsync(Guid tenantId, Guid rugId, Guid templateId, IReadOnlyList<Guid>? skippedOptionalStepIds, CancellationToken ct = default);
    Task<BulkOperationResultDto> BulkAdvanceAsync(Guid tenantId, BulkAdvanceRequest request, CancellationToken ct = default);
    Task<BulkOperationResultDto> BulkGoBackAsync(Guid tenantId, BulkRugIdsRequest request, CancellationToken ct = default);
    Task<BulkOperationResultDto> BulkUpdateWorkflowAsync(Guid tenantId, BulkUpdateWorkflowRequest request, CancellationToken ct = default);
}

/// <summary>
/// هماهنگ‌کنندهٔ کار با فرش: از دیتابیس می‌خواند، موتور WorkflowEngine را صدا می‌زند، ذخیره می‌کند.
/// tenantId همیشه از JWT کاربر می‌آید تا فرش کارگاه دیگر دیده نشود.
/// </summary>
public sealed class RugManagementService(
    IRugRepository rugs,
    IWorkflowTemplateRepository templates,
    IWorkflowEngine workflowEngine,
    IRepository<RugWorkflowStep> rugSteps,
    IUnitOfWork unitOfWork) : IRugManagementService
{
    public async Task<IReadOnlyList<RugDto>> ListAsync(Guid tenantId, RugStatus? status, CancellationToken ct = default)
    {
        var list = await rugs.ListByTenantAsync(tenantId, status, ct);
        return list.Select(r => r.ToDto(workflowEngine)).ToList();
    }

    public async Task<RugDto?> GetAsync(Guid tenantId, Guid rugId, CancellationToken ct = default)
    {
        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct);
        return rug?.ToDto(workflowEngine);
    }

    public async Task<RugDto> CreateAsync(Guid tenantId, CreateRugRequest request, CancellationToken ct = default)
    {
        var sku = await rugs.GenerateNextSkuAsync(tenantId, ct);
        var rug = new Rug
        {
            TenantId = tenantId,
            Sku = sku,
            Title = request.Title,
            Origin = request.Origin,
            Pattern = request.Pattern,
            Material = request.Material,
            KnotDensity = request.KnotDensity,
            WidthMeters = request.WidthMeters,
            LengthMeters = request.LengthMeters,
            PurchaseCost = request.PurchaseCost,
            TargetSalePrice = request.TargetSalePrice,
            ImageUrl = request.ImageUrl,
            Notes = request.Notes,
            MetadataJson = request.MetadataJson,
            Status = RugStatus.Draft
        };

        // مسیر ۱: مراحل دستی از فرانت
        if (request.CustomSteps is { Count: > 0 })
        {
            var custom = request.CustomSteps
                .Select(s => new CustomWorkflowStepRequest(
                    s.ProcessStepTypeId,
                    s.IsOptional,
                    s.ServiceProviderId,
                    null))
                .ToList();
            await workflowEngine.BuildCustomWorkflowAsync(rug, custom, ct);
        }
        // مسیر ۲: کپی از قالب + حذف مراحل اختیاری انتخاب‌شده
        else if (request.WorkflowTemplateId.HasValue)
        {
            var template = await templates.GetWithStepsAsync(request.WorkflowTemplateId.Value, tenantId, ct)
                ?? throw new InvalidOperationException("قالب فرایند یافت نشد.");
            await workflowEngine.InitializeWorkflowFromTemplateAsync(rug, template, request.SkippedOptionalStepIds, ct);
        }

        await rugs.AddAsync(rug, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var created = await rugs.GetWithWorkflowAsync(rug.Id, tenantId, ct)
            ?? throw new InvalidOperationException("بارگذاری فرش بعد از ثبت ناموفق بود.");
        return created.ToDto(workflowEngine);
    }

    public async Task<RugDto> UpdateAsync(Guid tenantId, Guid rugId, UpdateRugRequest request, CancellationToken ct = default)
    {
        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش یافت نشد.");

        rug.Title = request.Title ?? rug.Title;
        rug.Origin = request.Origin ?? rug.Origin;
        rug.Pattern = request.Pattern ?? rug.Pattern;
        rug.Material = request.Material ?? rug.Material;
        rug.KnotDensity = request.KnotDensity ?? rug.KnotDensity;
        rug.WidthMeters = request.WidthMeters;
        rug.LengthMeters = request.LengthMeters;
        rug.PurchaseCost = request.PurchaseCost ?? rug.PurchaseCost;
        rug.TargetSalePrice = request.TargetSalePrice ?? rug.TargetSalePrice;
        if (request.Status.HasValue) rug.Status = request.Status.Value;
        rug.ImageUrl = request.ImageUrl ?? rug.ImageUrl;
        rug.Notes = request.Notes ?? rug.Notes;
        rug.MetadataJson = request.MetadataJson ?? rug.MetadataJson;
        rug.UpdatedAt = DateTimeOffset.UtcNow;

        rugs.Update(rug);
        await unitOfWork.SaveChangesAsync(ct);
        return rug.ToDto(workflowEngine);
    }

    public async Task<RugDto> AdvanceStepAsync(
        Guid tenantId, Guid rugId, Guid stepId, AdvanceRugStepRequest request, CancellationToken ct = default)
    {
        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش یافت نشد.");

        await workflowEngine.AdvanceStepAsync(rug, stepId, ToAdvanceRequest(request), ct);

        rug.UpdatedAt = DateTimeOffset.UtcNow;
        rugs.Update(rug);
        await unitOfWork.SaveChangesAsync(ct);
        return rug.ToDto(workflowEngine);
    }

    public async Task<RugDto> UpdateStepPricingAsync(
        Guid tenantId, Guid rugId, Guid stepId, AdvanceRugStepRequest request, CancellationToken ct = default)
    {
        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش یافت نشد.");

        await workflowEngine.UpdateStepPricingAsync(rug, stepId, ToAdvanceRequest(request with { MarkCompleted = false }), ct);

        rug.UpdatedAt = DateTimeOffset.UtcNow;
        rugs.Update(rug);
        await unitOfWork.SaveChangesAsync(ct);
        return rug.ToDto(workflowEngine);
    }

    public async Task<RugDto> SkipStepAsync(Guid tenantId, Guid rugId, Guid stepId, CancellationToken ct = default)
    {
        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش یافت نشد.");

        await workflowEngine.SkipStepAsync(rug, stepId, ct);
        rug.UpdatedAt = DateTimeOffset.UtcNow;
        rugs.Update(rug);
        await unitOfWork.SaveChangesAsync(ct);
        return rug.ToDto(workflowEngine);
    }

    public async Task<RugDto> GoBackStepAsync(Guid tenantId, Guid rugId, CancellationToken ct = default)
    {
        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش یافت نشد.");
        await workflowEngine.GoBackStepAsync(rug, ct);
        rug.UpdatedAt = DateTimeOffset.UtcNow;
        rugs.Update(rug);
        await unitOfWork.SaveChangesAsync(ct);
        return rug.ToDto(workflowEngine);
    }

    public async Task<RugDto> ActivateStepAsync(Guid tenantId, Guid rugId, Guid stepId, CancellationToken ct = default)
    {
        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش یافت نشد.");
        await workflowEngine.ActivateStepAsync(rug, stepId, ct);
        rug.UpdatedAt = DateTimeOffset.UtcNow;
        rugs.Update(rug);
        await unitOfWork.SaveChangesAsync(ct);
        return rug.ToDto(workflowEngine);
    }

    public async Task<RugDto> UpdateWorkflowAsync(
        Guid tenantId, Guid rugId, UpdateRugWorkflowRequest request, CancellationToken ct = default)
    {
        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش یافت نشد.");

        var custom = request.PendingSteps
            .Select(s => new CustomWorkflowStepRequest(s.ProcessStepTypeId, s.IsOptional, s.ServiceProviderId, null))
            .ToList();

        var existingIds = rug.WorkflowSteps.Select(s => s.Id).ToHashSet();
        await workflowEngine.UpdateWorkflowPathAsync(rug, custom, ct);
        // مراحل تازه‌ساختهٔ موتور را صریح Add می‌کنیم تا EF آن‌ها را (با Id از پیش‌تعیین) Added ببیند نه Modified.
        await AddNewStepsAsync(rug, existingIds, ct);

        rug.UpdatedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(ct);
        return rug.ToDto(workflowEngine);
    }

    /// <summary>
    /// اعمال یک قالب گردش کار روی فرشی که هنوز مسیر ندارد (یا فقط مراحل در صف دارد).
    /// اگر مرحلهٔ تکمیل‌شده/رد‌شده داشته باشد رد می‌شود تا تاریخچه از بین نرود (از «ویرایش مسیر» استفاده شود).
    /// </summary>
    public async Task<RugDto> ApplyTemplateAsync(
        Guid tenantId, Guid rugId, Guid templateId, IReadOnlyList<Guid>? skippedOptionalStepIds, CancellationToken ct = default)
    {
        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش یافت نشد.");

        if (rug.WorkflowSteps.Any(s => s.Status is WorkflowStepStatus.Completed or WorkflowStepStatus.Skipped))
            throw new InvalidOperationException("این فرش تاریخچهٔ مرحله دارد؛ برای تغییر مسیر از «ویرایش مسیر» استفاده کنید.");

        var template = await templates.GetWithStepsAsync(templateId, tenantId, ct)
            ?? throw new InvalidOperationException("قالب فرایند یافت نشد.");

        var existingIds = rug.WorkflowSteps.Select(s => s.Id).ToHashSet();
        await workflowEngine.InitializeWorkflowFromTemplateAsync(rug, template, skippedOptionalStepIds, ct);
        await AddNewStepsAsync(rug, existingIds, ct);
        rug.UpdatedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(ct);

        // بارگذاری دوباره برای داشتن نام مراحل (navigation ProcessStepType)
        var reloaded = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)!;
        return reloaded!.ToDto(workflowEngine);
    }

    public async Task<BulkOperationResultDto> BulkGoBackAsync(Guid tenantId, BulkRugIdsRequest request, CancellationToken ct = default)
    {
        var errors = new List<BulkItemErrorDto>();
        var ok = 0;
        foreach (var rugId in request.RugIds.Distinct())
        {
            try
            {
                var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
                    ?? throw new KeyNotFoundException("فرش یافت نشد.");
                await workflowEngine.GoBackStepAsync(rug, ct);
                rug.UpdatedAt = DateTimeOffset.UtcNow;
                ok++;
            }
            catch (Exception ex)
            {
                errors.Add(new BulkItemErrorDto(rugId, ex.Message));
            }
        }
        await unitOfWork.SaveChangesAsync(ct);
        return new BulkOperationResultDto(ok, errors.Count, errors);
    }

    /// <summary>مراحلی که موتور به فرشِ tracked اضافه کرده را با وضعیت Added ثبت می‌کند.</summary>
    private async Task AddNewStepsAsync(Rug rug, HashSet<Guid> existingIds, CancellationToken ct)
    {
        foreach (var step in rug.WorkflowSteps.Where(s => !existingIds.Contains(s.Id)).ToList())
            await rugSteps.AddAsync(step, ct);
    }

    public async Task<BulkOperationResultDto> BulkAdvanceAsync(
        Guid tenantId, BulkAdvanceRequest request, CancellationToken ct = default)
    {
        var errors = new List<BulkItemErrorDto>();
        var ok = 0;
        foreach (var rugId in request.RugIds.Distinct())
        {
            try
            {
                var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
                    ?? throw new KeyNotFoundException("فرش یافت نشد.");
                var step = rug.WorkflowSteps.FirstOrDefault(s => s.Status == WorkflowStepStatus.InProgress)
                    ?? throw new InvalidOperationException("مرحله جاری یافت نشد.");
                await workflowEngine.AdvanceStepAsync(rug, step.Id, ToAdvanceRequest(request.Step), ct);
                rug.UpdatedAt = DateTimeOffset.UtcNow;
                rugs.Update(rug);
                ok++;
            }
            catch (Exception ex)
            {
                errors.Add(new BulkItemErrorDto(rugId, ex.Message));
            }
        }
        await unitOfWork.SaveChangesAsync(ct);
        return new BulkOperationResultDto(ok, errors.Count, errors);
    }

    public async Task<BulkOperationResultDto> BulkUpdateWorkflowAsync(
        Guid tenantId, BulkUpdateWorkflowRequest request, CancellationToken ct = default)
    {
        var errors = new List<BulkItemErrorDto>();
        var ok = 0;
        var custom = request.PendingSteps
            .Select(s => new CustomWorkflowStepRequest(s.ProcessStepTypeId, s.IsOptional, s.ServiceProviderId, null))
            .ToList();

        foreach (var rugId in request.RugIds.Distinct())
        {
            try
            {
                var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
                    ?? throw new KeyNotFoundException("فرش یافت نشد.");
                var existingIds = rug.WorkflowSteps.Select(s => s.Id).ToHashSet();
                await workflowEngine.UpdateWorkflowPathAsync(rug, custom, ct);
                await AddNewStepsAsync(rug, existingIds, ct);
                rug.UpdatedAt = DateTimeOffset.UtcNow;
                ok++;
            }
            catch (Exception ex)
            {
                errors.Add(new BulkItemErrorDto(rugId, ex.Message));
            }
        }
        await unitOfWork.SaveChangesAsync(ct);
        return new BulkOperationResultDto(ok, errors.Count, errors);
    }

    private static AdvanceStepRequest ToAdvanceRequest(AdvanceRugStepRequest request) => new(
        request.ServiceProviderId,
        request.ManualCostOverride,
        request.PricingModel,
        request.UnitRate,
        request.PricingConfigJson,
        request.FieldValuesJson,
        request.Notes,
        request.MarkCompleted,
        request.Adjustment);
}
