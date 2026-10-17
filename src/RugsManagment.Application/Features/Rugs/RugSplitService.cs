using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Costing;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Rugs;

/// <summary>زمینهٔ یک برش: کارگاه، رقم اعشار مبلغ‌ها و «چه کسی، کِی».</summary>
public sealed record RugSplitContext(Guid TenantId, short MoneyScale, DateTimeOffset Now);

/// <summary>برش یک دسته به دو ردیف (spec D.9).</summary>
public interface IRugSplitService
{
    /// <summary>
    /// <paramref name="original"/> (ردیابی‌شده، با مراحل و فروش) را برش می‌زند: تعداد و شمارهٔ خودش کم می‌شود و فرش تازه با
    /// <see cref="SplitRugRequest.Quantity"/> تخته، مراحل و سطرهای خریدش به UnitOfWork اضافه می‌شود. ذخیره نمی‌کند؛
    /// فراخواننده یک بار ذخیره می‌کند تا پاس سازگاری هر دو را در یک تراکنش ببیند (و همپوشانی شماره‌های تایپ‌شده را زیر قفل
    /// کارگاه بسنجد). فرش تازه را برمی‌گرداند.
    /// </summary>
    Task<Rug> SplitAsync(Rug original, SplitRugRequest request, RugSplitContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// برش دسته (D.9). «۵۰ تخته ← ۳۰ + ۲۰ · شماره‌ها ۳۹۱۱ تا ۳۹۴۰ و ۳۹۴۱ تا ۳۹۶۰».
///
/// <para><b>چرا برش و نه فروش جزئی:</b> دسته یک ردیف است با یک بها؛ فروش یا نگله‌بندیِ بخشی از آن یعنی دو ردیف با دو
/// سرنوشت. پس اول برش (M.2 #27)، بعد هر بخش راه خودش را می‌رود.</para>
///
/// <para><b>پول جمعش ثابت می‌ماند:</b> هر مبلغی که مال «کل ردیف» است (سطر خرید «کل»، مبلغ محاسبه‌شده، دستی و تعدیل هر
/// مرحله، قیمت فروش هدف) با «بزرگ‌ترین باقی‌مانده» به نسبت تعداد تقسیم می‌شود، پس جمع دو بخش دقیقاً همان مبلغ قبلی است.
/// سطرهای «هر متر»، «هر تخته» و «درصد» کپی می‌شوند، چون خودشان با تعداد تازه دوباره حساب می‌شوند.</para>
///
/// <para>عکس‌ها با فرش مبدأ می‌مانند. فرش تازه کد انبار تازه از شمارنده می‌گیرد. نگله، سهم ثبت‌های هزینه و مبلغ ثابت
/// (pin) در فازهای محموله و هزینه به همین برش اضافه می‌شوند (D.9: خط تازه در همان نگله، تکثیر هدف‌ها، تقسیم pinها).</para>
/// </summary>
public sealed class RugSplitService(
    IRugRepository rugs,
    IRepository<RugWorkflowStep> steps,
    IRepository<RugPurchaseLine> purchaseLines,
    IRugSkuGenerator skus,
    IAuditLog audit,
    IShipmentRepository? shipments = null) : IRugSplitService
{
    public async Task<Rug> SplitAsync(
        Rug original, SplitRugRequest request, RugSplitContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(request);

        var (keptSerial, takenSerial) = Plan(original, request);
        var taken = request.Quantity;
        var kept = original.Quantity - taken;
        // «جداشده از ۳۹۱۱ - ۳۹۶۰»: شمارهٔ دسته پیش از برش، نه بخشی که ماند. شماره با ارقام فارسی، ولی کد انبار همان‌طور
        // که روی برچسب و اسکنر است (لاتین)، چون این متن همان‌طور در تاریخچه و چیپ صفحهٔ فرش نشان داده می‌شود
        var lotText = SerialLabel(original.SerialNo, original.Sku);

        var sku = (await skus.NextAsync(context.TenantId, 1, cancellationToken))[0];
        var created = new Rug
        {
            TenantId = original.TenantId,
            Sku = sku,
            Title = original.Title,
            Origin = original.Origin,
            Pattern = original.Pattern,
            Material = original.Material,
            KnotDensity = original.KnotDensity,
            Notes = original.Notes,
            MetadataJson = original.MetadataJson,
            Status = original.Status,
            WorkflowTemplateId = original.WorkflowTemplateId,
            BatchId = original.BatchId,
            CurrentStepIndex = original.CurrentStepIndex,
            CreatedAt = context.Now
        };
        created.SetIntakeSize(original.IntakeLength, original.IntakeWidth);
        if (original is { FinalLength: { } fl, FinalWidth: { } fw })
            created.SetFinalSize(fl, fw, original.FinalMeasuredAt ?? context.Now, original.FinalMeasuredBy);

        // تعداد و شمارهٔ هر دو بخش؛ شمارهٔ مبدأ اول کوتاه می‌شود تا بازهٔ تازه هیچ‌وقت با خودش در حافظه همپوشانی نداشته باشد
        original.SetSerialAndQuantity(keptSerial, kept);
        created.SetSerialAndQuantity(takenSerial, taken);

        var members = Members(original, created, kept, taken);

        // قیمت فروش هدف برای کل ردیف است
        if (original.TargetSalePrice is { } target)
        {
            var (a, b) = SplitOptional(target, members);
            original.TargetSalePrice = a;
            created.TargetSalePrice = b;
        }

        original.UpdatedAt = context.Now;
        await rugs.AddAsync(created, cancellationToken);

        await SplitPurchaseLinesAsync(original, created, members, context, cancellationToken);
        await CopyStepsAsync(original, created, members, kept, taken, cancellationToken);

        if (shipments is not null && await shipments.GetLineOfRugTrackedAsync(context.TenantId, original.Id, cancellationToken) is { } line)
        {
            if (line.Shipment.IsFinalized)
                throw new DomainConflictException(Messages.ShipmentLockedOperator.Text);
            var following = await shipments.ListLinesOfBaleTrackedAsync(context.TenantId, line.BaleId, cancellationToken);
            foreach (var next in following.Where(x => x.Seq > line.Seq)) next.Seq++;
            await shipments.AddLineAsync(new ShipmentLine
            {
                TenantId = context.TenantId, ShipmentId = line.ShipmentId, BaleId = line.BaleId,
                RugId = created.Id, Seq = line.Seq + 1, UsdPerM2Override = line.UsdPerM2Override
            }, cancellationToken);
        }

        var takenText = SerialLabel(takenSerial?.Display, created.Sku);
        audit.Record(AuditAction.RugSplit, nameof(Rug), original.Id,
            $"{PersianText.ToPersianDigits(taken.ToString())} تخته از این دسته جدا شد ({takenText}، {created.Sku}).",
            original.Sku,
            new { kept, taken, createdRugId = created.Id, keptSerial = keptSerial?.Display, takenSerial = takenSerial?.Display });
        audit.Record(AuditAction.RugSplit, nameof(Rug), created.Id,
            $"جداشده از {lotText}",
            created.Sku,
            new { splitFromRugId = original.Id, splitFromSku = original.Sku, splitFromSerial = original.SerialNo });

        return created;
    }

    /// <summary>شمارهٔ بازرگان با ارقام فارسی، یا اگر ندارد کد انبار (لاتین، همان که روی برچسب است).</summary>
    private static string SerialLabel(string? serial, string sku)
        => serial is null ? sku : PersianText.ToPersianDigits(serial);

    // ═════════════════════════════════════════════════════════════════════
    // پیش‌شرط‌ها و شماره‌ها
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// پیش‌شرط‌ها و شمارهٔ دو بخش. خودکار فقط وقتی هر دو شماره خالی آمده‌اند و بازه دقیقاً به اندازهٔ تعداد است (فرش تازه
    /// شماره‌های آخر را می‌گیرد)؛ دستهٔ بی‌شماره بی‌شماره می‌ماند. در بقیهٔ حالت‌ها (بازهٔ بلندتر از تعداد، شمارهٔ متنی) هر دو
    /// شماره باید نوشته شوند و هر کدام تعداد خودش را بپوشاند.
    /// </summary>
    public static (SerialNumber? Kept, SerialNumber? Taken) Plan(Rug original, SplitRugRequest request)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(request);

        if (original.Quantity < 2)
            throw new AppValidationException(Messages.SplitNotALot.Text);

        if (request.Quantity < 1 || request.Quantity >= original.Quantity)
            throw AppValidationException.ForField("quantity",
                Messages.SplitCount.Format(("max", original.Quantity - 1)));

        // M.2 #27: فروش جزئی دسته نداریم؛ اول برش، بعد فروش
        if (original.Sale is not null)
            throw new DomainConflictException(Messages.SplitSold.Text, "quantity");

        var taken = request.Quantity;
        var kept = original.Quantity - taken;
        var typedKept = request.SerialFrom;
        var typedTaken = request.SerialTo;
        var serial = original.Serial;

        if (string.IsNullOrWhiteSpace(typedKept) && string.IsNullOrWhiteSpace(typedTaken))
        {
            if (serial is null)
                return (null, null);

            if (serial.IsRange && serial.Count == original.Quantity)
                return serial.SplitLast(taken);

            throw AppValidationException.ForField("serialTo", Messages.SplitSerialsRequired.Text);
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var keptSerial = ReadTyped(typedKept, kept, "serialFrom", errors);
        var takenSerial = ReadTyped(typedTaken, taken, "serialTo", errors);
        if (errors.Count > 0)
            throw new AppValidationException(errors.Count == 1 ? errors.Values.First()[0] : Messages.ValidationFailed.Text, errors);

        return (keptSerial, takenSerial);
    }

    private static SerialNumber? ReadTyped(string? text, int quantity, string field, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            errors[field] = [Messages.SplitSerialMissing.Text];
            return null;
        }

        if (!SerialNumber.TryParse(text, out var serial, out var error) || serial is null)
        {
            errors[field] = [error?.Message ?? Messages.SerialFormat.Text];
            return null;
        }

        if (!serial.Covers(quantity))
        {
            errors[field] = [serial.QuantityError(quantity).Message];
            return null;
        }

        return serial;
    }

    // ═════════════════════════════════════════════════════════════════════
    // سطرهای خرید
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// «کل»: مبلغ همان سطر (ROUND(rate، s)) به نسبت تعداد تقسیم می‌شود و نرخ هر بخش همان سهمش است؛ پس جمع خرید دو بخش
    /// دقیقاً خرید قبلی است. «هر متر»، «هر تخته» و «درصد» با همان نرخ کپی می‌شوند و پاس با تعداد تازه حسابشان می‌کند.
    /// </summary>
    private async Task SplitPurchaseLinesAsync(
        Rug original, Rug created, IReadOnlyList<LrMember> members, RugSplitContext context, CancellationToken cancellationToken)
    {
        var tenantId = context.TenantId;
        var snapshot = await purchaseLines.ListAsync(l => l.RugId == original.Id && l.TenantId == tenantId, cancellationToken);

        foreach (var line in snapshot.OrderBy(l => l.SortOrder))
        {
            var rate = line.Rate;
            if (line.Basis == CostBasis.Total)
            {
                var (keptRate, takenRate) = SplitAmount(Num.Round(line.Rate, context.MoneyScale), context.MoneyScale, members);
                var tracked = await purchaseLines.GetByIdAsync(line.Id, cancellationToken)
                              ?? throw new KeyNotFoundException(Messages.PurchaseLineUnknown.Text);
                tracked.Rate = keptRate;
                tracked.UpdatedAt = context.Now;
                rate = takenRate;
            }

            await purchaseLines.AddAsync(new RugPurchaseLine
            {
                TenantId = line.TenantId,
                RugId = created.Id,
                CategoryId = line.CategoryId,
                Label = line.Label,
                Basis = line.Basis,
                Rate = rate,
                SortOrder = line.SortOrder,
                Notes = line.Notes
            }, cancellationToken);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // مراحل
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// مراحل کپی می‌شوند (همان نوع، ترتیب، وضعیت، انجام‌دهنده و تاریخ‌ها)؛ مبلغ محاسبه‌شده، مبلغ دستی و تعدیل که برای کل
    /// ردیف‌اند به نسبت تعداد تقسیم می‌شوند. عکس تعدادِ قیمت‌گذاری هم به تعداد هر بخش می‌رسد تا قیمت‌گذاری دوباره روی
    /// همان بخش باشد.
    /// </summary>
    private async Task CopyStepsAsync(
        Rug original, Rug created, IReadOnlyList<LrMember> members, int kept, int taken, CancellationToken cancellationToken)
    {
        foreach (var step in original.WorkflowSteps.OrderBy(s => s.OrderIndex).ToList())
        {
            var (keptCalculated, takenCalculated) = SplitOptional(step.CalculatedCost, members);
            var (keptManual, takenManual) = SplitOptional(step.ManualCostOverride, members);
            var (keptAdjustment, takenAdjustment) = SplitOptional(step.Adjustment, members);

            var copy = new RugWorkflowStep
            {
                RugId = created.Id,
                ProcessStepTypeId = step.ProcessStepTypeId,
                OrderIndex = step.OrderIndex,
                IsOptional = step.IsOptional,
                Status = step.Status,
                ServiceProviderId = step.ServiceProviderId,
                StartedAt = step.StartedAt,
                CompletedAt = step.CompletedAt,
                CompletedByUserId = step.CompletedByUserId,
                CompletedByName = step.CompletedByName,
                CalculatedCost = takenCalculated,
                ManualCostOverride = takenManual,
                AppliedPricingModel = step.AppliedPricingModel,
                AppliedUnitRate = step.AppliedUnitRate,
                PricingConfigJson = step.PricingConfigJson,
                Adjustment = takenAdjustment,
                FieldValuesJson = step.FieldValuesJson,
                Notes = step.Notes,
                PricedLength = step.PricedLength,
                PricedWidth = step.PricedWidth,
                PricedQuantity = step.PricedQuantity is null ? null : taken
            };

            step.CalculatedCost = keptCalculated;
            step.ManualCostOverride = keptManual;
            step.Adjustment = keptAdjustment;
            if (step.PricedQuantity is not null) step.PricedQuantity = kept;

            await steps.AddAsync(copy, cancellationToken);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // تقسیم مبلغ
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>دو سهم‌بر با وزن تعداد هر بخش؛ شکستن تساوی با شماره و شناسه (همان قاعدهٔ LR همه‌جا).</summary>
    private static IReadOnlyList<LrMember> Members(Rug original, Rug created, int kept, int taken) =>
    [
        new(original.Id, kept, original.SerialSort),
        new(created.Id, taken, created.SerialSort)
    ];

    private static (decimal Kept, decimal Taken) SplitAmount(decimal amount, int scale, IReadOnlyList<LrMember> members)
    {
        var shares = LargestRemainder.Split(amount, scale, members).Shares;
        return (shares[0], shares[1]);
    }

    /// <summary>
    /// مبلغ اختیاری در همان تعداد رقم اعشار خودش (حداکثر ۶) تقسیم می‌شود تا جمع دو سهم دقیقاً خودش باشد. دادهٔ قدیمیِ
    /// بی‌دقتِ ستون numeric بی‌حد که بیش از ۶ رقم اعشار داشت اول به ۶ رقم گرد می‌شود.
    /// </summary>
    private static (decimal? Kept, decimal? Taken) SplitOptional(decimal? amount, IReadOnlyList<LrMember> members)
    {
        if (amount is not { } value) return (null, null);
        var scale = ScaleOf(value);
        var (a, b) = SplitAmount(Num.Round(value, scale), scale, members);
        return (a, b);
    }

    /// <summary>تعداد رقم اعشار واقعیِ یک مبلغ (بدون صفرهای انتهایی)، حداکثر ۶.</summary>
    public static int ScaleOf(decimal value)
    {
        var scale = 0;
        var probe = Math.Abs(value);
        while (scale < 6 && decimal.Truncate(probe) != probe)
        {
            probe *= 10m;
            scale++;
        }

        return scale;
    }
}
