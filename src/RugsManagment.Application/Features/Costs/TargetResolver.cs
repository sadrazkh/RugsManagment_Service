using System.Text.RegularExpressions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Costs;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.Features.Intake;
using RugsManagment.Domain.Common;

namespace RugsManagment.Application.Features.Costs;

/// <summary>
/// «فرش‌های دیگر…» در پنل هزینه (<c>POST /api/costs/resolve-targets</c>، spec D.2): متن شماره‌ها، یک فیلتر فهرست فرش‌ها یا «همهٔ
/// انبار» را <b>همین حالا</b> به فهرست صریح فرش تبدیل می‌کند. هیچ‌وقت «فیلتر زنده» ذخیره نمی‌شود؛ <c>ScopeNote</c> نشان می‌دهد
/// فهرست چطور و کی ساخته شد («۲۴۲ فرش انبار در ۱۴۰۵/۰۶/۱۵»).
/// </summary>
public interface ITargetResolver
{
    Task<ResolveTargetsResultDto> ResolveAsync(Guid tenantId, ResolveTargetsRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// پیاده‌سازی: شماره‌ها با «،»، «,»، «؛»، فاصله یا خط تازه جدا می‌شوند؛ دسته با «-» یا «تا» («3780-3790»، «۳۷۸۰ تا ۳۷۹۰»). هر
/// شمارهٔ عددی فرش‌هایی را می‌گیرد که بازه‌شان آن را می‌پوشاند (پس «3920» دستهٔ «3911 - 3960» را می‌دهد)؛ شمارهٔ متنی همان
/// متن را. فرش سطل زباله نیست. تکه‌ای که فرشی نیافت یا شکلش درست نبود در <c>Unresolved</c> برمی‌گردد تا پنل نشانش دهد.
/// </summary>
public sealed partial class TargetResolver(
    ICostEntryRepository entries,
    IRugRepository rugs,
    TimeProvider? time = null) : ITargetResolver
{
    /// <summary>بیشترین فرشی که یک فیلتر به فهرست صریح تبدیل می‌کند.</summary>
    public const int MaxFilterRugs = 20_000;

    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public async Task<ResolveTargetsResultDto> ResolveAsync(
        Guid tenantId, ResolveTargetsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var found = new Dictionary<Guid, CostRugRef>();
        var unresolved = new List<string>();
        string? note = null;
        var today = IranDay.Today(_time.GetUtcNow());
        var jalali = PersianText.ToPersianDigits(IranDay.JalaliLatin(today));

        if (!string.IsNullOrWhiteSpace(request.SerialsText))
        {
            foreach (var token in Tokens(request.SerialsText))
            {
                if (!SerialNumber.TryParse(token, out var serial, out _) || serial is null)
                {
                    unresolved.Add(token);
                    continue;
                }

                var matches = await entries.RugsBySerialAsync(
                    tenantId, serial.IsNumeric ? null : serial.Display, serial.From, serial.To, cancellationToken);
                if (matches.Count == 0)
                {
                    unresolved.Add(PersianText.ToPersianDigits(serial.Display));
                    continue;
                }

                foreach (var rug in matches) found.TryAdd(rug.Id, rug);
            }

            note = Messages.CostScopeNoteSerials.Format(("n", found.Count));
        }

        if (request.Filter is { } filter)
        {
            var ids = new List<Guid>();
            var query = filter with { Page = 1, PageSize = RugQuery.MaxPageSize };
            while (ids.Count < MaxFilterRugs)
            {
                var page = await rugs.SearchAsync(tenantId, query.Sanitized(), cancellationToken);
                ids.AddRange(page.Items.Select(i => i.Id));
                if (!page.HasNext || page.Items.Count == 0) break;
                query = query with { Page = query.Page + 1 };
            }

            foreach (var rug in await entries.GetRugsAsync(tenantId, ids.Distinct().ToList(), cancellationToken))
            {
                if (!rug.Deleted) found.TryAdd(rug.Id, rug);
            }

            note = Messages.CostScopeNoteFilter.Format(("n", found.Count), ("date", jalali));
        }

        if (request.InStock == true)
        {
            foreach (var rug in await entries.InStockRugsAsync(tenantId, cancellationToken))
                found.TryAdd(rug.Id, rug);
            note = Messages.CostScopeNoteStock.Format(("n", found.Count), ("date", jalali));
        }

        var ordered = found.Values
            .OrderBy(r => r.SerialSort is null ? 1 : 0)
            .ThenBy(r => r.SerialSort)
            .ThenBy(r => r.SerialNo, StringComparer.Ordinal)
            .ThenBy(r => r.Sku, StringComparer.Ordinal)
            .ToList();

        return new ResolveTargetsResultDto(
            ordered.Select(r => r.Id).ToList(),
            ordered.Count,
            ordered.Sum(r => r.Quantity),
            ordered.Sum(r => r.TotalAreaM2),
            unresolved,
            note ?? Messages.CostTargetRugs.Format(("n", ordered.Count), ("pieces", ordered.Sum(r => r.Quantity))));
    }

    /// <summary>
    /// تکه‌های متن شماره‌ها: دسته‌ها («3911 - 3960»، «3911 تا 3960») یک تکه می‌مانند و بقیه با جداکننده‌ها از هم جدا می‌شوند.
    /// </summary>
    public static IReadOnlyList<string> Tokens(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var normalized = PersianText.Normalize(text);
        normalized = RangeWord().Replace(normalized, "-");
        normalized = RangeDash().Replace(normalized, "-");
        return Separators().Split(normalized).Where(t => t.Length > 0).Distinct(StringComparer.Ordinal).ToList();
    }

    [GeneratedRegex(@"(?<=\d)\s*تا\s*(?=\d)")]
    private static partial Regex RangeWord();

    [GeneratedRegex(@"\s*[-–—_]\s*")]
    private static partial Regex RangeDash();

    [GeneratedRegex(@"[\s،,؛;]+")]
    private static partial Regex Separators();
}
