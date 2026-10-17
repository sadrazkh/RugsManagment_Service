using System.Globalization;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Common;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.Features.Rugs;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Common;
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

    // ── فاز ۲: اندازهٔ نهایی، برش، شماره و پیشنهاد مقدار ──────────────

    /// <summary>
    /// اندازهٔ نهایی (C.4): طول و عرض، «همان اندازه»، یا پاک کردن؛ با نسخهٔ فرش (کهنه → ۴۰۹). هرگز سطر خرید «هر متر» را
    /// عوض نمی‌کند (آن روی اندازهٔ اول است).
    /// </summary>
    Task<RugDto> SetFinalSizeAsync(Guid tenantId, Guid rugId, SetFinalSizeRequest request, CancellationToken ct = default);

    /// <summary>برش دسته (D.9) با نسخهٔ فرش؛ هر دو فرش بعد از همان پاس.</summary>
    Task<SplitRugResultDto> SplitAsync(Guid tenantId, Guid rugId, SplitRugRequest request, CancellationToken ct = default);

    /// <summary>شمارهٔ پیشنهادی بعدی (G.4)؛ در حالت «دستی» null.</summary>
    Task<NextSerialDto> NextSerialAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>یافتن فرش فعال با شماره، دقیق یا «داخل دسته»؛ پیدا نشد → null.</summary>
    Task<RugBySerialDto?> FindBySerialAsync(Guid tenantId, string serial, CancellationToken ct = default);

    /// <summary>پیشنهاد مقدارهای قبلیِ یک خانهٔ combo (<c>/api/rugs/values</c>).</summary>
    Task<IReadOnlyList<string>> ListValuesAsync(Guid tenantId, RugValuesQuery query, CancellationToken ct = default);
}

/// <summary>
/// هماهنگ‌کنندهٔ کار با فرش: از دیتابیس می‌خواند، موتور WorkflowEngine را صدا می‌زند، ذخیره می‌کند.
/// tenantId همیشه از کوکی ورود کاربر می‌آید تا فرش کارگاه دیگر دیده نشود.
///
/// <para><b>نوشتن فرش از فاز ۲</b> فقط از راه <see cref="IRugFieldWriter"/> است (کلیدهای FieldCatalog در <c>Values</c>)،
/// و هر ذخیره از پاس سازگاری می‌گذرد (D.4)؛ پس DTOی پاسخ همان عددهای تازه (مبلغ سطرهای خرید، بهای تمام‌شده) را دارد و
/// نسخهٔ فرش (xmin) با <see cref="IConcurrencyGuard"/> سنجیده می‌شود: نسخهٔ کهنه → ۴۰۹. درخواستِ شکل قدیمی (بی
/// <c>Values</c>) قاعده‌های I.3 را دارد تا رفت‌وبرگشت فرم قدیمی نه سطرهای خرید چندگانه را خراب کند و نه اندازهٔ اول را با
/// اندازهٔ نهایی رونویسی کند.</para>
///
/// <para><b>پول</b> (A.5): هر DTO که برمی‌گردد از <see cref="IMoneyVisibility"/> می‌گذرد (برای انباردار مبلغ‌ها null)، و
/// هر درخواستی که عضو یا کلید پولیِ پرشده دارد از کاربر غیر Business با ۴۰۳ رد می‌شود. عضو پولیِ null در درخواست
/// قدیمی رد نمی‌شود و مقدار ذخیره‌شده را دست نمی‌زند (erratum E5).</para>
///
/// <para><b>کد انبار</b> از شمارندهٔ اتمیک کارگاه است (<see cref="IRugSkuGenerator"/>)؛ حلقهٔ تلاش دوبارهٔ قدیمی برداشته
/// شد. <b>شمارهٔ خودکار</b> (حالت Auto، E2) زیر قفل حساب‌های کارگاه و پیش از flush روی همان فرش ردیابی‌شده نوشته
/// می‌شود؛ <c>SerialOverlapStage</c> فقط می‌سنجد.</para>
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
    IMoneyVisibility money,
    IRugFieldWriter writer,
    IRugDtoBuilder dtos,
    ISerialSuggester serials,
    IRugSkuGenerator skus,
    IRugSplitService splitter,
    IConcurrencyGuard concurrency,
    ILedgerLock ledgerLock,
    ITenantPreferencesRepository preferences) : IRugManagementService
{
    public async Task<IReadOnlyList<RugDto>> ListAsync(Guid tenantId, RugStatus? status, CancellationToken ct = default)
    {
        var list = await rugs.ListByTenantAsync(tenantId, status, ct);
        return await dtos.BuildManyAsync(list, ct);
    }

    /// <summary>
    /// فهرست صفحه‌بندی‌شده. برای غیر Business مرتب‌سازی «هزینهٔ کل» به «تاریخ ثبت» برمی‌گردد و ستون‌های پولی null
    /// می‌شوند، تا نه عدد و نه ترتیب ردیف‌ها هزینه را لو دهد.
    /// </summary>
    public async Task<PagedResult<RugListItemDto>> SearchAsync(Guid tenantId, RugQuery query, CancellationToken ct = default)
        => money.Redact(await rugs.SearchAsync(tenantId, money.GuardSort(query), ct));

    public async Task<RugDto?> GetAsync(Guid tenantId, Guid rugId, CancellationToken ct = default)
    {
        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct);
        return rug is null ? null : await dtos.BuildAsync(rug, ct);
    }

    // ═════════════════════════════════════════════════════════════════════
    // ثبت
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ثبت فرش تازه. درخواست فاز ۲ (<c>Values</c> پر) همهٔ فیلدها را از <see cref="IRugFieldWriter"/> می‌نویسد؛ درخواست
    /// قدیمی همان رفتار قبل را دارد (اندازه‌ها اندازهٔ اول می‌شوند و قیمت خرید یک سطر «کل»). کد انبار از شمارنده، بی حلقهٔ
    /// تلاش دوباره.
    /// </summary>
    public async Task<RugDto> CreateAsync(Guid tenantId, CreateRugRequest request, CancellationToken ct = default)
    {
        var v2 = request.Values is not null;
        // عضوهای پولی قدیمی در هر دو شکل درخواست سنجیده می‌شوند (A.5، E5): در شکل فاز ۲ نادیده گرفته می‌شوند، ولی مبلغ
        // پرشده از غیر Business باز هم ۴۰۳ است، نه پذیرشِ بی‌صدا
        money.EnsureMayWriteMoney(request.PurchaseCost, request.TargetSalePrice);
        if (v2) writer.EnsureMayWrite(request.Values);

        await EnsureProvidersOwnedAsync(tenantId, request.CustomSteps?.Select(s => s.ServiceProviderId), ct);
        var template = request.CustomSteps is not { Count: > 0 } && request.WorkflowTemplateId is { } templateId
            ? await LoadTemplateAsync(tenantId, templateId, ct)
            : null;

        var context = await WriteContextAsync(tenantId, isNew: true, ct);
        var rug = new Rug { TenantId = tenantId, Status = RugStatus.Draft };

        RugFieldWriteResult? written = null;
        if (v2)
        {
            written = await writer.ApplyAsync(rug, request.Values!, context, ct);
        }
        else
        {
            rug.Title = Blank(request.Title);
            rug.Origin = Blank(request.Origin);
            rug.Pattern = Blank(request.Pattern);
            rug.Material = Blank(request.Material);
            rug.KnotDensity = request.KnotDensity;
            rug.TargetSalePrice = request.TargetSalePrice;
            rug.ImageUrl = request.ImageUrl;
            rug.Notes = request.Notes;
            rug.MetadataJson = request.MetadataJson;
            SetLegacyIntakeSize(rug, request.LengthMeters, request.WidthMeters);

            if (request.PurchaseCost is { } purchaseCost)
                await writer.ApplyLegacyPurchaseCostAsync(rug, purchaseCost, context, ct);
        }

        // مسیر ۱: مراحل دستی از فرانت
        if (request.CustomSteps is { Count: > 0 })
        {
            var custom = request.CustomSteps
                .Select(s => new CustomWorkflowStepRequest(s.ProcessStepTypeId, s.IsOptional, s.ServiceProviderId, null))
                .ToList();
            await workflowEngine.BuildCustomWorkflowAsync(rug, custom, ct);
        }
        // مسیر ۲: کپی از قالب + حذف مراحل اختیاری انتخاب‌شده
        else if (template is not null)
        {
            await workflowEngine.InitializeWorkflowFromTemplateAsync(rug, template, request.SkippedOptionalStepIds, ct);
            await DropForeignProvidersAsync(tenantId, rug, ct);
        }

        // کد انبار بعد از همهٔ سنجش‌ها، تا درخواستِ ردشده شماره‌ای از شمارنده نسوزاند
        rug.Sku = (await skus.NextAsync(tenantId, 1, ct))[0];
        await rugs.AddAsync(rug, ct);

        audit.Record(AuditAction.Created, nameof(Rug), rug.Id,
            $"فرش «{rug.Title ?? rug.SerialNo ?? rug.Sku}» ثبت شد.", rug.Sku);

        // حالت «خودکار» (G.4) برای هر فرش تازه، از هر دو شکل درخواست: درخواست قدیمی اصلاً شماره ندارد
        if (written?.NeedsAutoSerial ?? context.SerialMode == SerialMode.Auto)
            await SaveWithAutoSerialAsync(tenantId, rug, ct);
        else
            await unitOfWork.SaveChangesAsync(ct);

        var created = await rugs.GetWithWorkflowAsync(rug.Id, tenantId, ct)
            ?? throw new InvalidOperationException("بارگذاری فرش بعد از ثبت ناموفق بود.");
        return await dtos.BuildAsync(created, ct);
    }

    /// <summary>
    /// شمارهٔ خودکار (E2): در یک تراکنش، اول قفل حساب‌های کارگاه، بعد بزرگ‌ترین شمارهٔ در حال استفاده (حالا هیچ ثبت
    /// هم‌زمانی نمی‌تواند همان را بگیرد)، بعد شماره روی همین فرش ردیابی‌شده و ذخیره. پاسِ همان ذخیره قفل را دوباره نمی‌گیرد
    /// و فقط همپوشانی را می‌سنجد؛ پس DTO همان شماره و نسخهٔ تازه را دارد.
    /// </summary>
    private Task SaveWithAutoSerialAsync(Guid tenantId, Rug rug, CancellationToken ct)
        => unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await ledgerLock.AcquireAsync(tenantId, token);
            var next = await serials.NextFreeAsync(tenantId, token);
            rug.SetSerialAndQuantity(SerialSuggester.ForNewRow(next, rug.Quantity), rug.Quantity);
            await unitOfWork.SaveChangesAsync(token);
        }, ct);

    // ═════════════════════════════════════════════════════════════════════
    // ویرایش
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ویرایش فرش. شکل فاز ۲ (<c>Values</c> + <c>Version</c>) فقط از نویسندهٔ فیلدها؛ نسخه لازم است. شکل قدیمی قاعده‌های
    /// I.3 را دارد و نسخه را اگر آمده باشد می‌سنجد.
    /// </summary>
    public async Task<RugDto> UpdateAsync(Guid tenantId, Guid rugId, UpdateRugRequest request, CancellationToken ct = default)
    {
        var v2 = request.Values is not null;

        // قیمت‌ها در شکل قدیمی با «?? مقدار فعلی» نوشته می‌شوند، پس null (که فرم انباردار می‌فرستد) مقدار ذخیره‌شده را
        // نگه می‌دارد؛ فقط مبلغِ پرشده از غیر Business رد می‌شود (E5) — در شکل فاز ۲ هم، که این عضوها نادیده‌اند (A.5).
        money.EnsureMayWriteMoney(request.PurchaseCost, request.TargetSalePrice);
        if (v2)
        {
            writer.EnsureMayWrite(request.Values);
            if (request.Version is null)
                throw new AppValidationException(Messages.RugVersionRequired.Text);
        }

        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش یافت نشد.");
        if (request.Version is { } version)
            concurrency.Expect(rug, version);

        var context = await WriteContextAsync(tenantId, isNew: false, ct);
        if (v2)
        {
            await writer.ApplyAsync(rug, request.Values!, context, ct);
        }
        else
        {
            rug.Title = request.Title ?? rug.Title;
            rug.Origin = request.Origin ?? rug.Origin;
            rug.Pattern = request.Pattern ?? rug.Pattern;
            rug.Material = request.Material ?? rug.Material;
            rug.KnotDensity = request.KnotDensity ?? rug.KnotDensity;
            ApplyLegacyEffectiveSize(rug, request.LengthMeters, request.WidthMeters);
            if (request.PurchaseCost is { } purchaseCost)
                await writer.ApplyLegacyPurchaseCostAsync(rug, purchaseCost, context, ct);
            rug.TargetSalePrice = request.TargetSalePrice ?? rug.TargetSalePrice;
            if (request.Status.HasValue) rug.Status = request.Status.Value;
            rug.ImageUrl = request.ImageUrl ?? rug.ImageUrl;
            rug.Notes = request.Notes ?? rug.Notes;
            rug.MetadataJson = request.MetadataJson ?? rug.MetadataJson;
        }

        // ستون خود فرش همیشه نوشته می‌شود تا بررسی نسخه (UPDATE … WHERE xmin) انجام شود و نسخه جلو برود
        rug.UpdatedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(ct);
        return await dtos.BuildAsync(rug, ct);
    }

    /// <summary>
    /// قاعدهٔ I.3 برای طول و عرضِ فرم قدیمی (جفت مؤثر): تا اندازهٔ نهایی نیست، اندازهٔ اول را می‌نویسند؛ با اندازهٔ نهایی،
    /// همان جفت مؤثر بی‌اثر است و مقدار دیگر ۴۰۹ (از «اندازهٔ نهایی» عوض شود). هرگز اندازهٔ اول را با اندازهٔ نهایی رونویسی
    /// نمی‌کنند.
    /// </summary>
    private static void ApplyLegacyEffectiveSize(Rug rug, decimal lengthMeters, decimal widthMeters)
    {
        var length = Num.Round(lengthMeters, Dimension.Decimals);
        var width = Num.Round(widthMeters, Dimension.Decimals);
        if (length == rug.LengthMeters && width == rug.WidthMeters)
            return;

        if (rug.HasFinalSize)
            throw new DomainConflictException(Messages.RugHasFinalSize.Text,
                length != rug.LengthMeters ? "lengthMeters" : "widthMeters");

        SetLegacyIntakeSize(rug, length, width);
    }

    /// <summary>اندازهٔ اول از عضوهای قدیمی، با همان مرزهای اندازه (خطا روی نام عضو قدیمی تا فرم قدیمی نشانش دهد).</summary>
    private static void SetLegacyIntakeSize(Rug rug, decimal lengthMeters, decimal widthMeters)
    {
        var length = Num.Round(lengthMeters, Dimension.Decimals);
        var width = Num.Round(widthMeters, Dimension.Decimals);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (!Dimension.IsValid(length)) errors["lengthMeters"] = [Messages.Dimension.Text];
        if (!Dimension.IsValid(width)) errors["widthMeters"] = [Messages.Dimension.Text];
        if (errors.Count > 0)
            throw new AppValidationException(Messages.Dimension.Text, errors);

        rug.SetIntakeSize(length, width);
    }

    // ═════════════════════════════════════════════════════════════════════
    // اندازهٔ نهایی و برش
    // ═════════════════════════════════════════════════════════════════════

    public async Task<RugDto> SetFinalSizeAsync(
        Guid tenantId, Guid rugId, SetFinalSizeRequest request, CancellationToken ct = default)
    {
        var sameAsIntake = request.SameAsIntake == true;
        var clear = request.Clear == true;
        var hasPair = request.Length is not null && request.Width is not null;
        var shapes = (sameAsIntake ? 1 : 0) + (clear ? 1 : 0) + (hasPair ? 1 : 0);
        var partial = (request.Length is null) != (request.Width is null);
        if (shapes != 1 || partial)
            throw new AppValidationException(Messages.FinalSizeRequest.Text);

        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش یافت نشد.");
        concurrency.Expect(rug, request.Version);

        var now = DateTimeOffset.UtcNow;
        string summary;
        if (clear)
        {
            rug.ClearFinalSize();
            summary = $"اندازهٔ نهایی فرش {Label(rug)} پاک شد.";
        }
        else
        {
            if (sameAsIntake) rug.SetFinalSizeSameAsIntake(now, currentUser.UserId);
            else rug.SetFinalSize(request.Length!.Value, request.Width!.Value, now, currentUser.UserId);
            summary = $"اندازهٔ نهایی فرش {Label(rug)}: {Size(rug.FinalLength!.Value)} × {Size(rug.FinalWidth!.Value)}";
        }

        rug.UpdatedAt = now;
        audit.Record(AuditAction.FinalSizeRecorded, nameof(Rug), rug.Id, summary, rug.Sku);
        await unitOfWork.SaveChangesAsync(ct);
        return await dtos.BuildAsync(rug, ct);
    }

    public async Task<SplitRugResultDto> SplitAsync(
        Guid tenantId, Guid rugId, SplitRugRequest request, CancellationToken ct = default)
    {
        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
        await ledgerLock.AcquireAsync(tenantId, ct);
        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش یافت نشد.");
        concurrency.Expect(rug, request.Version);

        var prefs = await LoadPreferencesAsync(tenantId, ct);
        var created = await splitter.SplitAsync(
            rug, request, new RugSplitContext(tenantId, prefs.MoneyScale, DateTimeOffset.UtcNow), ct);

        await unitOfWork.SaveChangesAsync(ct);

        var original = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش یافت نشد.");
        var fresh = await rugs.GetWithWorkflowAsync(created.Id, tenantId, ct)
            ?? throw new InvalidOperationException("بارگذاری فرش تازه بعد از برش ناموفق بود.");
        return new SplitRugResultDto(await dtos.BuildAsync(original, ct), await dtos.BuildAsync(fresh, ct));
        }, ct);
    }

    // ═════════════════════════════════════════════════════════════════════
    // شماره و پیشنهاد مقدار
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// شمارهٔ بعدی همراه حالت شمارهٔ کارگاه: فرم در حالت «خودکار» خانهٔ شماره را نوشتنی نشان نمی‌دهد (سرور متن کاربر را
    /// برای فرش تازه نادیده می‌گیرد، G.4)، و در «پیشنهاد بعدی» پیشنهاد را با Enter/Tab می‌پذیرد.
    /// </summary>
    public async Task<NextSerialDto> NextSerialAsync(Guid tenantId, CancellationToken ct = default)
    {
        var next = await serials.SuggestAsync(tenantId, ct);
        var prefs = await LoadPreferencesAsync(tenantId, ct);
        return new NextSerialDto(next, prefs.SerialMode);
    }

    /// <summary>
    /// تطبیق همیشه دقیق است (هرگز «شامل»): شمارهٔ خودِ فرش، یا عدد تکی داخل بازهٔ یک دسته. متنی که شماره نیست هم
    /// «پیدا نشد» است (اسکنر ممکن است هر چیزی بخواند).
    /// </summary>
    public async Task<RugBySerialDto?> FindBySerialAsync(Guid tenantId, string serial, CancellationToken ct = default)
    {
        if (!SerialNumber.TryParse(serial, out var parsed, out _) || parsed is null)
            return null;

        var hit = await rugs.FindBySerialAsync(tenantId, parsed, ct);
        return hit is null ? null : money.Redact(hit);
    }

    public Task<IReadOnlyList<string>> ListValuesAsync(Guid tenantId, RugValuesQuery query, CancellationToken ct = default)
        => rugs.ListDistinctValuesAsync(tenantId, query.Sanitized(), ct);

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

        audit.Record(AuditAction.Deleted, nameof(Rug), rug.Id,
            $"فرش {rug.Sku} به سطل زباله منتقل شد.", rug.Sku);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// بازگردانی از سطل زباله. شمارهٔ فرش دوباره سنجیده می‌شود (فرش حذف‌شده شماره‌اش را آزاد کرده بود، M.2 #4): اگر حالا
    /// مال فرش دیگری است، پاس سازگاری ۴۰۹ می‌دهد و فرش در سطل می‌ماند.
    /// </summary>
    public async Task<RugDto> RestoreAsync(Guid tenantId, Guid rugId, CancellationToken ct = default)
    {
        var rug = await rugs.GetDeletedAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش حذف‌شده یافت نشد.");

        rug.DeletedAt = null;
        rug.DeletedByUserId = null;
        rug.UpdatedAt = DateTimeOffset.UtcNow;

        audit.Record(AuditAction.Restored, nameof(Rug), rug.Id,
            $"فرش {rug.Sku} از سطل زباله بازگردانده شد.", rug.Sku);
        await unitOfWork.SaveChangesAsync(ct);

        // بعد از بازگردانی، فیلتر سراسری دیگر مانع نیست و می‌شود کامل خواندش
        var restored = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش یافت نشد.");
        return await dtos.BuildAsync(restored, ct);
    }

    public Task<IReadOnlyList<DeletedRugDto>> ListDeletedAsync(Guid tenantId, CancellationToken ct = default)
        => rugs.ListDeletedAsync(tenantId, ct);

    // ═════════════════════════════════════════════════════════════════════
    // مراحل
    // ═════════════════════════════════════════════════════════════════════

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
        return await dtos.BuildAsync(rug, ct);
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
        return await dtos.BuildAsync(rug, ct);
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
        return await dtos.BuildAsync(rug, ct);
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
        return await dtos.BuildAsync(rug, ct);
    }

    public async Task<RugDto> ActivateStepAsync(Guid tenantId, Guid rugId, Guid stepId, CancellationToken ct = default)
    {
        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش یافت نشد.");
        await workflowEngine.ActivateStepAsync(rug, stepId, ct);
        rug.UpdatedAt = DateTimeOffset.UtcNow;
        rugs.Update(rug);
        await unitOfWork.SaveChangesAsync(ct);
        return await dtos.BuildAsync(rug, ct);
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
        return await dtos.BuildAsync(rug, ct);
    }

    /// <summary>
    /// اعمال یک قالب گردش کار روی فرشی که هنوز مسیر ندارد (یا فقط مراحل در صف دارد).
    /// اگر مرحلهٔ تکمیل‌شده/رد‌شده داشته باشد رد می‌شود تا تاریخچه از بین نرود (از «ویرایش مسیر» استفاده شود).
    /// قالب پیش از به‌کار رفتن با کارگاه سنجیده می‌شود (E17).
    /// </summary>
    public async Task<RugDto> ApplyTemplateAsync(
        Guid tenantId, Guid rugId, Guid templateId, IReadOnlyList<Guid>? skippedOptionalStepIds, CancellationToken ct = default)
    {
        var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش یافت نشد.");

        if (rug.WorkflowSteps.Any(s => s.Status is WorkflowStepStatus.Completed or WorkflowStepStatus.Skipped))
            throw new InvalidOperationException("این فرش تاریخچهٔ مرحله دارد؛ برای تغییر مسیر از «ویرایش مسیر» استفاده کنید.");

        var template = await LoadTemplateAsync(tenantId, templateId, ct);

        var existingIds = rug.WorkflowSteps.Select(s => s.Id).ToHashSet();
        await workflowEngine.InitializeWorkflowFromTemplateAsync(rug, template, skippedOptionalStepIds, ct);
        await DropForeignProvidersAsync(tenantId, rug, ct);
        await AddNewStepsAsync(rug, existingIds, ct);
        rug.UpdatedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(ct);

        // بارگذاری دوباره برای داشتن نام مراحل (navigation ProcessStepType)
        var reloaded = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
            ?? throw new KeyNotFoundException("فرش یافت نشد.");
        return await dtos.BuildAsync(reloaded, ct);
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
        if (request.BatchId is not null) changes.Add("ورودی");

        if (changes.Count == 0)
            throw new InvalidOperationException("هیچ فیلدی برای تغییر انتخاب نشده است.");

        // گروه مقصد باید مال همین کارگاه باشد. بدون این بررسی، شناسهٔ ناموجود به نقض کلید خارجی
        // (خطای ۵۰۰ و برگشت کل عملیات) می‌رسید و شناسهٔ گروه کارگاه دیگر بی‌صدا ذخیره می‌شد.
        if (request.BatchId is Guid targetBatchId && targetBatchId != Guid.Empty)
        {
            var owned = await batches.ListAsync(b => b.Id == targetBatchId && b.TenantId == tenantId, ct);
            if (owned.Count == 0)
                throw new InvalidOperationException("ورودی انتخاب‌شده یافت نشد.");
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
    private static string? Blank(string? value)
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

    // ═════════════════════════════════════════════════════════════════════
    // کمکی‌ها
    // ═════════════════════════════════════════════════════════════════════

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

    /// <summary>
    /// قالب کارگاه، سنجیده با کارگاه (E17): قالب‌هایی که پیش از رفع نشت میان کارگاه‌ها ذخیره شده‌اند ممکن است مرحله‌ای از
    /// کارگاه دیگر داشته باشند؛ با آن قالب فرشی ساخته نمی‌شود (نام، نرخ و فرم مرحلهٔ کارگاه دیگر به فرش این کارگاه می‌چسبید).
    /// نوع مرحلهٔ سیستمی (بی کارگاه) یا اختصاصی همین کارگاه درست است.
    /// </summary>
    private async Task<WorkflowTemplate> LoadTemplateAsync(Guid tenantId, Guid templateId, CancellationToken ct)
    {
        var template = await templates.GetWithStepsAsync(templateId, tenantId, ct)
            ?? throw new InvalidOperationException("قالب فرایند یافت نشد.");

        var foreign = template.Steps.Any(s =>
            s.ProcessStepType is null || (s.ProcessStepType.TenantId is { } owner && owner != tenantId));
        if (foreign)
            throw new AppValidationException(Messages.TemplateForeignStep.Text);

        return template;
    }

    /// <summary>
    /// انجام‌دهندهٔ پیش‌فرضِ مرحله‌های قالب که مال این کارگاه نیست برداشته می‌شود (E17)؛ مرحله بی انجام‌دهنده می‌ماند و نرخ
    /// پیش‌فرض نوع مرحله را می‌گیرد، به‌جای اینکه کار این کارگاه به حساب طرفِ کارگاه دیگر برود. خود قالب دست نمی‌خورد.
    /// </summary>
    private async Task DropForeignProvidersAsync(Guid tenantId, Rug rug, CancellationToken ct)
    {
        var ids = rug.WorkflowSteps
            .Where(s => s.ServiceProviderId.HasValue)
            .Select(s => s.ServiceProviderId!.Value)
            .Distinct()
            .ToList();
        if (ids.Count == 0) return;

        var owned = (await providers.ListAsync(p => ids.Contains(p.Id) && p.TenantId == tenantId, ct))
            .Select(p => p.Id)
            .ToHashSet();

        foreach (var step in rug.WorkflowSteps.Where(s => s.ServiceProviderId is { } id && !owned.Contains(id)))
        {
            step.ServiceProviderId = null;
            step.ServiceProvider = null;
        }
    }

    private async Task<RugWriteContext> WriteContextAsync(Guid tenantId, bool isNew, CancellationToken ct)
    {
        var prefs = await LoadPreferencesAsync(tenantId, ct);
        return new RugWriteContext(tenantId, isNew, prefs.SerialMode, prefs.MoneyScale, DateTimeOffset.UtcNow, currentUser.UserId);
    }

    private async Task<TenantPreferences> LoadPreferencesAsync(Guid tenantId, CancellationToken ct)
        => (await preferences.GetAsync(tenantId, ct))?.Preferences ?? TenantPreferences.CreateDefault(tenantId);

    private static string Label(Rug rug) => PersianText.ToPersianDigits(rug.SerialNo ?? rug.Sku);

    /// <summary>ضلع برای متن تاریخچه: «۲٫۰۸» (فرهنگ ثابت، بعد ارقام فارسی).</summary>
    private static string Size(decimal meters)
        => PersianText.ToPersianDigits(meters.ToString("0.00", CultureInfo.InvariantCulture)).Replace('.', '٫');

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
