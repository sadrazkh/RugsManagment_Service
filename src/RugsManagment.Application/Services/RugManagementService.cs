using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Common;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.Mapping;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Services;

/// <summary>قرارداد عملیات CRUD و پیش بردن مرحله روی فرش‌ها</summary>
public interface IRugManagementService
{
    Task<IReadOnlyList<RugDto>> ListAsync(Guid tenantId, RugStatus? status, CancellationToken ct = default);

    /// <summary>فهرست صفحه‌بندی‌شده با جستجو، فیلتر و مرتب‌سازی — مسیر اصلی صفحهٔ فرش‌ها.</summary>
    Task<PagedResult<RugListItemDto>> SearchAsync(Guid tenantId, RugQuery query, CancellationToken ct = default);
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

    /// <summary>ویرایش گروهی مشخصات — فقط فیلدهای مقداردار اعمال می‌شوند.</summary>
    Task<BulkOperationResultDto> BulkUpdateFieldsAsync(Guid tenantId, BulkUpdateFieldsRequest request, CancellationToken ct = default);
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
    IServiceProviderService providerRates,
    ICurrentUser currentUser,
    IAuditLog audit,
    IUnitOfWork unitOfWork) : IRugManagementService
{
    public async Task<IReadOnlyList<RugDto>> ListAsync(Guid tenantId, RugStatus? status, CancellationToken ct = default)
    {
        var list = await rugs.ListByTenantAsync(tenantId, status, ct);
        return list.Select(r => r.ToDto(workflowEngine)).ToList();
    }

    public Task<PagedResult<RugListItemDto>> SearchAsync(Guid tenantId, RugQuery query, CancellationToken ct = default)
        => rugs.SearchAsync(tenantId, query, ct);

    public async Task<RugDto?> GetAsync(Guid tenantId, Guid rugId, CancellationToken ct = default)
    {
        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct);
        return rug?.ToDto(workflowEngine);
    }

    /// <summary>
    /// ثبت فرش جدید. اگر دو کاربر هم‌زمان ثبت کنند ممکن است SKU یکسان تولید شود و
    /// ایندکس یکتای دیتابیس آن را رد کند؛ در این حالت چند بار دوباره تلاش می‌کنیم.
    /// </summary>
    public async Task<RugDto> CreateAsync(Guid tenantId, CreateRugRequest request, CancellationToken ct = default)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await CreateOnceAsync(tenantId, request, ct);
            }
            catch (DuplicateKeyException) when (attempt < maxAttempts)
            {
                // برخورد SKU با ثبت هم‌زمان — با شمارهٔ تازه دوباره تلاش می‌کنیم
            }
        }
    }

    private async Task<RugDto> CreateOnceAsync(Guid tenantId, CreateRugRequest request, CancellationToken ct)
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

        audit.Record(AuditAction.Created, nameof(Rug), rug.Id,
            $"فرش «{rug.Title ?? rug.Sku}» ثبت شد.", rug.Sku);

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

        var target = rug.WorkflowSteps.FirstOrDefault(s => s.Id == stepId);
        var stepName = target?.ProcessStepType?.NameFa ?? "مرحله";

        // مقادیر فرم داینامیک در برابر اسکیمای همان نوع مرحله سنجیده و پاک می‌شوند
        request = request with
        {
            FieldValuesJson = StepFieldSchema.ValidateValues(
                target?.ProcessStepType?.FieldSchemaJson, request.FieldValuesJson)
        };

        request = await ApplyProviderRateAsync(tenantId, rug, stepId, request, ct);
        await workflowEngine.AdvanceStepAsync(rug, stepId, ToAdvanceRequest(request), ct);

        StampCompletedBy(rug, stepId);

        rug.UpdatedAt = DateTimeOffset.UtcNow;
        rugs.Update(rug);

        audit.Record(AuditAction.StepAdvanced, nameof(Rug), rug.Id,
            $"مرحلهٔ «{stepName}» تکمیل شد.", rug.Sku);

        await unitOfWork.SaveChangesAsync(ct);
        return rug.ToDto(workflowEngine);
    }

    /// <summary>
    /// ثبت «چه کسی این مرحله را تمام کرد». نام کاربر کپی می‌شود تا با حذف حساب،
    /// تاریخچهٔ فرش بی‌نام نماند.
    /// </summary>
    private void StampCompletedBy(Rug rug, Guid stepId)
    {
        var step = rug.WorkflowSteps.FirstOrDefault(s => s.Id == stepId);
        if (step is null || step.Status != WorkflowStepStatus.Completed) return;

        step.CompletedByUserId = currentUser.UserId;
        step.CompletedByName = currentUser.DisplayName;
    }

    public async Task<RugDto> UpdateStepPricingAsync(
        Guid tenantId, Guid rugId, Guid stepId, AdvanceRugStepRequest request, CancellationToken ct = default)
    {
        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش یافت نشد.");

        request = await ApplyProviderRateAsync(tenantId, rug, stepId, request, ct);
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

        var stepName = rug.WorkflowSteps.FirstOrDefault(s => s.Id == stepId)?.ProcessStepType?.NameFa ?? "مرحله";

        await workflowEngine.SkipStepAsync(rug, stepId, ct);
        rug.UpdatedAt = DateTimeOffset.UtcNow;
        rugs.Update(rug);

        audit.Record(AuditAction.StepSkipped, nameof(Rug), rug.Id,
            $"مرحلهٔ «{stepName}» رد شد.", rug.Sku);

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

        var current = rug.WorkflowSteps.FirstOrDefault(s => s.Status == WorkflowStepStatus.InProgress);
        audit.Record(AuditAction.StepReverted, nameof(Rug), rug.Id,
            $"به مرحلهٔ «{current?.ProcessStepType?.NameFa ?? "قبلی"}» بازگشت.", rug.Sku);

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

    public async Task<BulkOperationResultDto> BulkUpdateFieldsAsync(
        Guid tenantId, BulkUpdateFieldsRequest request, CancellationToken ct = default)
    {
        var errors = new List<BulkItemErrorDto>();
        var ok = 0;

        // فهرست تغییرات برای ثبت در تاریخچه — تا بعداً معلوم باشد چه چیزی گروهی عوض شد
        var changes = new List<string>();
        if (request.Origin is not null) changes.Add("اصالت");
        if (request.Pattern is not null) changes.Add("طرح");
        if (request.Material is not null) changes.Add("جنس");
        if (request.KnotDensity is not null) changes.Add("رجشمار");
        if (request.TargetSalePrice is not null) changes.Add("قیمت هدف");
        if (request.Status is not null) changes.Add("وضعیت");
        if (request.BatchId is not null) changes.Add("گروه");

        if (changes.Count == 0)
            throw new InvalidOperationException("هیچ فیلدی برای تغییر انتخاب نشده است.");

        foreach (var rugId in request.RugIds.Distinct())
        {
            try
            {
                var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
                    ?? throw new KeyNotFoundException("فرش یافت نشد.");

                if (request.Origin is not null) rug.Origin = Blank(request.Origin);
                if (request.Pattern is not null) rug.Pattern = Blank(request.Pattern);
                if (request.Material is not null) rug.Material = Blank(request.Material);
                if (request.KnotDensity is not null) rug.KnotDensity = request.KnotDensity;
                if (request.TargetSalePrice is not null) rug.TargetSalePrice = request.TargetSalePrice;
                if (request.Status is not null) rug.Status = request.Status.Value;

                // Guid.Empty قرارداد «خارج کردن از گروه» است
                if (request.BatchId is Guid batchId)
                    rug.BatchId = batchId == Guid.Empty ? null : batchId;

                rug.UpdatedAt = DateTimeOffset.UtcNow;
                rugs.Update(rug);
                ok++;
            }
            catch (Exception ex)
            {
                errors.Add(new BulkItemErrorDto(rugId, ex.Message));
            }
        }

        if (ok > 0)
        {
            audit.Record(AuditAction.Updated, nameof(Rug), null,
                $"ویرایش گروهی {PersianText.ToPersianDigits(ok.ToString())} فرش ({string.Join("، ", changes)}).");
        }

        await unitOfWork.SaveChangesAsync(ct);
        return new BulkOperationResultDto(ok, errors.Count, errors);
    }

    /// <summary>رشتهٔ خالی یعنی «پاک کن»؛ متن واقعی نرمال‌سازی می‌شود.</summary>
    private static string? Blank(string value)
        => string.IsNullOrWhiteSpace(value) ? null : PersianText.Normalize(value.Trim());

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

    /// <summary>
    /// اگر مرحله به یک طرف خدمات نسبت داده شده و اپراتور نرخ یا مبلغ دستی نداده،
    /// نرخ توافقیِ همان طرف برای همان نوع مرحله اعمال می‌شود.
    ///
    /// ترتیب اولویت (از قوی به ضعیف):
    ///   مبلغ دستی ← نرخ صریحِ همین ثبت ← نرخ توافقی طرف ← نرخ پیش‌فرض نوع مرحله
    /// </summary>
    private async Task<AdvanceRugStepRequest> ApplyProviderRateAsync(
        Guid tenantId, Rug rug, Guid stepId, AdvanceRugStepRequest request, CancellationToken ct)
    {
        // اپراتور صریحاً تصمیم گرفته — دست نمی‌زنیم
        if (request.ManualCostOverride.HasValue || request.UnitRate.HasValue || request.PricingModel.HasValue)
            return request;

        var step = rug.WorkflowSteps.FirstOrDefault(s => s.Id == stepId);
        if (step is null) return request;

        // طرفِ همین ثبت، وگرنه طرفی که قبلاً روی مرحله نشسته
        var providerId = request.ServiceProviderId ?? step.ServiceProviderId;
        if (providerId is null) return request;

        var rate = await providerRates.FindRateAsync(tenantId, providerId.Value, step.ProcessStepTypeId, ct);
        if (rate is null) return request;

        return request with { PricingModel = rate.PricingModel, UnitRate = rate.UnitRate };
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
