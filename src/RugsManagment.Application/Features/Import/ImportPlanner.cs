using System.Globalization;
using System.Text.RegularExpressions;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Import;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;
using RugsManagment.Application.Features.Shipments;
namespace RugsManagment.Application.Features.Import;

/// <summary>طرح خالص ورود: هیچ ردیفی از فایل پیش از بازبینی در دیتابیس نوشته نمی‌شود.</summary>
public static partial class ImportPlanner
{
    public static decimal? Number(string? value)
    {
        var text = PersianText.Normalize(value).Replace("٬", "", StringComparison.Ordinal).Replace(",", "", StringComparison.Ordinal).Replace('٫', '.');
        return decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var n) ? n : null;
    }
    public static (string? Title, DateOnly? Date) Title(SheetGrid sheet, int header)
    {
        for (var row = header - 1; row >= 1; row--)
        {
            var title = sheet.RowText(row).Trim();
            var match = TitleDate().Match(PersianText.Normalize(title));
            if (!match.Success) continue;
            try
            {
                var calendar = new PersianCalendar();
                return (title, DateOnly.FromDateTime(calendar.ToDateTime(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                    int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture), 0, 0, 0, 0)));
            }
            catch (ArgumentOutOfRangeException) { return (title, null); }
        }
        return (null, null);
    }
    public static ImportPreviewDto Plan(SheetGrid sheet, int header, IReadOnlyDictionary<string, int> mapping,
        ImportOptions options, IReadOnlyDictionary<int, Dictionary<string, string?>>? overrides = null,
        IReadOnlyDictionary<string, decimal>? rateTable = null, string rateKeyField = "rug.origin", decimal? fallbackRate = null,
        IReadOnlyList<int>? skippedRows = null)
    {
        if (!mapping.ContainsKey("serial") || !mapping.ContainsKey("length") || !mapping.ContainsKey("width"))
            throw new DomainValidationException(ImportMessages.Text("E_NO_HEADER"), "mapping");
        if (header < 1 || header >= sheet.LastRow) throw new DomainValidationException("ردیف سرستون را درست انتخاب کنید.", "headerRow");
        if (mapping.Values.Any(c => c < 1 || c > sheet.LastColumn) || mapping.Values.Distinct().Count() != mapping.Count)
            throw new DomainValidationException("برای هر مشخصه یک ستون جدا انتخاب کنید.", "mapping");
        var rows = new List<ImportRowDto>();
        var skipped = skippedRows?.ToHashSet() ?? [];
        var serials = new List<(SerialNumber Serial, ImportRowDto Row)>();
        var global = new List<ImportIssueDto>();
        var rates = new Dictionary<string, decimal>();
        var summary = false;
        long? previous = null;
        int? currentBale = null;
        for (var r = header + 1; r <= sheet.LastRow; r++)
        {
            if (sheet.Text(r, 4) is "نگله" or "عدل" && sheet.RowText(r).Contains("تعداد فرش", StringComparison.Ordinal)) summary = true;
            var kind = RowClassifier.Classify(sheet, r, mapping["serial"], summary);
            if (kind is SheetRowKind.Title or SheetRowKind.Header) { global.Add(ImportMessages.Issue("I_REPEATED_HEADER")); continue; }
            if (kind == SheetRowKind.Summary) { if (!global.Any(i => i.Code == "I_SUMMARY_DETECTED")) global.Add(ImportMessages.Issue("I_SUMMARY_DETECTED")); continue; }
            if (kind == SheetRowKind.BaleFooter)
            {
                if (currentBale is { } bale && Number(sheet.Text(r, 3)) is { } label && label != bale)
                    rows.Add(new(r, "skip", currentBale, new(), [ImportMessages.Issue("W_FOOTER_LABEL")]));
                continue;
            }
            if (kind != SheetRowKind.Data && !(kind == SheetRowKind.Unknown
                && new[] { "length", "width", "qty", "origin", "material", "pattern", "bg_color", "border_color", "usdPerM2" }
                    .Any(field => mapping.TryGetValue(field, out var column) && !string.IsNullOrWhiteSpace(sheet.Text(r, column))))) continue;
            var values = mapping.ToDictionary(k => k.Key, k => (string?)sheet.Text(r, k.Value));
            if (overrides?.TryGetValue(r, out var changes) == true)
                foreach (var (key, value) in changes) values[key] = value;
            if (skipped.Contains(r))
            {
                // انتخاب کاربر صریح می‌ماند؛ حتی دادهٔ نامعتبر را برای تشخیص و برگرداندن همان ردیف دست‌نخورده نگه می‌داریم.
                int? skippedBale = null;
                if (options.Mode == ImportMode.Declaration && mapping.TryGetValue("bale", out var skippedBaleColumn))
                {
                    skippedBale = BaleResolver.Resolve(sheet, r, skippedBaleColumn).Number;
                    if (Number(values.GetValueOrDefault("bale")) is { } b && b is >= 1 and <= 9999 && b == decimal.Truncate(b)) skippedBale = (int)b;
                }
                rows.Add(new(r, "skip", skippedBale, values, [ImportMessages.Issue("I_ROW_SKIPPED")]));
                currentBale = skippedBale;
                continue;
            }
            var issues = new List<ImportIssueDto>();
            SerialNumber? serial = null;
            try { serial = SerialNumber.Parse(values.GetValueOrDefault("serial")); }
            catch (DomainValidationException ex)
            {
                issues.Add(ImportMessages.Issue(ex.Message == SerialNumber.ReversedErrorMessage ? "E_RANGE_REVERSED"
                    : ex.Message == SerialNumber.SpanErrorMessage ? "E_RANGE_TOO_LONG" : "E_SERIAL_INVALID", "serial"));
            }
            if (serial is null && !issues.Any(i => i.Field == "serial")) issues.Add(ImportMessages.Issue("E_SERIAL_INVALID", "serial"));
            var qty = LotParser.Quantity(serial, values.GetValueOrDefault("qty"), values.GetValueOrDefault("material"), issues);
            values["qty"] = qty.ToString(CultureInfo.InvariantCulture);
            if (serial is not null) values["serial"] = serial.Display;
            if (KnownValueSplitter.Apply(sheet, r, mapping, values)) issues.Add(ImportMessages.Issue("W_MERGED_SPLIT"));
            // اصلاح دستی مشخصات باید بر پیشنهاد ادغام اولویت داشته باشد؛ شماره و تعداد همچنان یکسان‌سازی می‌شوند.
            if (overrides?.TryGetValue(r, out var explicitValues) == true)
                foreach (var (key, value) in explicitValues) values[key] = value;
            values["qty"] = qty.ToString(CultureInfo.InvariantCulture);
            if (serial is not null) values["serial"] = serial.Display;
            foreach (var field in new[] { "length", "width" })
            {
                var n = Number(values.GetValueOrDefault(field));
                if (mapping.TryGetValue(field, out var dimensionColumn) && sheet.Cell(r, dimensionColumn)?.Snapped == true)
                    issues.Add(ImportMessages.Issue("W_DIM_SNAPPED", field));
                if (n is null) { issues.Add(ImportMessages.Issue("E_DIM_MISSING", field)); continue; }
                var rounded = Num.Round(n.Value, 2);
                if (n != rounded && decimal.Abs(n.Value - rounded) <= 0.000000001m)
                { n = rounded; issues.Add(ImportMessages.Issue("W_DIM_SNAPPED", field)); }
                if (n > 12m)
                {
                    issues.Add(ImportMessages.Issue("W_DIM_CM", field));
                    if (options.ConvertCentimeters) n /= 100m;
                }
                if (n is < 0.05m or > 30m || n != Num.Round(n.Value, 2)) issues.Add(ImportMessages.Issue("E_DIM_INVALID", field));
                values[field] = n.Value.ToString(CultureInfo.InvariantCulture);
            }
            foreach (var field in new[] { "bg_color", "border_color", "pattern", "origin", "material" })
            {
                if (string.IsNullOrWhiteSpace(values.GetValueOrDefault(field)))
                { values[field] = null; issues.Add(ImportMessages.Issue("W_MISSING_FIELD", field)); }
                else if (values[field]!.Length > 200) { values[field] = values[field]![..200]; }
            }
            var rate = Number(values.GetValueOrDefault("usdPerM2"));
            if (rate is null && rateTable is not null)
            {
                var key = DeclaredValueMath.KeyValueOf(rateKeyField, values.GetValueOrDefault("title"), values.GetValueOrDefault("origin"),
                    values.GetValueOrDefault("pattern"), values.GetValueOrDefault("material"), System.Text.Json.JsonSerializer.Serialize(values));
                rate = DeclaredValueMath.Resolve(null, key, rateTable, fallbackRate).Rate;
                if (rate is not null) values["usdPerM2"] = rate.Value.ToString(CultureInfo.InvariantCulture);
            }
            if (rate is null) issues.Add(ImportMessages.Issue("W_NO_DECLARED_VALUE", "usdPerM2"));
            else if (rate is < 0m or > 999_999_999_999.99m || rate != Num.Round(rate.Value, 2))
                issues.Add(new("E_RATE_INVALID", "error", "ارزش دلاری باید نامنفی و با حداکثر دو رقم اعشار باشد.", "usdPerM2"));
            else if (values.GetValueOrDefault("origin") is { Length: > 0 } origin) rates.TryAdd(origin, rate.Value);
            int? baleNumber = null;
            if (options.Mode == ImportMode.Declaration && mapping.TryGetValue("bale", out var baleColumn))
            {
                var resolved = BaleResolver.Resolve(sheet, r, baleColumn);
                baleNumber = resolved.Number;
                if (overrides?.TryGetValue(r, out var change) == true && change.TryGetValue("bale", out var changed) && Number(changed) is { } bn && bn is >= 1 and <= 9999 && bn == decimal.Truncate(bn)) baleNumber = (int)bn;
                if (resolved.Adopted) issues.Add(ImportMessages.Issue("W_ROW_ADOPTED", "bale"));
            }
            if (options.Mode == ImportMode.Declaration && baleNumber is null)
                issues.Add(new("E_BALE_MISSING", "error", "شمارهٔ نگلهٔ این ردیف مشخص نیست.", "bale"));
            currentBale = baleNumber;
            if (serial?.SortKey is { } sort)
            { if (previous is { } last && sort < last) issues.Add(ImportMessages.Issue("W_SERIAL_OUT_OF_SEQUENCE", "serial")); previous = Math.Max(previous ?? sort, sort); }
            var row = new ImportRowDto(r, issues.Any(i => i.Severity == "error") ? "error" : "create", baleNumber, values, issues);
            rows.Add(row);
            if (serial is null) continue;
            foreach (var other in serials)
            {
                var equal = serial.Display == other.Serial.Display;
                var overlap = serial.From is { } from && other.Serial.From is { } of && from <= other.Serial.To && of <= serial.To;
                if (!equal && !overlap) continue;
                var code = equal ? "E_SERIAL_DUP_IN_FILE" : "E_SERIAL_OVERLAP_IN_FILE";
                AddSerialConflict(row, code, other.Row.SheetRow);
                AddSerialConflict(other.Row, code, row.SheetRow);
            }
            serials.Add((serial, row));
        }
        if (Title(sheet, header).Date is not null) global.Add(ImportMessages.Issue("I_TITLE_DATE"));
        rows = rows.Select(r => r.Issues.Any(i => i.Severity == "error") ? r with { Action = "error" } : r).ToList();
        var reconciliation = Reconciler.Reconcile(sheet, rows, options.Mode, global, mapping);
        var summaryIssues = rows.SelectMany(r => r.Issues).Concat(global).GroupBy(i => i.Code)
            .Select(g => new IssueSummaryDto(g.Key, g.First().Severity, g.First().Message, g.Count())).ToArray();
        return Build(rows, reconciliation, summaryIssues, rates, 0);
    }

    /// <summary>هر علت یک بار روی ردیف می‌آید و شمارهٔ تمام ردیف‌های متعارض را برای تصمیم کاربر همراه دارد.</summary>
    private static void AddSerialConflict(ImportRowDto row, string code, int otherRow)
    {
        var issues = (List<ImportIssueDto>)row.Issues;
        var index = issues.FindIndex(i => i.Code == code);
        if (index < 0)
        {
            issues.Add(ImportMessages.Issue(code, "serial") with { RelatedRows = [otherRow] });
            return;
        }
        var issue = issues[index];
        issues[index] = issue with { RelatedRows = (issue.RelatedRows ?? []).Append(otherRow).Distinct().Order().ToArray() };
    }
    public static ImportPreviewDto Build(IReadOnlyList<ImportRowDto> rows, IReadOnlyList<ReconciliationRowDto> reconciliation,
        IReadOnlyList<IssueSummaryDto> issues, IReadOnlyDictionary<string, decimal> rates, uint version)
    {
        var included = rows.Where(r => r.Action is "create" or "attach").ToArray();
        return new(new(included.Length, included.Sum(Qty), included.Select(r => r.Bale).Where(b => b.HasValue).Distinct().Count(),
                included.Sum(Area), included.Sum(Usd), rows.Count(r => r.Action == "error"),
                issues.Where(i => i.Severity == "warning").Sum(i => i.Count), rows.Count(r => r.Action == "attach"), rows.Count(r => r.Action == "skip" && r.Values.ContainsKey("serial"))),
            reconciliation, rows, issues, rates, version);
    }
    public static int Qty(ImportRowDto row) => (int)(Number(row.Values.GetValueOrDefault("qty")) ?? 1);
    public static decimal Area(ImportRowDto row) => Num.Round((Number(row.Values.GetValueOrDefault("length")) ?? 0) * (Number(row.Values.GetValueOrDefault("width")) ?? 0), 2) * Qty(row);
    public static decimal Usd(ImportRowDto row) => Num.Round(Area(row) / Qty(row) * (Number(row.Values.GetValueOrDefault("usdPerM2")) ?? 0), 2) * Qty(row);
    [GeneratedRegex(@"(\d{4})[./](\d{1,2})[./](\d{1,2})")]
    private static partial Regex TitleDate();
}
