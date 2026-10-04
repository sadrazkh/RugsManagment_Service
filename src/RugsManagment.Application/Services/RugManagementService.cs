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

    // ── سطل زباله ─────────────────────────────────────────────────

    /// <summary>انتقال فرش به سطل زباله (حذف نرم). فرشِ فروخته‌شده حذف نمی‌شود.</summary>
    Task SoftDeleteAsync(Guid tenantId, Guid rugId, CancellationToken ct = default);

    /// <summary>بازگرداندن فرش از سطل زباله با همان مراحل و هزینه‌ها.</summary>
    Task<RugDto> RestoreAsync(Guid tenantId, Guid rugId, CancellationToken ct = default);

    Task<IReadOnlyList<DeletedRugDto>> ListDeletedAsync(Guid tenantId, CancellationToken ct = default);
}

/// <summary>
/// هماهنگ‌کنندهٔ کار با فرش: از دیتابیس می‌خواند، موتور WorkflowEngine را صدا می‌زند، ذخیره می‌کند.
/// tenantId همیشه از کوکی ورود کاربر می‌آید تا فرش کارگاه دیگر دیده نشود.
///
/// پول (A.5): هر DTO که برمی‌گردد از <see cref="IMoneyVisibility"/> می‌گذرد (برای انباردار مبلغ‌ها null)، و
/// هر درخواستی که عضو پولیِ پرشده دارد از کاربر غیر Business با ۴۰۳ رد می‌شود. عضو پولیِ null رد نمی‌شود
/// و مقدار ذخیره‌شده را دست نمی‌زند (erratum E5)، چون فرم قدیمی فرش تا فاز بعد همه را دوباره می‌فرستد.
/// </summary>
public sealed class RugManagementService(
    IRugRepository rugs,
    IWorkflowTemplateRepository templates,
    IWorkflowEngine workflowEngine,
    IRepository<RugWorkflowStep> rugSteps,
    IServiceProviderService providerRates,
    IServiceProviderRepository providers,
    IRugBatchRepository batches,
    ICurrentUser currentUser,
    IAuditLog audit,
    IUnitOfWork unitOfWork,
    IMoneyVisibility money) : IRugManagementService
{
    public async Task<IReadOnlyList<RugDto>> ListAsync(Guid tenantId, RugStatus? status, CancellationToken ct = default)
    {
        var list = await rugs.ListByTenantAsync(tenantId, status, ct);
        return list.Select(r => r.ToDto(workflowEngine, money)).ToList();
    }

    /// <summary>
    /// فهرست صفحه‌بندی‌شده. برای غیر Business مرتب‌سازی «هزینهٔ کل» به «تاریخ ثبت» برمی‌گردد و ستون سرمایه
    /// null می‌شود، تا نه عدد و نه ترتیب ردیف‌ها هزینه را لو دهد.
    /// </summary>
    public async Task<PagedResult<RugListItemDto>> SearchAsync(Guid tenantId, RugQuery query, CancellationToken ct = default)
        => money.Redact(await rugs.SearchAsync(tenantId, money.GuardSort(query), ct));

    public async Task<RugDto?> GetAsync(Guid tenantId, Guid rugId, CancellationToken ct = default)
    {
        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct);
        return rug?.ToDto(workflowEngine, money);
    }

    /// <summary>
    /// ثبت فرش جدید. اگر دو کاربر هم‌زمان ثبت کنند ممکن است SKU یکسان تولید شود و
    /// ایندکس یکتای دیتابیس آن را رد کند؛ در این حالت چند بار دوباره تلاش می‌کنیم.
    /// </summary>
    public async Task<RugDto> CreateAsync(Guid tenantId, CreateRugRequest request, CancellationToken ct = default)
    {
        money.EnsureMayWriteMoney(request.PurchaseCost, request.TargetSalePrice);

        const int maxAttempts = 5;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await CreateOnceAsync(tenantId, request, ct);
            }
            catch (DuplicateKeyException) when (attempt < maxAttempts)
            {
                // برخورد SKU با ثبت هم‌زمان — با شمارهٔ تازه دوباره تلاش می‌کنیم.
                // Reset لازم است: فرشِ شکست‌خورده (با مراحل و ردیف تاریخچه‌اش) هنوز «Added» در DbContext
                // مانده؛ بدون پاک‌کردن، SaveChangesِ تلاش بعدی دوباره همان‌ها را با همان SKU درج می‌کرد
                // و هر پنج تلاش با همان خطا شکست می‌خورد.
                unitOfWork.Reset();
            }
        }
    }

    private async Task<RugDto> CreateOnceAsync(Guid tenantId, CreateRugRequest request, CancellationToken ct)
    {
        await EnsureProvidersOwnedAsync(tenantId, request.CustomSteps?.Select(s => s.ServiceProviderId), ct);

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
        return created.ToDto(workflowEngine, money);
    }

    public async Task<RugDto> UpdateAsync(Guid tenantId, Guid rugId, UpdateRugRequest request, CancellationToken ct = default)
    {
        // قیمت‌ها پایین‌تر با «?? مقدار فعلی» نوشته می‌شوند، پس null (که فرم انباردار می‌فرستد) مقدار
        // ذخیره‌شده را نگه می‌دارد؛ فقط مبلغِ پرشده از غیر Business رد می‌شود (E5).
        money.EnsureMayWriteMoney(request.PurchaseCost, request.TargetSalePrice);

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
        return rug.ToDto(workflowEngine, money);
    }

    // ── سطل زباله ─────────────────────────────────────────────────

    /// <summary>
    /// حذف نرم: رکورد می‌ماند و فقط از دید همهٔ کوئری‌ها خارج می‌شود (فیلتر سراسری EF).
    /// دلیل نرم‌بودن: هزینه‌های ثبت‌شدهٔ مراحل و بدهی استادکارها با یک کلیک اشتباه از بین نرود.
    /// </summary>
    public async Task SoftDeleteAsync(Guid tenantId, Guid rugId, CancellationToken ct = default)
    {
        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش یافت نشد.");

        // فروش، سند مالی است؛ حذف فرشِ فروخته‌شده گزارش درآمد را بی‌صدا خراب می‌کند
        if (rug.Sale is not null)
            throw new InvalidOperationException("این فرش فروخته شده است. ابتدا فروش را لغو کنید، سپس حذف کنید.");

        rug.DeletedAt = DateTimeOffset.UtcNow;
        rug.DeletedByUserId = currentUser.UserId;
        rug.UpdatedAt = DateTimeOffset.UtcNow;

        rugs.Update(rug);
        audit.Record(AuditAction.Deleted, nameof(Rug), rug.Id,
            $"فرش {rug.Sku} به سطل زباله منتقل شد.", rug.Sku);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<RugDto> RestoreAsync(Guid tenantId, Guid rugId, CancellationToken ct = default)
    {
        var rug = await rugs.GetDeletedAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش حذف‌شده یافت نشد.");

        rug.DeletedAt = null;
        rug.DeletedByUserId = null;
        rug.UpdatedAt = DateTimeOffset.UtcNow;

        rugs.Update(rug);
        audit.Record(AuditAction.Restored, nameof(Rug), rug.Id,
            $"فرش {rug.Sku} از سطل زباله بازگردانده شد.", rug.Sku);
        await unitOfWork.SaveChangesAsync(ct);

        // بعد از بازگردانی، فیلتر سراسری دیگر مانع نیست و می‌شود کامل خواندش
        var restored = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)!;
        return restored!.ToDto(workflowEngine, money);
    }

    public Task<IReadOnlyList<DeletedRugDto>> ListDeletedAsync(Guid tenantId, CancellationToken ct = default)
        => rugs.ListDeletedAsync(tenantId, ct);

    public async Task<RugDto> AdvanceStepAsync(
        Guid tenantId, Guid rugId, Guid stepId, AdvanceRugStepRequest request, CancellationToken ct = default)
    {
        // تکمیل بدون مبلغ برای انباردار مجاز است: نرخ توافقی یا پیش‌فرض پایین‌تر تزریق می‌شود
        money.EnsureMayWriteStepPricing(request);

        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش یافت نشد.");

        await EnsureProvidersOwnedAsync(tenantId, [request.ServiceProviderId], ct);

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
        return rug.ToDto(workflowEngine, money);
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
        money.EnsureMayWriteStepPricing(request);

        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش یافت نشد.");

        await EnsureProvidersOwnedAsync(tenantId, [request.ServiceProviderId], ct);

        request = await ApplyProviderRateAsync(tenantId, rug, stepId, request, ct);
        await workflowEngine.UpdateStepPricingAsync(rug, stepId, ToAdvanceRequest(request with { MarkCompleted = false }), ct);

        rug.UpdatedAt = DateTimeOffset.UtcNow;
        rugs.Update(rug);
        await unitOfWork.SaveChangesAsync(ct);
        return rug.ToDto(workflowEngine, money);
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
        return rug.ToDto(workflowEngine, money);
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
        return rug.ToDto(workflowEngine, money);
    }

    public async Task<RugDto> ActivateStepAsync(Guid tenantId, Guid rugId, Guid stepId, CancellationToken ct = default)
    {
        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش یافت نشد.");
        await workflowEngine.ActivateStepAsync(rug, stepId, ct);
        rug.UpdatedAt = DateTimeOffset.UtcNow;
        rugs.Update(rug);
        await unitOfWork.SaveChangesAsync(ct);
        return rug.ToDto(workflowEngine, money);
    }

    public async Task<RugDto> UpdateWorkflowAsync(
        Guid tenantId, Guid rugId, UpdateRugWorkflowRequest request, CancellationToken ct = default)
    {
        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش یافت نشد.");

        await EnsureProvidersOwnedAsync(tenantId, request.PendingSteps.Select(s => s.ServiceProviderId), ct);

        var custom = request.PendingSteps
            .Select(s => new CustomWorkflowStepRequest(s.ProcessStepTypeId, s.IsOptional, s.ServiceProviderId, null))
            .ToList();

        var existingIds = rug.WorkflowSteps.Select(s => s.Id).ToHashSet();
        await workflowEngine.UpdateWorkflowPathAsync(rug, custom, ct);
        // مراحل تازه‌ساختهٔ موتور را صریح Add می‌کنیم تا EF آن‌ها را (با Id از پیش‌تعیین) Added ببیند نه Modified.
        await AddNewStepsAsync(rug, existingIds, ct);

        rug.UpdatedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(ct);
        return rug.ToDto(workflowEngine, money);
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
        return reloaded!.ToDto(workflowEngine, money);
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

    /// <summary>
    /// تکمیل گروهی مرحلهٔ جاری چند فرش — با همان قواعد تکمیل تکی:
    /// نرخ توافقیِ طرف خدمات (یا نرخ پیش‌فرض مرحله) تزریق می‌شود، «چه کسی تکمیل کرد» ثبت می‌شود
    /// و برای هر فرش یک ردیف تاریخچه نوشته می‌شود. قبلاً هیچ‌کدام از این‌ها انجام نمی‌شد، پس
    /// همان مرحله از مسیر گروهی هزینهٔ متفاوتی با مسیر تکی می‌گرفت و ردی در تاریخچه نمی‌ماند.
    /// </summary>
    public async Task<BulkOperationResultDto> BulkAdvanceAsync(
        Guid tenantId, BulkAdvanceRequest request, CancellationToken ct = default)
    {
        // مبلغِ پرشده از غیر Business کل درخواست را ۴۰۳ می‌کند، پیش از دست زدن به هر فرش
        money.EnsureMayWriteStepPricing(request.Step);

        // طرف خدمات برای همهٔ فرش‌ها یکی است؛ اگر مال کارگاه دیگر باشد کل درخواست ۴۰۴ است
        await EnsureProvidersOwnedAsync(tenantId, [request.Step.ServiceProviderId], ct);

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
                var stepName = step.ProcessStepType?.NameFa ?? "مرحله";

                var itemRequest = request.Step;

                // اگر مقادیر فرم فرستاده شده، مثل تکمیل تکی با اسکیمای همان نوع مرحله سنجیده و پاک می‌شوند.
                // (بدون مقدار، رفتار تکمیل سریع گروهی عوض نمی‌شود.)
                if (itemRequest.FieldValuesJson is not null)
                {
                    itemRequest = itemRequest with
                    {
                        FieldValuesJson = StepFieldSchema.ValidateValues(
                            step.ProcessStepType?.FieldSchemaJson, itemRequest.FieldValuesJson)
                    };
                }

                // هر فرش نرخ توافقیِ طرفِ مرحلهٔ خودش را می‌گیرد، نه یک نرخ مشترک
                itemRequest = await ApplyProviderRateAsync(tenantId, rug, step.Id, itemRequest, ct);
                await workflowEngine.AdvanceStepAsync(rug, step.Id, ToAdvanceRequest(itemRequest), ct);

                StampCompletedBy(rug, step.Id);
                rug.UpdatedAt = DateTimeOffset.UtcNow;
                rugs.Update(rug);

                audit.Record(AuditAction.StepAdvanced, nameof(Rug), rug.Id,
                    $"مرحلهٔ «{stepName}» تکمیل شد (گروهی).", rug.Sku);
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
        money.EnsureMayWriteMoney(request.TargetSalePrice);

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

        // گروه مقصد باید مال همین کارگاه باشد. بدون این بررسی، شناسهٔ ناموجود به نقض کلید خارجی
        // (خطای ۵۰۰ و برگشت کل عملیات) می‌رسید و شناسهٔ گروه کارگاه دیگر بی‌صدا ذخیره می‌شد.
        if (request.BatchId is Guid targetBatchId && targetBatchId != Guid.Empty)
        {
            var owned = await batches.ListAsync(b => b.Id == targetBatchId && b.TenantId == tenantId, ct);
            if (owned.Count == 0)
                throw new InvalidOperationException("گروه انتخاب‌شده یافت نشد.");
        }

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
        await EnsureProvidersOwnedAsync(tenantId, request.PendingSteps.Select(s => s.ServiceProviderId), ct);

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
    ///   مبلغ دستی ← نرخ صریحِ همین ثبت ← قیمتِ صریحِ ذخیره‌شده روی مرحله (با همان طرف)
    ///   ← نرخ توافقی طرف ← نرخ پیش‌فرض نوع مرحله
    /// </summary>
    private async Task<AdvanceRugStepRequest> ApplyProviderRateAsync(
        Guid tenantId, Rug rug, Guid stepId, AdvanceRugStepRequest request, CancellationToken ct)
    {
        // اپراتور صریحاً تصمیم گرفته — دست نمی‌زنیم
        if (request.ManualCostOverride.HasValue || request.UnitRate.HasValue || request.PricingModel.HasValue)
            return request;

        var step = rug.WorkflowSteps.FirstOrDefault(s => s.Id == stepId);
        if (step is null) return request;

        // انجام‌دهنده برداشته می‌شود («—»): مرحله دیگر طرفی ندارد که نرخ توافقی‌اش تزریق شود
        if (request.ClearServiceProvider == true && request.ServiceProviderId is null)
            return request;

        // قیمتی که قبلاً صریح روی مرحله ذخیره شده («فقط ذخیره» با نرخ ویژه، یا پیکربندی ترکیبی) با همان
        // انجام‌دهنده دست نمی‌خورد. ویرایشگرِ هزینه همین قیمت را پیش‌پر و پیش‌نمایش می‌کند و اگر کاربر چیزی را
        // عوض نکند null می‌فرستد؛ بدون این بررسی، «تکمیل» نرخ توافقیِ طرف را جای قیمتی می‌نشاند که کاربر روی
        // صفحه دیده بود. مرحله‌ای که هنوز قیمتی ذخیره ندارد، یا انجام‌دهنده‌اش همین حالا عوض می‌شود، همچنان
        // نرخ توافقی (طرف جدید) را می‌گیرد.
        var providerUnchanged = request.ServiceProviderId is null || request.ServiceProviderId == step.ServiceProviderId;
        var hasExplicitPricing = step.AppliedPricingModel.HasValue
                                 || step.AppliedUnitRate.HasValue
                                 || !string.IsNullOrWhiteSpace(step.PricingConfigJson);
        if (providerUnchanged && hasExplicitPricing)
            return request;

        // طرفِ همین ثبت، وگرنه طرفی که قبلاً روی مرحله نشسته
        var providerId = request.ServiceProviderId ?? step.ServiceProviderId;
        if (providerId is null) return request;

        var rate = await providerRates.FindRateAsync(tenantId, providerId.Value, step.ProcessStepTypeId, ct);
        if (rate is null) return request;

        return request with { PricingModel = rate.PricingModel, UnitRate = rate.UnitRate };
    }

    /// <summary>
    /// هر طرف خدماتی که به مرحله نسبت داده می‌شود باید مال همین کارگاه باشد.
    /// بدون این بررسی، شناسهٔ طرفِ کارگاه دیگر روی مرحله می‌نشست و کار این کارگاه به حساب
    /// (و ماندهٔ بدهیِ) طرفِ کارگاه دیگر اضافه می‌شد. طرفِ کارگاه دیگر = «یافت نشد» (۴۰۴).
    /// </summary>
    private async Task EnsureProvidersOwnedAsync(Guid tenantId, IEnumerable<Guid?>? providerIds, CancellationToken ct)
    {
        var ids = (providerIds ?? [])
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return;

        var owned = await providers.ListAsync(p => ids.Contains(p.Id) && p.TenantId == tenantId, ct);
        if (owned.Count != ids.Count)
            throw new KeyNotFoundException("طرف خدمات یافت نشد.");
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
        request.Adjustment,
        request.ClearManualOverride,
        request.ClearServiceProvider);
}
