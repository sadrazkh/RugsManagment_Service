using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.DTOs.Shipments;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Shipments;

/// <summary>عمل‌های روی خط‌ها (عضویت فرش در نگله) و جست‌وجوی فرش با شماره (spec C.3، C.4، I.2).</summary>
public interface ILineService
{
    /// <summary>
    /// <c>POST /api/shipments/{id}/lines/batch</c>: هر عمل جدا ذخیره می‌شود (هر کدام یک پاس سازگاری، همه در یک تراکنش) و
    /// نتیجهٔ خودش را دارد؛ شکست یکی بقیه را نمی‌اندازد. <paramref name="canOverrideRate"/> = سیاست Business (عمل <c>rate</c>).
    /// محمولهٔ کارگاه دیگر → <see cref="KeyNotFoundException"/>.
    /// </summary>
    Task<LinesBatchResultDto> BatchAsync(
        Guid tenantId, Guid shipmentId, LinesBatchRequest request, bool canOverrideRate, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>GET /api/shipments/{id}/find-rug?serial=</c>: تطبیق دقیق یا «داخل دسته» و اگر خط دارد کجا؛ پیدا نشد → null.
    /// </summary>
    Task<FindRugResultDto?> FindRugAsync(Guid tenantId, Guid shipmentId, string serial, CancellationToken cancellationToken = default);
}

/// <summary>
/// خط‌ها: افزودن (با شناسه یا شماره، با «داخل دسته» و «منتقل شود؟»)، انتقال، خارج کردن، اندازهٔ نهایی، نرخ دستی و ترتیب.
///
/// <para><b>نگله را اولین فرشش می‌سازد:</b> «افزودن» با شمارهٔ نگله‌ای که هنوز نیست همان نگله را در همان ذخیره می‌سازد؛ اگر
/// افزودن شکست بخورد ردیابی پاک می‌شود و نگله هم ساخته نمی‌شود. پس «نگلهٔ بعد» (که فقط شمارهٔ جاری را در مرورگر بالا
/// می‌برد) هرگز نگلهٔ خالی جا نمی‌گذارد (C.2).</para>
///
/// <para>قفل «قطعی»، «یک خط برای هر فرش» و «نگله مال همین محموله» را پاس سازگاری می‌سنجد (<c>ShipmentLockGuard</c>،
/// <c>BaleMembershipGuard</c>)؛ این سرویس فقط پرسش‌ها و پیام‌های زودتر را می‌دهد.</para>
/// </summary>
public sealed class LineService(
    IShipmentRepository shipments,
    IRugRepository rugs,
    IShipmentSettingsReader settingsReader,
    IConcurrencyGuard concurrency,
    IUnitOfWork unitOfWork,
    IAuditLog audit,
    ICurrentUser currentUser,
    IMoneyVisibility money,
    TimeProvider? time = null) : ILineService
{
    /// <summary>بزرگ‌ترین نرخ دستی (numeric(14,2)).</summary>
    public const decimal MaxUsdPerM2 = 999_999_999_999.99m;

    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public async Task<LinesBatchResultDto> BatchAsync(
        Guid tenantId, Guid shipmentId, LinesBatchRequest request, bool canOverrideRate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var header = await shipments.GetHeaderAsync(tenantId, shipmentId, cancellationToken)
                     ?? throw new KeyNotFoundException("محموله یافت نشد.");
        var settings = await settingsReader.GetAsync(tenantId, cancellationToken);
        var context = new OpContext(tenantId, header, settings, canOverrideRate);
        var results = new List<LineOpResultDto>(request.Ops?.Count ?? 0);

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            foreach (var op in request.Ops ?? [])
            {
                LineOpResultDto result;
                try
                {
                    result = await ApplyAsync(context, op, ct);
                }
                catch (Exception ex) when (IsOpFailure(ex))
                {
                    // ذخیرهٔ شکست‌خورده به savepoint خودش برگشته؛ هر چه بی ذخیره در ردیابی مانده (نگلهٔ تازه) هم پاک می‌شود
                    unitOfWork.Reset();
                    result = Fail(op, ex);
                }

                results.Add(result);
            }

            var done = results.Where(r => r.Ok).ToList();
            if (done.Count > 0)
            {
                audit.Record(AuditAction.LinesChanged, nameof(Shipment), header.Id,
                    $"{Persian(done.Count)} تغییر در {context.Terms.Bales} محمولهٔ {Persian(header.Number)}.", header.Title,
                    new { ops = done.GroupBy(r => r.Op).ToDictionary(g => g.Key ?? "?", g => g.Count()) });
                await unitOfWork.SaveChangesAsync(ct);
            }
        }, cancellationToken);

        var bales = await shipments.ListBalesAsync(tenantId, shipmentId, cancellationToken);
        return new LinesBatchResultDto(results, bales.Select(ShipmentMapping.ToSummary).ToList(), ShipmentMapping.Totals(bales));
    }

    public async Task<FindRugResultDto?> FindRugAsync(
        Guid tenantId, Guid shipmentId, string serial, CancellationToken cancellationToken = default)
    {
        _ = await shipments.GetHeaderAsync(tenantId, shipmentId, cancellationToken)
            ?? throw new KeyNotFoundException("محموله یافت نشد.");
        if (!SerialNumber.TryParse(serial, out var parsed, out _) || parsed is null)
            return null;

        var hit = await rugs.FindBySerialAsync(tenantId, parsed, cancellationToken);
        if (hit is null)
            return null;

        var placement = (await shipments.PlacementsAsync(tenantId, [hit.Rug.Id], cancellationToken)).FirstOrDefault();
        var inBale = placement is null
            ? null
            : new FindRugInBaleDto(placement.ShipmentId, placement.ShipmentNumber, placement.BaleId, placement.BaleNumber,
                placement.LineId, placement.ShipmentId == shipmentId, placement.ShipmentStatus);
        return new FindRugResultDto(money.Redact(hit).Rug, inBale, hit.MatchType);
    }

    // ═════════════════════════════════════════════════════════════════════
    // عمل‌ها
    // ═════════════════════════════════════════════════════════════════════

    private sealed record OpContext(Guid TenantId, ShipmentHeaderData Header, ShipmentSettings Settings, bool CanOverrideRate)
    {
        public BaleTermWords Terms => Settings.Terms;
        public Guid ShipmentId => Header.Id;
    }

    private Task<LineOpResultDto> ApplyAsync(OpContext context, LineOp op, CancellationToken ct)
        => (op.Op ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            LineOpKinds.Add => AddAsync(context, op, ct),
            LineOpKinds.Move => MoveAsync(context, op, ct),
            LineOpKinds.Remove => RemoveAsync(context, op, ct),
            LineOpKinds.Measure => MeasureAsync(context, op, ct),
            LineOpKinds.Rate => RateAsync(context, op, ct),
            LineOpKinds.Order => OrderAsync(context, op, ct),
            _ => Task.FromResult(Error(op, Messages.LineOpUnknown.Code, Messages.LineOpUnknown.Text, "op"))
        };

    private async Task<LineOpResultDto> AddAsync(OpContext context, LineOp op, CancellationToken ct)
    {
        var tenantId = context.TenantId;

        // ── کدام فرش ──
        Guid rugId;
        if (op.RugId is { } id)
        {
            rugId = id;
        }
        else if (!string.IsNullOrWhiteSpace(op.Serial))
        {
            if (!SerialNumber.TryParse(op.Serial, out var parsed, out var error) || parsed is null)
                return Error(op, error?.Code ?? Messages.SerialFormat.Code, error?.Message ?? Messages.SerialFormat.Text, "serial");

            var hit = await rugs.FindBySerialAsync(tenantId, parsed, ct);
            if (hit is null)
            {
                return Error(op, Messages.RugSerialNotFound.Code,
                    Messages.RugSerialNotFound.Format(("serial", PersianText.ToPersianDigits(parsed.Display))), "serial");
            }

            if (hit.MatchType == SerialMatchType.InRange)
            {
                var (from, to) = RangeOf(hit.Rug.SerialNo);
                return Prompt(op, new LineOpPromptDto(Messages.LineSerialInRange.Code,
                    Messages.LineSerialInRange.Format(("from", from), ("to", to)),
                    hit.Rug.Id, hit.Rug.SerialNo, LineMatchTypes.InRange, null, null, null, null, hit.Rug.Quantity));
            }

            rugId = hit.Rug.Id;
        }
        else
        {
            return Error(op, Messages.LineRugRequired.Code, Messages.LineRugRequired.Text, "serial");
        }

        var rug = await shipments.RugStateAsync(tenantId, rugId, ct)
                  ?? throw new KeyNotFoundException("فرش یافت نشد.");
        if (rug.Deleted)
            return Error(op, Messages.LineRugTrashed.Code, Messages.LineRugTrashed.Text, "rugId");
        if (rug.HasSale)
            return Error(op, Messages.LineRugSold.Code, Messages.LineRugSold.Format(context.Terms), "rugId");

        // ── کجا است ──
        var existing = await shipments.GetLineOfRugTrackedAsync(tenantId, rugId, ct);
        if (existing is not null && existing.Shipment.IsFinalized)
            return Error(op, Messages.ShipmentLocked.Code, LockMessage(existing.Shipment.Number), "rugId");

        if (existing is not null && op.Move != true)
        {
            // همان نگلهٔ مقصد: کاری نیست (اسکن دوباره)، ولی فقط اگر مقصد را با شناسه یا شماره همان نگله گفته باشند
            if (existing.ShipmentId == context.ShipmentId
                && (op.BaleId == existing.BaleId || (op.BaleId is null && op.BaleNumber == existing.Bale.Number)))
                return await OkAsync(context, op, existing.Id, ct);

            var same = existing.ShipmentId == context.ShipmentId;
            var template = same ? Messages.LineMoveSameQuestion : Messages.LineMoveQuestion;
            return Prompt(op, new LineOpPromptDto(template.Code,
                template.Format(context.Terms, ("shipment", existing.Shipment.Number), ("n", existing.Bale.Number)),
                rugId, rug.SerialNo, LineMatchTypes.Exact, existing.ShipmentId, existing.Shipment.Number,
                existing.BaleId, existing.Bale.Number, rug.Quantity));
        }

        // ── نگلهٔ مقصد (شمارهٔ تازه همین‌جا ساخته می‌شود) ──
        var (bale, created) = await TargetBaleAsync(context, op, ct);
        var seq = await PlaceAsync(context, bale, created, op.Seq, existing?.Id, ct);

        ShipmentLine line;
        if (existing is not null)
        {
            line = existing;
            line.ShipmentId = context.ShipmentId;
            line.BaleId = bale.Id;
            line.Seq = seq;
            line.UpdatedAt = _time.GetUtcNow();
        }
        else
        {
            line = new ShipmentLine
            {
                TenantId = tenantId,
                ShipmentId = context.ShipmentId,
                BaleId = bale.Id,
                RugId = rugId,
                Seq = seq,
                CreatedAt = _time.GetUtcNow()
            };
            await shipments.AddLineAsync(line, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return await OkAsync(context, op, line.Id, ct);
    }

    private async Task<LineOpResultDto> MoveAsync(OpContext context, LineOp op, CancellationToken ct)
    {
        var line = await LineOfShipmentAsync(context, op, ct);
        if (line is null)
            return Error(op, Messages.LineNotInShipment.Code, Messages.LineNotInShipment.Text, "lineId");

        var (bale, created) = await TargetBaleAsync(context, op, ct);
        if (bale.Id == line.BaleId && (op.Seq is null || op.Seq == line.Seq))
            return await OkAsync(context, op, line.Id, ct);

        line.Seq = await PlaceAsync(context, bale, created, op.Seq, line.Id, ct);
        line.BaleId = bale.Id;
        line.UpdatedAt = _time.GetUtcNow();
        await unitOfWork.SaveChangesAsync(ct);
        return await OkAsync(context, op, line.Id, ct);
    }

    private async Task<LineOpResultDto> RemoveAsync(OpContext context, LineOp op, CancellationToken ct)
    {
        var line = await LineOfShipmentAsync(context, op, ct);
        if (line is null)
            return Error(op, Messages.LineNotInShipment.Code, Messages.LineNotInShipment.Text, "lineId");

        // ردیف پیش از خروج: «برگردان» همان نگله و همان Seq را لازم دارد
        var before = (await shipments.ListRowsAsync(context.TenantId, context.ShipmentId, context.Settings.KeyField,
            lineIds: [line.Id], cancellationToken: ct)).Select(ShipmentMapping.ToRow).FirstOrDefault();

        shipments.RemoveLine(line);
        await unitOfWork.SaveChangesAsync(ct);
        return new LineOpResultDto(op.ClientOpId, true, [], before, Op: LineOpKinds.Remove);
    }

    private async Task<LineOpResultDto> MeasureAsync(OpContext context, LineOp op, CancellationToken ct)
    {
        if (op.RugId is not { } rugId)
            return Error(op, Messages.LineRugRequired.Code, Messages.LineRugRequired.Text, "rugId");

        var sameAsIntake = op.SameAsIntake == true;
        var clear = op.Clear == true;
        var hasPair = op.FinalLength is not null && op.FinalWidth is not null;
        var partial = (op.FinalLength is null) != (op.FinalWidth is null);
        if ((sameAsIntake ? 1 : 0) + (clear ? 1 : 0) + (hasPair ? 1 : 0) != 1 || partial)
            return Error(op, Messages.FinalSizeRequest.Code, Messages.FinalSizeRequest.Text, "finalLength");

        var line = await shipments.GetLineOfRugTrackedAsync(context.TenantId, rugId, ct);
        if (line is null || line.ShipmentId != context.ShipmentId)
            return Error(op, Messages.LineNotInShipment.Code, Messages.LineNotInShipment.Text, "rugId");

        var rug = await rugs.GetByIdAsync(rugId, ct);
        if (rug is null || rug.TenantId != context.TenantId || rug.DeletedAt is not null)
            throw new KeyNotFoundException("فرش یافت نشد.");
        if (op.RugVersion is { } version)
            concurrency.Expect(rug, version);

        var now = _time.GetUtcNow();
        if (clear) rug.ClearFinalSize();
        else if (sameAsIntake) rug.SetFinalSizeSameAsIntake(now, currentUser.UserId);
        else rug.SetFinalSize(op.FinalLength!.Value, op.FinalWidth!.Value, now, currentUser.UserId);
        rug.UpdatedAt = now;

        var label = rug.SerialNo is null ? rug.Sku : PersianText.ToPersianDigits(rug.SerialNo);
        audit.Record(AuditAction.FinalSizeRecorded, nameof(Rug), rug.Id,
            clear
                ? $"اندازهٔ نهایی فرش {label} پاک شد."
                : $"اندازهٔ نهایی فرش {label}: {Messages.FormatNumber(rug.FinalLength!.Value)} × {Messages.FormatNumber(rug.FinalWidth!.Value)}",
            rug.Sku);
        await unitOfWork.SaveChangesAsync(ct);
        return await OkAsync(context, op, line.Id, ct);
    }

    private async Task<LineOpResultDto> RateAsync(OpContext context, LineOp op, CancellationToken ct)
    {
        if (!context.CanOverrideRate)
            return Error(op, Messages.LineRateForbidden.Code, Messages.LineRateForbidden.Text, "usdPerM2Override");

        if (op.UsdPerM2Override is { } rate && (rate < 0 || rate > MaxUsdPerM2 || !Num.HasAtMostDecimals(rate, 2)))
        {
            return rate < 0
                ? Error(op, Messages.DeclaredNegative.Code, Messages.DeclaredNegative.Text, "usdPerM2Override")
                : Error(op, Messages.OutOfRange.Code,
                    Messages.OutOfRange.Format(("label", "دلار هر متر"), ("min", 0), ("max", MaxUsdPerM2)), "usdPerM2Override");
        }

        var line = await LineOfShipmentAsync(context, op, ct);
        if (line is null)
            return Error(op, Messages.LineNotInShipment.Code, Messages.LineNotInShipment.Text, "lineId");

        line.UsdPerM2Override = op.UsdPerM2Override;
        line.UpdatedAt = _time.GetUtcNow();
        await unitOfWork.SaveChangesAsync(ct);
        return await OkAsync(context, op, line.Id, ct);
    }

    private async Task<LineOpResultDto> OrderAsync(OpContext context, LineOp op, CancellationToken ct)
    {
        if (op.BaleId is not { } baleId)
            return Error(op, Messages.LineBaleRequired.Code, Messages.LineBaleRequired.Format(context.Terms), "baleId");

        var bale = await shipments.GetBaleTrackedAsync(context.TenantId, baleId, ct);
        if (bale is null || bale.ShipmentId != context.ShipmentId)
            return Error(op, Messages.LineBaleMismatch.Code, Messages.LineBaleMismatch.Format(context.Terms), "baleId");

        var lines = await shipments.ListLinesOfBaleTrackedAsync(context.TenantId, baleId, ct);
        var byId = lines.ToDictionary(l => l.Id);
        var wanted = (op.LineIds ?? []).Distinct().ToList();
        if (wanted.Any(id => !byId.ContainsKey(id)))
            return Error(op, Messages.LineNotInShipment.Code, Messages.LineNotInShipment.Text, "lineIds");

        // ترتیب خواسته‌شده اول، بقیه به ترتیب فعلی بعدش
        var ordered = wanted.Select(id => byId[id]).Concat(lines.Where(l => !wanted.Contains(l.Id))).ToList();
        var now = _time.GetUtcNow();
        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].Seq == i + 1) continue;
            ordered[i].Seq = i + 1;
            ordered[i].UpdatedAt = now;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return new LineOpResultDto(op.ClientOpId, true, [], Op: LineOpKinds.Order);
    }

    // ═════════════════════════════════════════════════════════════════════
    // کمک‌ها
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>خطِ ردیابی‌شدهٔ این محموله با نسخهٔ مسلح؛ خط محمولهٔ دیگر → null.</summary>
    private async Task<ShipmentLine?> LineOfShipmentAsync(OpContext context, LineOp op, CancellationToken ct)
    {
        if (op.LineId is not { } lineId) return null;
        var line = await shipments.GetLineTrackedAsync(context.TenantId, lineId, ct);
        if (line is null || line.ShipmentId != context.ShipmentId) return null;
        if (op.LineVersion is { } version)
            concurrency.Expect(line, version);
        return line;
    }

    /// <summary>
    /// نگلهٔ مقصد: با شناسه (باید مال همین محموله باشد) یا با شماره (اگر نیست، همین‌جا ساخته می‌شود و با همان ذخیره می‌رود).
    /// </summary>
    private async Task<(Bale Bale, bool Created)> TargetBaleAsync(OpContext context, LineOp op, CancellationToken ct)
    {
        if (op.BaleId is { } baleId)
        {
            var bale = await shipments.GetBaleTrackedAsync(context.TenantId, baleId, ct)
                       ?? throw new KeyNotFoundException("نگله یافت نشد.");
            if (bale.ShipmentId != context.ShipmentId)
                throw new DomainConflictException(Messages.LineBaleMismatch.Format(context.Terms), "baleId");
            return (bale, false);
        }

        if (op.BaleNumber is not { } number)
            throw AppValidationException.ForField("baleNumber", Messages.LineBaleRequired.Format(context.Terms));
        if (number < 1 || number > Bale.MaxNumber)
            throw AppValidationException.ForField("baleNumber",
                Messages.BaleNumberInvalid.Format(context.Terms, ("max", Bale.MaxNumber)));

        var existing = await shipments.FindBaleByNumberTrackedAsync(context.TenantId, context.ShipmentId, number, ct);
        if (existing is not null)
            return (existing, false);

        var created = new Bale
        {
            TenantId = context.TenantId,
            ShipmentId = context.ShipmentId,
            Number = number,
            CreatedAt = _time.GetUtcNow()
        };
        await shipments.AddBaleAsync(created, ct);
        audit.Record(AuditAction.BaleCreated, nameof(Bale), created.Id,
            $"{context.Terms.BaleOf} {Persian(number)} با اولین فرشش در محمولهٔ {Persian(context.Header.Number)} ساخته شد.",
            context.Header.Title, new { shipmentId = context.ShipmentId, number });
        return (created, true);
    }

    /// <summary>
    /// Seq در نگلهٔ مقصد: بی <paramref name="seq"/> انتها (بزرگ‌ترین + ۱)؛ با آن همان جا، و اگر خط دیگری آن را دارد آن خط و
    /// بعدی‌هایش یکی جلو می‌روند (برای «برگردان»، که فرش را در همان جای قبلی می‌گذارد).
    /// </summary>
    private async Task<int> PlaceAsync(
        OpContext context, Bale bale, bool created, int? seq, Guid? movingLineId, CancellationToken ct)
    {
        if (created)
            return seq is { } first && first >= 1 ? first : 1;

        var lines = await shipments.ListLinesOfBaleTrackedAsync(context.TenantId, bale.Id, ct);
        var others = lines.Where(l => l.Id != movingLineId).ToList();
        if (seq is not { } wanted || wanted < 1)
            return others.Count == 0 ? 1 : others.Max(l => l.Seq) + 1;

        if (others.Any(l => l.Seq == wanted))
        {
            var now = _time.GetUtcNow();
            foreach (var other in others.Where(l => l.Seq >= wanted))
            {
                other.Seq++;
                other.UpdatedAt = now;
            }
        }

        return wanted;
    }

    private async Task<LineOpResultDto> OkAsync(OpContext context, LineOp op, Guid lineId, CancellationToken ct)
    {
        var row = (await shipments.ListRowsAsync(context.TenantId, context.ShipmentId, context.Settings.KeyField,
            lineIds: [lineId], cancellationToken: ct)).Select(ShipmentMapping.ToRow).FirstOrDefault();
        return new LineOpResultDto(op.ClientOpId, true, [], row, Op: Normalized(op));
    }

    private string LockMessage(int shipmentNumber)
        => money.CanSeeMoney
            ? Messages.ShipmentLocked.Format(("number", shipmentNumber))
            : Messages.ShipmentLockedOperator.Text;

    private static LineOpResultDto Error(LineOp op, string code, string message, string? field = null)
        => new(op.ClientOpId, false, [new LineOpErrorDto(code, message, field)], Op: Normalized(op));

    private static LineOpResultDto Prompt(LineOp op, LineOpPromptDto prompt)
        => new(op.ClientOpId, false, [], Prompt: prompt, Op: Normalized(op));

    private static string? Normalized(LineOp op) => op.Op?.Trim().ToLowerInvariant();

    /// <summary>خطاهایی که فقط همان عمل را می‌اندازند (بقیه خطای سرور است و کل درخواست را برمی‌گرداند).</summary>
    private static bool IsOpFailure(Exception ex)
        => ex is DomainConflictException or AppValidationException or DomainValidationException
            or ConcurrencyConflictException or DuplicateKeyException or KeyNotFoundException;

    private static LineOpResultDto Fail(LineOp op, Exception ex)
        => ex switch
        {
            ShipmentLockedException locked => Error(op, Messages.ShipmentLocked.Code, locked.Message, locked.Field),
            DomainConflictException conflict => Error(op,
                conflict.Extensions?.TryGetValue("code", out var code) == true && code is string text ? text : Messages.Conflict.Code,
                conflict.Message, conflict.Field),
            AppValidationException validation => Error(op, Messages.ValidationFailed.Code,
                validation.Errors.Values.SelectMany(v => v).FirstOrDefault() ?? validation.Message,
                validation.Errors.Keys.FirstOrDefault()),
            DomainValidationException validation => Error(op, Messages.ValidationFailed.Code,
                validation.Errors.Values.SelectMany(v => v).FirstOrDefault() ?? validation.Message,
                validation.Errors.Keys.FirstOrDefault()),
            ConcurrencyConflictException or DuplicateKeyException => Error(op, Messages.Conflict.Code, Messages.Conflict.Text),
            KeyNotFoundException => Error(op, Messages.NotFound.Code, Messages.NotFound.Text),
            _ => Error(op, Messages.ServerError.Code, Messages.ServerError.Text)
        };

    /// <summary>دو سرِ بازهٔ یک دسته با ارقام فارسی («۳۹۱۱»، «۳۹۶۰»).</summary>
    private static (string From, string To) RangeOf(string? serialNo)
    {
        var parsed = SerialNumber.Parse(serialNo);
        return parsed is { From: { } f, To: { } t }
            ? (Persian(f), Persian(t))
            : (PersianText.ToPersianDigits(serialNo ?? string.Empty), PersianText.ToPersianDigits(serialNo ?? string.Empty));
    }

    private static string Persian(int number) => PersianText.ToPersianDigits(number.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
