using System.Globalization;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Batches;
using RugsManagment.Application.DTOs.Grid;
using RugsManagment.Application.Features.Intake;
using RugsManagment.Application.Ledger;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Services;

public interface IRugBatchService
{
    Task<IReadOnlyList<RugBatchDto>> ListAsync(Guid tenantId, CancellationToken ct = default);
    Task<RugBatchDto> CreateAsync(Guid tenantId, CreateRugBatchRequest request, CancellationToken ct = default);
    Task<RugBatchDto> UpdateAsync(Guid tenantId, Guid id, UpdateRugBatchRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task AssignRugsAsync(Guid tenantId, Guid batchId, BatchRugIdsRequest request, CancellationToken ct = default);
    Task RemoveRugsAsync(Guid tenantId, Guid batchId, BatchRugIdsRequest request, CancellationToken ct = default);

    // ── ورودی (فروشندهٔ یک روز؛ فاز ۳) ────────────────────────────────

    /// <summary>
    /// <c>POST /api/batches/today</c>: ورودیِ (کارگاه، روز ایرانِ <see cref="TodayBatchRequest.ReceivedAt"/> یا امروز،
    /// فروشندهٔ یکسان‌شده) را پیدا می‌کند یا می‌سازد (B.5). دو درخواست هم‌زمان برای یک فروشنده یک ورودی می‌گیرند.
    /// </summary>
    Task<IntakeBatchDto> TodayAsync(Guid tenantId, TodayBatchRequest request, CancellationToken ct = default);

    /// <summary>
    /// <c>GET /api/batches/recent?days=7</c>: ورودی‌های روزدارِ <paramref name="days"/> روز گذشته (امروز هم) که دست‌کم یک
    /// فرش دارند، با جمع تخته و متراژ — نوار «ورودی‌های اخیر».
    /// </summary>
    Task<IReadOnlyList<IntakeBatchDto>> RecentAsync(Guid tenantId, int days, CancellationToken ct = default);

    /// <summary>
    /// ورودیِ (روز، فروشنده) برای یک ردیف جدول: پیدا می‌کند یا در همین UnitOfWork می‌سازد، <b>بی ذخیره</b>. فراخواننده
    /// باید پیش از آن قفل حساب‌های کارگاه را در تراکنش گرفته باشد (<c>ILedgerLock</c>) تا دو ذخیرهٔ هم‌زمان دو ورودی
    /// برای یک کلید نسازند، و خودش ذخیره کند.
    /// </summary>
    Task<RugBatch> ResolveForDayAsync(Guid tenantId, DateOnly day, string? sellerName, CancellationToken ct = default);
}

/// <summary>
/// «ورودی‌ها» (RugBatch: فرش‌های یک فروشنده در یک روز، B.5، H.1).
///
/// <para><b>کلید ورودی</b> (کارگاه، روز ایران، فروشندهٔ یکسان‌شده) است و فروشندهٔ خالی خودش یک کلید است. جدول ورود هر
/// ردیف را هنگام ذخیره با همین کلید به ورودی‌اش می‌رساند («پیدا کن یا بساز»)؛ پس دو فروشنده در یک روز دو ورودی‌اند و
/// عوض کردن فروشندهٔ یک ردیف فقط همان ردیف را جابه‌جا می‌کند. یکسان‌سازی فروشنده (<see cref="SellerKeyOf"/>) «حاج‌رضا»،
/// «حاج رضا» و «حاج  رضا» را یکی می‌بیند تا یک اشتباه تایپی ورودی دوم نسازد.</para>
///
/// <para><b>شماره</b> از شمارندهٔ کارگاه (<c>batch</c>) است، نه max+1: دو ساختِ هم‌زمان هرگز یک شماره نمی‌گیرند. نام
/// پیش‌فرض «ورودی {شماره} · {فروشنده} · {تاریخ شمسی}» است.</para>
///
/// <para>ورودی‌هایی که از صفحهٔ «ورودی‌ها» با نام ساخته می‌شوند (مسیر قدیمی) روز ندارند و بیرون از کلیدند؛ پس دو ورودیِ
/// دستی در یک روز با ورودی‌های جدول برخورد نمی‌کنند.</para>
/// </summary>
public sealed class RugBatchService(
    IRugBatchRepository batches,
    IRugRepository rugs,
    IUnitOfWork unitOfWork,
    ITenantCounters counters,
    IIntakeGridRepository intake,
    ILedgerLock ledgerLock) : IRugBatchService
{
    /// <summary>کلید شمارندهٔ شمارهٔ «ورودی» (M2 آن را به بزرگ‌ترین شمارهٔ موجود رساند؛ seed هم از همین می‌گیرد).</summary>
    public const string BatchCounterKey = "batch";

    /// <summary>بیشترین بازهٔ «ورودی‌های اخیر» (روز).</summary>
    public const int MaxRecentDays = 31;

    public async Task<IReadOnlyList<RugBatchDto>> ListAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = await batches.ListByTenantAsync(tenantId, ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<RugBatchDto> CreateAsync(Guid tenantId, CreateRugBatchRequest request, CancellationToken ct = default)
    {
        // شماره از شمارندهٔ کارگاه، نه max+1: دو ساختِ هم‌زمان هرگز یک شماره نمی‌گیرند و ایندکس یکتای
        // (کارگاه، شماره) دومین ورودیِ کارگاه را با 23505 رد نمی‌کند (B.5)
        var number = await counters.NextBlockAsync(tenantId, BatchCounterKey, 1, ct);
        var batch = new RugBatch
        {
            TenantId = tenantId,
            Number = checked((int)number),
            Name = request.Name.Trim(),
            Description = request.Description,
            ReceivedAt = request.ReceivedAt ?? DateTimeOffset.UtcNow
        };
        await batches.AddAsync(batch, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(batch);
    }

    public async Task<RugBatchDto> UpdateAsync(Guid tenantId, Guid id, UpdateRugBatchRequest request, CancellationToken ct = default)
    {
        var batch = await batches.GetWithRugsAsync(id, tenantId, ct)
            ?? throw new KeyNotFoundException(Messages.IntakeBatchNotFound.Text);
        batch.Name = request.Name.Trim();
        batch.Description = request.Description;
        batch.ReceivedAt = request.ReceivedAt;

        // ورودیِ روزدار (کلید ورودی) با تاریخ رسیدنش هم‌خوان می‌ماند؛ اگر حالا با ورودیِ همان فروشنده در روز تازه
        // برخورد کند، ایندکس یکتا ۴۰۹ می‌دهد و هیچ چیزی عوض نمی‌شود
        if (batch.ReceivedOn is not null && request.ReceivedAt is { } receivedAt)
            batch.ReceivedOn = IranDay.Of(receivedAt);

        batch.UpdatedAt = DateTimeOffset.UtcNow;
        batches.Update(batch);
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(batch);
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var batch = await batches.GetWithRugsAsync(id, tenantId, ct)
            ?? throw new KeyNotFoundException(Messages.IntakeBatchNotFound.Text);
        foreach (var rug in batch.Rugs)
        {
            rug.BatchId = null;
            rugs.Update(rug);
        }
        batches.Remove(batch);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task AssignRugsAsync(Guid tenantId, Guid batchId, BatchRugIdsRequest request, CancellationToken ct = default)
    {
        // ورودی باید مال همین کارگاه باشد؛ وگرنه فرش‌های این کارگاه به ورودیِ کارگاه دیگر وصل می‌شدند.
        // ورودی کارگاه دیگر درست مثل ورودی ناموجود «پیدا نشد» است (۴۰۴) تا وجودش لو نرود.
        var batch = await batches.GetByIdAsync(batchId, ct);
        if (batch is null || batch.TenantId != tenantId)
            throw new KeyNotFoundException(Messages.IntakeBatchNotFound.Text);

        foreach (var rugId in request.RugIds.Distinct())
        {
            var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
                ?? throw new KeyNotFoundException($"فرش {rugId} یافت نشد.");
            rug.BatchId = batchId;
            rug.UpdatedAt = DateTimeOffset.UtcNow;
            rugs.Update(rug);
        }
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task RemoveRugsAsync(Guid tenantId, Guid batchId, BatchRugIdsRequest request, CancellationToken ct = default)
    {
        foreach (var rugId in request.RugIds.Distinct())
        {
            var rug = await rugs.GetWithWorkflowAsync(rugId, tenantId, ct)
                ?? throw new KeyNotFoundException($"فرش {rugId} یافت نشد.");
            if (rug.BatchId == batchId)
            {
                rug.BatchId = null;
                rug.UpdatedAt = DateTimeOffset.UtcNow;
                rugs.Update(rug);
            }
        }
        await unitOfWork.SaveChangesAsync(ct);
    }

    // ═════════════════════════════════════════════════════════════════════
    // ورودی (فروشندهٔ یک روز)
    // ═════════════════════════════════════════════════════════════════════

    public async Task<IntakeBatchDto> TodayAsync(Guid tenantId, TodayBatchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IranDay.TryParse(request.ReceivedAt, DateTimeOffset.UtcNow, out var day))
            throw AppValidationException.ForField("receivedAt", Messages.GridDateInvalid.Text);
        var seller = CleanSeller(request.SellerName);

        // قفل حساب‌های کارگاه پیش از «پیدا کن یا بساز»: درخواست هم‌زمانِ همان فروشنده پشت این یکی صبر می‌کند و ورودیِ
        // ساخته‌شده را پیدا می‌کند، به‌جای اینکه با ایندکس یکتای کلید ورودی ۴۰۹ بگیرد
        var batch = await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await ledgerLock.AcquireAsync(tenantId, token);
            var resolved = await ResolveForDayAsync(tenantId, day, seller, token);
            await unitOfWork.SaveChangesAsync(token);
            return resolved;
        }, ct);

        var summary = (await intake.SummarizeBatchesAsync(tenantId, [batch.Id], ct)).SingleOrDefault();
        return ToIntakeDto(summary ?? new IntakeBatchRow(batch, 0, 0, 0m));
    }

    public async Task<IReadOnlyList<IntakeBatchDto>> RecentAsync(Guid tenantId, int days, CancellationToken ct = default)
    {
        var span = days < 1 ? 7 : Math.Min(days, MaxRecentDays);
        var today = IranDay.Today();
        var rows = await intake.ListBatchesAsync(tenantId, today.AddDays(-(span - 1)), today, ct);
        return rows.Where(r => r.Rows > 0).Select(ToIntakeDto).ToList();
    }

    public async Task<RugBatch> ResolveForDayAsync(
        Guid tenantId, DateOnly day, string? sellerName, CancellationToken ct = default)
    {
        var seller = CleanSeller(sellerName);
        var key = SellerKeyOf(seller);

        var existing = await intake.FindBatchAsync(tenantId, day, key, ct);
        if (existing is not null)
            return existing;

        var number = checked((int)await counters.NextBlockAsync(tenantId, BatchCounterKey, 1, ct));
        var batch = new RugBatch
        {
            TenantId = tenantId,
            Number = number,
            Name = NameOf(number, seller, day),
            SellerName = seller,
            SellerKey = key,
            ReceivedOn = day,
            ReceivedAt = IranDay.ReceivedMoment(day, DateTimeOffset.UtcNow)
        };
        await batches.AddAsync(batch, ct);
        return batch;
    }

    // ═════════════════════════════════════════════════════════════════════
    // قاعده‌های خالص
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// کلید فروشنده (<c>KeyNormalize</c>، B.5): حروف عربی ← فارسی، ارقام ← لاتین، نیم‌فاصله و فاصله‌های پشت‌سرهم ← یک
    /// فاصله، حروف لاتین کوچک. نام خالی ← رشتهٔ خالی، که خودش یک کلید است («بی‌فروشنده»).
    /// </summary>
    public static string SellerKeyOf(string? sellerName)
        => PersianText.Normalize(sellerName).ToLowerInvariant();

    /// <summary>نام فروشنده همان‌طور که نوشته شد (فقط فاصلهٔ دو سر و یکسان‌سازی حروف)؛ خالی ← null. بلندتر از ستون → ۴۰۰.</summary>
    public static string? CleanSeller(string? sellerName)
    {
        if (string.IsNullOrWhiteSpace(sellerName)) return null;
        var cleaned = PersianText.Normalize(sellerName);
        if (cleaned.Length > RugBatch.SellerMaxLength)
            throw AppValidationException.ForField("batch.seller",
                Messages.IntakeSellerTooLong.Format(("n", RugBatch.SellerMaxLength)));
        return cleaned;
    }

    /// <summary>«ورودی ۱۲ · حاج رضا · ۱۴۰۵/۰۶/۱۵» (بی فروشنده: «ورودی ۱۲ · ۱۴۰۵/۰۶/۱۵»).</summary>
    public static string NameOf(int number, string? seller, DateOnly day)
    {
        var parts = new List<string> { "ورودی " + number.ToString(CultureInfo.InvariantCulture) };
        if (!string.IsNullOrWhiteSpace(seller)) parts.Add(seller);
        parts.Add(IranDay.JalaliLatin(day));
        var name = PersianText.ToPersianDigits(string.Join(" · ", parts));
        return name.Length <= 200 ? name : name[..200];
    }

    private static IntakeBatchDto ToIntakeDto(IntakeBatchRow row) => new(
        row.Batch.Id,
        row.Batch.Name,
        row.Batch.Description,
        row.Batch.ReceivedAt,
        row.Rows,
        null,
        row.Batch.SellerName,
        row.Batch.Number,
        row.Batch.ReceivedOn,
        row.Pieces,
        row.AreaM2,
        row.Batch.ReceivedOn is { } day ? IranDay.JalaliLatin(day) : null);

    private static RugBatchDto ToDto(RugBatch batch)
    {
        var stepGroups = batch.Rugs
            .Select(r => r.WorkflowSteps.FirstOrDefault(s => s.Status == WorkflowStepStatus.InProgress)?.ProcessStepType?.NameFa)
            .Where(n => n is not null)
            .GroupBy(n => n!)
            .Select(g => $"{g.Count()}× {g.Key}")
            .ToList();
        return new RugBatchDto(
            batch.Id,
            batch.Name,
            batch.Description,
            batch.ReceivedAt,
            batch.Rugs.Count,
            stepGroups.Count > 0 ? string.Join("، ", stepGroups) : null,
            batch.SellerName,
            batch.Number);
    }
}
