using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Shipments;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Shipments;

/// <summary>آمادگی یک محموله برای «قطعی» (spec C.6).</summary>
public interface IReadinessService
{
    /// <summary>
    /// تراشه‌های آمادگی به ترتیب ثابت (بازدارنده‌ها اول). با <paramref name="templateId"/> پیش‌پرواز قالب هم از
    /// <see cref="IShipmentReadinessContributor"/>ها می‌آید. محمولهٔ کارگاه دیگر → <see cref="KeyNotFoundException"/>.
    /// </summary>
    Task<IReadOnlyList<ReadinessIssueDto>> GetAsync(
        Guid tenantId, Guid shipmentId, Guid? templateId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// درز پیش‌پرواز قالب (<c>W_NO_USD_RATE</c>، <c>W_TEMPLATE_FIELD_EMPTY</c>): فاز اسناد پیاده‌سازی خودش را به‌صورت فایل تازه
/// ثبت می‌کند و این سرویس ویرایش نمی‌شود. فقط وقتی صدا زده می‌شود که قالبی خواسته شده باشد.
/// </summary>
public interface IShipmentReadinessContributor
{
    Task<IReadOnlyList<ReadinessIssueDto>> IssuesAsync(
        Guid tenantId, Guid shipmentId, Guid templateId, CancellationToken cancellationToken);
}

/// <summary>یک نگله برای قاعده‌های آمادگی.</summary>
public sealed record ReadinessBale(Guid Id, int Number, int Lines, decimal AreaM2);

/// <summary>یک ردیف برای قاعده‌های آمادگی.</summary>
public sealed record ReadinessRow(Guid RugId, string? Serial, bool HasFinalSize, DeclaredRateSource RateSource, string? KeyValue);

/// <summary>ترجیح‌هایی که قاعده‌های آمادگی می‌خوانند.</summary>
public sealed record ReadinessSettings(
    bool RequireFinalSize, decimal? TargetAreaM2, short TolerancePercent, BaleTermWords Terms, string KeyFieldLabel);

/// <summary>
/// قاعده‌های آمادگی (C.6)، خالص. چرا جدا: همین قاعده‌ها هم در «قطعی کن» (زیر قفل) و هم در فهرست محموله‌ها (شمار
/// بازدارنده‌ها از یک SQL گروهی) به کار می‌روند و نباید دو جواب بدهند.
/// </summary>
public static class ReadinessRules
{
    public static IReadOnlyList<ReadinessIssueDto> Build(
        IReadOnlyList<ReadinessBale> bales, IReadOnlyList<ReadinessRow> rows, ReadinessSettings settings)
    {
        ArgumentNullException.ThrowIfNull(bales);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(settings);
        var terms = settings.Terms;
        var issues = new List<ReadinessIssueDto>();

        if (rows.Count == 0)
            issues.Add(new ReadinessIssueDto(Messages.ReadinessEmptyShipment.Code, Messages.ReadinessEmptyShipment.Text, 0, [], true));

        foreach (var bale in bales.Where(b => b.Lines == 0).OrderBy(b => b.Number))
        {
            issues.Add(new ReadinessIssueDto(Messages.ReadinessEmptyBale.Code,
                Messages.ReadinessEmptyBale.Format(terms, ("n", bale.Number)), 1, [], true,
                BaleId: bale.Id, BaleNumber: bale.Number));
        }

        var noFinal = rows.Where(r => !r.HasFinalSize).Select(r => r.RugId).ToList();
        if (noFinal.Count > 0)
        {
            issues.Add(new ReadinessIssueDto(Messages.ReadinessNoFinalSize.Code,
                Messages.ReadinessNoFinalSize.Format(("n", noFinal.Count)), noFinal.Count, noFinal, settings.RequireFinalSize));
        }

        foreach (var group in MissingGroups(rows))
        {
            var ids = group.Rows.Select(r => r.RugId).ToList();
            issues.Add(group.Key.Length == 0
                ? new ReadinessIssueDto(Messages.ReadinessNoDeclaredKey.Code,
                    Messages.ReadinessNoDeclaredKey.Format(("n", ids.Count), ("label", settings.KeyFieldLabel)), ids.Count, ids, true)
                : new ReadinessIssueDto(Messages.ReadinessNoDeclaredValue.Code,
                    Messages.ReadinessNoDeclaredValue.Format(("n", ids.Count), ("key", group.Display)), ids.Count, ids, true,
                    Key: group.Display));
        }

        foreach (var bale in bales.Where(b => b.Lines > 0).OrderBy(b => b.Number))
        {
            if (!DeclaredValueMath.IsOverTarget(bale.AreaM2, settings.TargetAreaM2, settings.TolerancePercent))
                continue;
            issues.Add(new ReadinessIssueDto(Messages.ReadinessBaleOverTarget.Code,
                Messages.ReadinessBaleOverTarget.Format(terms,
                    ("n", bale.Number), ("area", bale.AreaM2), ("target", settings.TargetAreaM2!.Value)),
                1, [], false, BaleId: bale.Id, BaleNumber: bale.Number));
        }

        var noSerial = rows.Where(r => string.IsNullOrWhiteSpace(r.Serial)).Select(r => r.RugId).ToList();
        if (noSerial.Count > 0)
        {
            issues.Add(new ReadinessIssueDto(Messages.ReadinessNoSerial.Code,
                Messages.ReadinessNoSerial.Format(("n", noSerial.Count)), noSerial.Count, noSerial, false));
        }

        return issues;
    }

    /// <summary>
    /// شمار تراشه‌های بازدارنده (همان <see cref="Build"/>، بی ساختن پیام‌ها): محمولهٔ خالی، هر نگلهٔ خالی، «بی اندازهٔ
    /// نهایی» وقتی لازم است، و یک تراشه برای هر مقدار یکسان‌شدهٔ فیلد کلید که نرخ ندارد.
    /// </summary>
    public static int BlockingCount(int lines, int emptyBales, int noFinalSize, bool requireFinalSize, IEnumerable<string?> missingKeyValues)
    {
        var count = 0;
        if (lines == 0) count++;
        count += emptyBales;
        if (requireFinalSize && noFinalSize > 0) count++;
        count += missingKeyValues.Select(DeclaredValueMath.KeyNormalize).Distinct(StringComparer.Ordinal).Count();
        return count;
    }

    private sealed record MissingGroup(string Key, string Display, IReadOnlyList<ReadinessRow> Rows);

    /// <summary>ردیف‌های «ندارد» به گروه هر مقدار یکسان‌شدهٔ کلید؛ پرتعدادتر اول، بعد کلید. کلید خالی آخر.</summary>
    private static IEnumerable<MissingGroup> MissingGroups(IReadOnlyList<ReadinessRow> rows)
        => rows.Where(r => r.RateSource == DeclaredRateSource.Missing)
            .GroupBy(r => DeclaredValueMath.KeyNormalize(r.KeyValue), StringComparer.Ordinal)
            .Select(g => new MissingGroup(g.Key, PersianText.Normalize(g.First().KeyValue), g.ToList()))
            .OrderBy(g => g.Key.Length == 0)
            .ThenByDescending(g => g.Rows.Count)
            .ThenBy(g => g.Key, StringComparer.Ordinal);
}

/// <inheritdoc />
public sealed class ReadinessService(
    IShipmentRepository shipments,
    IShipmentSettingsReader settingsReader,
    IEnumerable<IShipmentReadinessContributor> contributors) : IReadinessService
{
    public async Task<IReadOnlyList<ReadinessIssueDto>> GetAsync(
        Guid tenantId, Guid shipmentId, Guid? templateId = null, CancellationToken cancellationToken = default)
    {
        var header = await shipments.GetHeaderAsync(tenantId, shipmentId, cancellationToken)
                     ?? throw new KeyNotFoundException("محموله یافت نشد.");
        var settings = await settingsReader.GetAsync(tenantId, cancellationToken);
        var issues = await BuildAsync(tenantId, shipmentId, header.TargetBaleAreaM2, settings, cancellationToken);

        if (templateId is not { } template)
            return issues;

        var all = issues.ToList();
        foreach (var contributor in contributors)
            all.AddRange(await contributor.IssuesAsync(tenantId, shipmentId, template, cancellationToken));
        return all;
    }

    /// <summary>تراشه‌های بی قالب برای یک محموله (فضای کار، خانه، «قطعی کن»).</summary>
    internal async Task<IReadOnlyList<ReadinessIssueDto>> BuildAsync(
        Guid tenantId, Guid shipmentId, decimal? shipmentTarget, ShipmentSettings settings, CancellationToken cancellationToken)
    {
        var bales = await shipments.ListBalesAsync(tenantId, shipmentId, cancellationToken);
        var rows = await shipments.ListRowsAsync(tenantId, shipmentId, settings.KeyField, cancellationToken: cancellationToken);
        return Build(bales, rows, settings, shipmentTarget, await KeyLabelAsync(tenantId, settings, cancellationToken));
    }

    /// <summary>همان قاعده‌ها روی داده‌ای که از پیش خوانده شده (فضای کار، «قطعی کن»).</summary>
    internal static IReadOnlyList<ReadinessIssueDto> Build(
        IReadOnlyList<BaleData> bales, IReadOnlyList<ShipmentRowData> rows, ShipmentSettings settings,
        decimal? shipmentTarget, string keyFieldLabel)
        => ReadinessRules.Build(
            bales.Select(b => new ReadinessBale(b.Id, b.Number, b.Lines, b.AreaM2)).ToList(),
            rows.Select(r => new ReadinessRow(r.RugId, r.SerialNo, r.FinalLength is not null && r.FinalWidth is not null,
                r.HasValue ? r.RateSource : DeclaredRateSource.Missing, r.KeyValue)).ToList(),
            new ReadinessSettings(settings.RequireFinalSize, settings.TargetFor(shipmentTarget), settings.TolerancePercent,
                settings.Terms, keyFieldLabel));

    /// <summary>برچسب فارسی فیلد کلید (فیلد سفارشی: برچسب خودش).</summary>
    internal async Task<string> KeyLabelAsync(Guid tenantId, ShipmentSettings settings, CancellationToken cancellationToken)
    {
        var custom = DeclaredValueMath.CustomKeyOf(settings.KeyField);
        if (custom is null)
            return DeclaredValueMath.KeyFieldLabel(settings.KeyField);

        var fields = await shipments.CustomFieldsAsync(tenantId, cancellationToken);
        return fields.FirstOrDefault(f => string.Equals(f.Key, custom, StringComparison.Ordinal))?.Label ?? custom;
    }
}
