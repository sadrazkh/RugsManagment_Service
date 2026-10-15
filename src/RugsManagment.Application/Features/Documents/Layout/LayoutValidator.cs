using System.Text.RegularExpressions;
using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Documents.Data;
using RugsManagment.Application.Features.Documents.Expressions;
using RugsManagment.Application.Fields;

namespace RugsManagment.Application.Features.Documents.Layout;

/// <summary>کدهای خطای قالب؛ متن فارسی هر کد در <c>Messages</c> (فایل <c>DocumentMessages.cs</c>) است.</summary>
public static class LayoutCodes
{
    public const string Json = "E_LAYOUT_JSON";
    public const string Version = "E_LAYOUT_VERSION";
    public const string Value = "E_LAYOUT_VALUE";
    public const string Required = "E_LAYOUT_REQUIRED";
    public const string Sheets = "E_LAYOUT_SHEETS";
    public const string SheetName = "E_LAYOUT_SHEET_NAME";
    public const string TooManyColumns = "E_LAYOUT_TOO_MANY_COLUMNS";
    public const string DuplicateKey = "E_LAYOUT_DUPLICATE_KEY";
    public const string DuplicateFooter = "E_LAYOUT_DUPLICATE_FOOTER";
    public const string DuplicateStyle = "E_LAYOUT_DUPLICATE_STYLE";
    public const string UnknownField = "E_LAYOUT_UNKNOWN_FIELD";
    public const string UnknownColumn = "E_LAYOUT_UNKNOWN_COLUMN";
    public const string UnknownStyle = "E_LAYOUT_UNKNOWN_STYLE";
    public const string UnknownFooter = "E_LAYOUT_UNKNOWN_FOOTER";
    public const string LedgerField = "E_LAYOUT_LEDGER_FIELD";
    public const string Cycle = "E_LAYOUT_CYCLE";
    public const string LetterColumn = "E_LAYOUT_LETTER_COL";
    public const string Span = "E_LAYOUT_SPAN";
    public const string Overlap = "E_LAYOUT_OVERLAP";
    public const string HiddenAnchor = "E_LAYOUT_HIDDEN_ANCHOR";
    public const string QtyRequired = "E_LAYOUT_QTY_REQUIRED";
    public const string Totals = "E_LAYOUT_TOTALS";
    public const string StyleValue = "E_LAYOUT_STYLE_VALUE";
    public const string ColumnSource = "E_LAYOUT_COLUMN_SOURCE";
    public const string ColumnType = "E_LAYOUT_COLUMN_TYPE";
    public const string FooterContent = "E_LAYOUT_FOOTER_CONTENT";
    public const string Width = "E_LAYOUT_WIDTH";
}

/// <summary>ساختن <see cref="LayoutIssue"/> با متن فارسی پرشده.</summary>
public static class LayoutMessages
{
    public static LayoutIssue Issue(string code, string path, params (string Key, object? Value)[] tokens)
        => new(code, path, Messages.Text(code, tokens));

    /// <summary>خطای عبارت با مسیرِ عضوی که عبارت در آن است؛ جای نویسه ۱-پایه در نشانهٔ <c>{n}</c>.</summary>
    public static LayoutIssue Expression(ExpressionError error, string path)
        => Issue(error.Code, path, ("n", error.Position + 1), ("key", error.Key), ("max", ExpressionLimits.MaxLength));
}

/// <summary>نتیجهٔ اعتبارسنجی: خطاها و اینکه قالب فیلد پولی دارد (<c>UsesMoneyFields</c>).</summary>
public sealed record LayoutValidationResult(IReadOnlyList<LayoutIssue> Errors, bool UsesMoneyFields)
{
    public bool Ok => Errors.Count == 0;
}

/// <summary>
/// اعتبارسنج قالب (spec E.2 «LayoutValidator rules»)؛ هر خطا یک کد، یک متن فارسی و مسیر JSON دارد تا طراح (P7) آن را زیر
/// همان خانه نشان دهد:
/// <list type="bullet">
/// <item>کلید ستون، شناسهٔ پانویس و نام سبک یکتا؛</item>
/// <item>هر ارجاع پیدا شود (فیلد در کاتالوگ، <c>col.*</c>، سبک، <c>of</c>، <c>footer:&lt;id&gt;</c>، <c>checkAgainst</c> و
/// فیلد دفتر جمع‌پذیر)؛ دورِ فرمول‌ها رد؛</item>
/// <item>در برگهٔ اصلی <c>title.fromCol/toCol</c> و <c>group.keyColumn</c> کلید ستون‌اند (errata E8: فقط برگهٔ اصلی؛
/// عنوان خلاصه حرف ثابت C تا H دارد)؛ پانویسی که <c>col</c>ش شبیه حرف اکسل است رد، و کلید ستونی که خودش شبیه حرف
/// است هم رد (وگرنه همان حرف از راه کلید برمی‌گشت)؛</item>
/// <item>هر خانهٔ پانویس در ستون‌های دیده‌شده جا شود و روی دیگری نیفتد — هم بی دسته و هم با دسته؛</item>
/// <item><c>lotRow: perPiece</c> بدون ستون <c>rug.qty</c> (دست‌کم <c>hasLots</c>) رد می‌شود، وگرنه دسته‌ها یک تخته شمرده
/// می‌شدند؛</item>
/// <item>جمع‌های خلاصه از ستون‌های خلاصه؛ مقدار سبک‌ها از فهرست مجاز (کادر none|thin|outer، رنگ AARRGGBB)؛ حداکثر ۳۰
/// ستون؛ هر فیلد پولی <c>UsesMoneyFields</c> را روشن می‌کند.</item>
/// </list>
/// </summary>
public static partial class LayoutValidator
{
    private static readonly HashSet<string> HAligns = new(StringComparer.Ordinal) { "left", "center", "right", "general" };
    private static readonly HashSet<string> VAligns = new(StringComparer.Ordinal) { "top", "center", "bottom" };
    private static readonly HashSet<string> Orientations = new(StringComparer.Ordinal) { "portrait", "landscape" };
    private static readonly HashSet<string> Papers = new(StringComparer.Ordinal) { "A4", "A3", "A5", "Letter" };

    [GeneratedRegex("^[0-9A-Fa-f]{8}$")]
    private static partial Regex ArgbPattern();

    /// <summary>شبیه حرف ستون اکسل (A، K، AB، M12).</summary>
    [GeneratedRegex("^[A-Z]{1,3}[0-9]*$")]
    private static partial Regex LetterLike();

    [GeneratedRegex(@"[\[\]:*?/\\]")]
    private static partial Regex InvalidSheetChars();

    /// <summary>متن JSON را می‌خواند و اعتبارسنجی می‌کند.</summary>
    public static LayoutValidationResult Validate(string? json, IReadOnlyCollection<FieldDefDto> fields)
    {
        var (layout, error) = LayoutJson.Parse(json);
        return layout is null ? new LayoutValidationResult([error!], false) : Validate(layout, fields);
    }

    /// <summary>قالب خوانده‌شده را با کاتالوگ فیلدها می‌سنجد.</summary>
    public static LayoutValidationResult Validate(DocumentLayout layout, IReadOnlyCollection<FieldDefDto> fields)
    {
        var v = new Run(layout, fields);
        v.Execute();
        return new LayoutValidationResult(v.Errors, v.UsesMoney);
    }

    private sealed class Run(DocumentLayout layout, IReadOnlyCollection<FieldDefDto> fields)
    {
        private readonly Dictionary<string, FieldDefDto> _fields = fields
            .GroupBy(f => f.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        public List<LayoutIssue> Errors { get; } = [];

        public bool UsesMoney { get; private set; }

        private void Add(string code, string path, params (string Key, object? Value)[] tokens)
            => Errors.Add(LayoutMessages.Issue(code, path, tokens));

        public void Execute()
        {
            if (layout.SchemaVersion != 1)
                Add(LayoutCodes.Version, "$.schemaVersion");
            if (!LayoutKinds.All.Contains(layout.Kind))
                Add(LayoutCodes.Value, "$.kind", ("value", layout.Kind));
            if (layout.LotRow is not (LotRowModes.PerPiece or LotRowModes.PerLine))
                Add(LayoutCodes.Value, "$.lotRow", ("value", layout.LotRow));

            CheckPage();
            CheckStyles();

            var mains = layout.Sheets.Select((s, i) => (s, i)).Where(x => x.s.Role == SheetRoles.Main).ToList();
            var summaries = layout.Sheets.Select((s, i) => (s, i)).Where(x => x.s.Role == SheetRoles.Summary).ToList();
            if (mains.Count != 1 || summaries.Count > 1 || layout.Sheets.Any(s => s.Role is not (SheetRoles.Main or SheetRoles.Summary)))
                Add(LayoutCodes.Sheets, "$.sheets");

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < layout.Sheets.Count; i++)
            {
                var name = layout.Sheets[i].Name;
                if (string.IsNullOrWhiteSpace(name) || name.Length > 31 || InvalidSheetChars().IsMatch(name)
                    || name.StartsWith('\'') || name.EndsWith('\'') || !names.Add(name))
                    Add(LayoutCodes.SheetName, $"$.sheets[{i}].name", ("value", name));
            }

            Dictionary<string, FooterCellLayout>? footerIds = null;
            if (mains.Count >= 1)
                footerIds = CheckMain(mains[0].s, mains[0].i);
            if (summaries.Count >= 1)
                CheckSummary(summaries[0].s, summaries[0].i, footerIds ?? new Dictionary<string, FooterCellLayout>(StringComparer.Ordinal));
        }

        // ── صفحه و سبک‌ها ───────────────────────────────────────────────

        private void CheckPage()
        {
            var p = layout.Page;
            if (!Papers.Contains(p.Paper))
                Add(LayoutCodes.Value, "$.page.paper", ("value", p.Paper));
            if (!Orientations.Contains(p.Orientation))
                Add(LayoutCodes.Value, "$.page.orientation", ("value", p.Orientation));
            if (p.Scale is < 10 or > 400)
                Add(LayoutCodes.Value, "$.page.scale", ("value", p.Scale));
            if (p.Zoom is < 10 or > 400)
                Add(LayoutCodes.Value, "$.page.zoom", ("value", p.Zoom));
            if (p.RowHeight is <= 0m or > 409m)
                Add(LayoutCodes.Value, "$.page.rowHeight", ("value", p.RowHeight));
            if (p.PrintTitleRows is < 0 or > 10)
                Add(LayoutCodes.Value, "$.page.printTitleRows", ("value", p.PrintTitleRows));
            if (p.SummaryPlacement != "sheet")
                Add(LayoutCodes.Value, "$.page.summaryPlacement", ("value", p.SummaryPlacement));
            var m = p.Margins;
            foreach (var (name, value) in new[] { ("left", m.Left), ("right", m.Right), ("top", m.Top), ("bottom", m.Bottom), ("header", m.Header), ("footer", m.Footer) })
            {
                if (value is < 0m or > 5m)
                    Add(LayoutCodes.Value, $"$.page.margins.{name}", ("value", value));
            }
        }

        private void CheckStyles()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (name, style) in layout.Styles.Entries)
            {
                var path = $"$.styles.{name}";
                if (!seen.Add(name))
                    Add(LayoutCodes.DuplicateStyle, path, ("key", name));
                if (string.IsNullOrWhiteSpace(style.Font))
                    Add(LayoutCodes.StyleValue, path + ".font", ("key", name), ("value", style.Font));
                if (style.Size is < 1m or > 409m)
                    Add(LayoutCodes.StyleValue, path + ".size", ("key", name), ("value", style.Size));
                if (!BorderStyles.All.Contains(style.Border))
                    Add(LayoutCodes.StyleValue, path + ".border", ("key", name), ("value", style.Border));
                if (!HAligns.Contains(style.HAlign))
                    Add(LayoutCodes.StyleValue, path + ".hAlign", ("key", name), ("value", style.HAlign));
                if (!VAligns.Contains(style.VAlign))
                    Add(LayoutCodes.StyleValue, path + ".vAlign", ("key", name), ("value", style.VAlign));
                if (style.ColorRgb is not null && !ArgbPattern().IsMatch(style.ColorRgb))
                    Add(LayoutCodes.StyleValue, path + ".colorRgb", ("key", name), ("value", style.ColorRgb));
                if (style.FillRgb is not null && !ArgbPattern().IsMatch(style.FillRgb))
                    Add(LayoutCodes.StyleValue, path + ".fillRgb", ("key", name), ("value", style.FillRgb));
            }
        }

        private void RequireStyle(string? name, string path, bool optional = true)
        {
            if (name is null)
            {
                if (!optional)
                    Add(LayoutCodes.Required, path, ("label", "سبک"));
                return;
            }

            if (!layout.Styles.Contains(name))
                Add(LayoutCodes.UnknownStyle, path, ("key", name));
        }

        // ── برگهٔ اصلی ──────────────────────────────────────────────────

        private Dictionary<string, FooterCellLayout> CheckMain(SheetLayout sheet, int si)
        {
            var root = $"$.sheets[{si}]";
            var columns = sheet.Columns;
            if (columns.Count == 0)
                Add(LayoutCodes.Required, root + ".columns", ("label", "ستون"));
            if (columns.Count > LayoutDefaults.MaxColumns)
                Add(LayoutCodes.TooManyColumns, root + ".columns", ("max", LayoutDefaults.MaxColumns));
            RequireStyle(sheet.ColumnStyle, root + ".columnStyle");

            // کلیدها
            var byKey = new Dictionary<string, (ColumnLayout Column, int Index)>(StringComparer.Ordinal);
            for (var j = 0; j < columns.Count; j++)
            {
                var c = columns[j];
                var path = $"{root}.columns[{j}]";
                if (string.IsNullOrWhiteSpace(c.Key))
                {
                    Add(LayoutCodes.Required, path + ".key", ("label", "کلید ستون"));
                    continue;
                }

                if (!byKey.TryAdd(c.Key, (c, j)))
                    Add(LayoutCodes.DuplicateKey, path + ".key", ("key", c.Key));

                // کلیدی که خودش شبیه حرف ستون است («E») ارجاع‌ها را دوپهلو می‌کرد: پانویسِ col: "E" هم کلید بود هم حرف
                if (LetterLike().IsMatch(c.Key))
                    Add(LayoutCodes.LetterColumn, path + ".key", ("key", c.Key));
            }

            // هر ستون
            var types = new Dictionary<string, ExprType>(StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var cycleReported = new HashSet<string>(StringComparer.Ordinal);

            ExprType? ColumnTypeOf(string key)
            {
                if (!byKey.TryGetValue(key, out var entry))
                    return null;
                if (types.TryGetValue(key, out var known))
                    return known;
                if (!visiting.Add(key))
                {
                    if (cycleReported.Add(key))
                        Add(LayoutCodes.Cycle, $"{root}.columns[{entry.Index}].expr", ("key", key));
                    return ExprType.Error;
                }

                var type = InferColumn(entry.Column, $"{root}.columns[{entry.Index}]", ColumnTypeOf);
                visiting.Remove(key);
                types[key] = type;
                return type;
            }

            for (var j = 0; j < columns.Count; j++)
            {
                var c = columns[j];
                var path = $"{root}.columns[{j}]";
                if (c.Width is not { } width || width <= 0m || width >= 256m)
                    Add(LayoutCodes.Width, path + ".width", ("key", c.Key));
                if (!Visibility.All.Contains(c.EffectiveVisibleWhen))
                    Add(LayoutCodes.Value, path + ".visibleWhen", ("value", c.VisibleWhen));
                if (c.EffectiveFormat.Length > 255)
                    Add(LayoutCodes.Value, path + ".format", ("value", c.Format));
                if (c.From is not null)
                    Add(LayoutCodes.Value, path + ".from", ("value", c.From));
                RequireStyle(c.EffectiveStyle, path + ".style");
                RequireStyle(c.EffectiveHeaderStyle, path + ".headerStyle");
                if (!string.IsNullOrWhiteSpace(c.Key))
                    ColumnTypeOf(c.Key);
                else
                    InferColumn(c, path, ColumnTypeOf);
            }

            var keys = byKey.Keys.ToHashSet(StringComparer.Ordinal);

            // ستون تعداد برای هر تخته یک ردیف
            if (layout.LotRow == LotRowModes.PerPiece
                && !columns.Any(c => c.Field == RugFieldKeys.Quantity && c.EffectiveVisibleWhen is Visibility.Always or Visibility.HasLots))
                Add(LayoutCodes.QtyRequired, root + ".columns");

            // عنوان
            if (sheet.Title is { } title)
            {
                var tp = root + ".title";
                RequireColumnKey(title.FromCol, tp + ".fromCol", keys, allowLast: false);
                RequireColumnKey(title.ToCol, tp + ".toCol", keys, allowLast: true);
                if (keys.Contains(title.FromCol) && (keys.Contains(title.ToCol) || title.ToCol == LayoutDefaults.LastColumn))
                {
                    foreach (var hasLots in new[] { false, true })
                    {
                        var visible = LayoutColumns.Visible(sheet, hasLots);
                        var from = IndexOf(visible, title.FromCol);
                        var to = title.ToCol == LayoutDefaults.LastColumn ? visible.Count - 1 : IndexOf(visible, title.ToCol);
                        if (from < 0 || to < 0)
                            Add(LayoutCodes.HiddenAnchor, tp, ("key", from < 0 ? title.FromCol : title.ToCol));
                        else if (to < from)
                            Add(LayoutCodes.Value, tp + ".toCol", ("value", title.ToCol));
                    }
                }

                CheckScalarExpression(title.Expr, tp + ".expr", ExprScope.Title(this));
                RequireStyle(title.Style, tp + ".style");
            }

            // گروه و پانویس
            var footerIds = new Dictionary<string, FooterCellLayout>(StringComparer.Ordinal);
            if (sheet.Group is { } group)
            {
                var gp = root + ".group";
                if (group.By != "bale")
                    Add(LayoutCodes.Value, gp + ".by", ("value", group.By));
                RequireColumnKey(group.KeyColumn, gp + ".keyColumn", keys, allowLast: false);
                RequireStyle(group.FooterStyle, gp + ".footerStyle");
                CheckCells(sheet, group.Footer, gp + ".footer", footerIds, ExprScope.Footer(this, withBale: true), ColumnTypeOf);
            }

            if (sheet.GrandTotal is { } grand)
            {
                var gp = root + ".grandTotal";
                RequireStyle(grand.Style, gp + ".style");
                var ids = new Dictionary<string, FooterCellLayout>(StringComparer.Ordinal);
                CheckCells(sheet, grand.Cells, gp + ".cells", ids, ExprScope.Footer(this, withBale: false), ColumnTypeOf);
            }

            for (var k = 0; k < sheet.CheckAgainst.Count; k++)
            {
                var check = sheet.CheckAgainst[k];
                var cp = $"{root}.checkAgainst[{k}]";
                if (!footerIds.ContainsKey(check.FooterId))
                    Add(LayoutCodes.UnknownFooter, cp + ".footerId", ("key", check.FooterId));
                if (!_fields.TryGetValue(check.LedgerField, out var ledger))
                    Add(LayoutCodes.UnknownField, cp + ".ledgerField", ("key", check.LedgerField));
                else if (!ledger.Aggregatable)
                    Add(LayoutCodes.LedgerField, cp + ".ledgerField", ("key", check.LedgerField));
            }

            return footerIds;
        }

        private static int IndexOf(IReadOnlyList<ColumnLayout> visible, string key)
        {
            for (var i = 0; i < visible.Count; i++)
            {
                if (visible[i].Key == key)
                    return i;
            }

            return -1;
        }

        private void RequireColumnKey(string? key, string path, IReadOnlySet<string> keys, bool allowLast)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                Add(LayoutCodes.Required, path, ("label", "کلید ستون"));
                return;
            }

            if (allowLast && key == LayoutDefaults.LastColumn || keys.Contains(key))
                return;
            Add(LetterLike().IsMatch(key) ? LayoutCodes.LetterColumn : LayoutCodes.UnknownColumn, path, ("key", key));
        }

        /// <summary>نوع یک ستون: نوع فیلد یا نوع عبارت؛ ستون فقط عدد یا متن.</summary>
        private ExprType InferColumn(ColumnLayout c, string path, Func<string, ExprType?> columnType)
        {
            var hasField = !string.IsNullOrWhiteSpace(c.Field);
            var hasExpr = !string.IsNullOrWhiteSpace(c.Expr);
            if (hasField == hasExpr)
            {
                Add(LayoutCodes.ColumnSource, path, ("key", c.Key));
                return ExprType.Error;
            }

            ExprType type;
            string member;
            if (hasField)
            {
                member = ".field";
                if (!_fields.TryGetValue(c.Field!, out var field))
                {
                    Add(LayoutCodes.UnknownField, path + member, ("key", c.Field));
                    return ExprType.Error;
                }

                UsesMoney |= field.Money;
                type = DocumentFields.TypeOf(field);
            }
            else
            {
                member = ".expr";
                type = CheckExpression(c.Expr!, path + member, ExprScope.Column(this, columnType));
            }

            if (type is ExprType.Bool or ExprType.Date)
            {
                Add(LayoutCodes.ColumnType, path + member, ("key", c.Key));
                return ExprType.Error;
            }

            return type;
        }

        private void CheckCells(
            SheetLayout sheet, IReadOnlyList<FooterCellLayout> cells, string path, Dictionary<string, FooterCellLayout> ids,
            ExprScope scope, Func<string, ExprType?> columnType)
        {
            var keys = sheet.Columns.Where(c => !string.IsNullOrWhiteSpace(c.Key)).Select(c => c.Key!).ToHashSet(StringComparer.Ordinal);
            for (var k = 0; k < cells.Count; k++)
            {
                var cell = cells[k];
                var cp = $"{path}[{k}]";
                if (string.IsNullOrWhiteSpace(cell.Id))
                    Add(LayoutCodes.Required, cp + ".id", ("label", "شناسهٔ خانه"));
                else if (!ids.TryAdd(cell.Id, cell))
                    Add(LayoutCodes.DuplicateFooter, cp + ".id", ("key", cell.Id));

                RequireColumnKey(cell.Col, cp + ".col", keys, allowLast: false);
                if (cell.Span < 1)
                    Add(LayoutCodes.Span, cp + ".span", ("key", cell.Id));
                RequireStyle(cell.Style, cp + ".style");

                var contents = (cell.Text is not null ? 1 : 0) + (!string.IsNullOrWhiteSpace(cell.Expr) ? 1 : 0) + (cell.Agg is not null ? 1 : 0);
                if (contents != 1)
                    Add(LayoutCodes.FooterContent, cp, ("key", cell.Id));

                if (cell.Agg is not null)
                {
                    if (cell.Agg == AggKinds.Count)
                    {
                        if (cell.Of is not null)
                            Add(LayoutCodes.Value, cp + ".of", ("value", cell.Of));
                    }
                    else if (cell.Agg == AggKinds.Sum)
                    {
                        if (string.IsNullOrWhiteSpace(cell.Of))
                            Add(LayoutCodes.Required, cp + ".of", ("label", "ستونِ جمع"));
                        else if (!keys.Contains(cell.Of))
                            Add(LetterLike().IsMatch(cell.Of) ? LayoutCodes.LetterColumn : LayoutCodes.UnknownColumn, cp + ".of", ("key", cell.Of));
                        else if (columnType(cell.Of) is { } t && t != ExprType.Number && t != ExprType.Error)
                            Add(LayoutCodes.Value, cp + ".of", ("value", cell.Of));
                    }
                    else
                    {
                        Add(LayoutCodes.Value, cp + ".agg", ("value", cell.Agg));
                    }
                }

                if (!string.IsNullOrWhiteSpace(cell.Expr))
                {
                    var type = CheckExpression(cell.Expr, cp + ".expr", scope with { ColumnTypes = columnType });
                    if (type is ExprType.Bool or ExprType.Date)
                        Add(LayoutCodes.ColumnType, cp + ".expr", ("key", cell.Id));
                }
            }

            // جا شدن و روی هم نیفتادن، با دسته و بی دسته
            foreach (var hasLots in new[] { false, true })
            {
                var visible = LayoutColumns.Visible(sheet, hasLots);
                var taken = new bool[visible.Count];
                for (var k = 0; k < cells.Count; k++)
                {
                    var cell = cells[k];
                    if (!keys.Contains(cell.Col) || cell.Span < 1)
                        continue;
                    var cp = $"{path}[{k}]";
                    var at = IndexOf(visible, cell.Col);
                    if (at < 0)
                    {
                        if (!Errors.Any(e => e.Code == LayoutCodes.HiddenAnchor && e.Path == cp + ".col"))
                            Add(LayoutCodes.HiddenAnchor, cp + ".col", ("key", cell.Id));
                        continue;
                    }

                    if (at + cell.Span > visible.Count)
                    {
                        if (!Errors.Any(e => e.Code == LayoutCodes.Span && e.Path == cp + ".span"))
                            Add(LayoutCodes.Span, cp + ".span", ("key", cell.Id));
                        continue;
                    }

                    for (var x = at; x < at + cell.Span; x++)
                    {
                        if (taken[x])
                        {
                            if (!Errors.Any(e => e.Code == LayoutCodes.Overlap && e.Path == cp + ".col"))
                                Add(LayoutCodes.Overlap, cp + ".col", ("key", cell.Id));
                            break;
                        }

                        taken[x] = true;
                    }
                }
            }
        }

        // ── برگهٔ خلاصه ─────────────────────────────────────────────────

        private void CheckSummary(SheetLayout sheet, int si, IReadOnlyDictionary<string, FooterCellLayout> footerIds)
        {
            var root = $"$.sheets[{si}]";
            var start = LayoutColumns.Index(sheet.StartCol);
            if (start == 0)
                Add(LayoutCodes.Value, root + ".startCol", ("value", sheet.StartCol));
            RequireStyle(sheet.HeaderStyle, root + ".headerStyle");
            RequireStyle(sheet.RowStyle, root + ".rowStyle");
            RequireStyle(sheet.ColumnStyle, root + ".columnStyle");

            if (sheet.Title is { } title)
            {
                // errata E8: عنوان خلاصه حرف ثابت دارد (C تا H)، چون این برگه جابه‌جایی ستون ندارد
                var from = LayoutColumns.Index(title.FromCol);
                var to = LayoutColumns.Index(title.ToCol);
                if (from == 0)
                    Add(LayoutCodes.Value, root + ".title.fromCol", ("value", title.FromCol));
                if (to == 0 || to < from)
                    Add(LayoutCodes.Value, root + ".title.toCol", ("value", title.ToCol));
                CheckScalarExpression(title.Expr, root + ".title.expr", ExprScope.Title(this));
                RequireStyle(title.Style, root + ".title.style");
            }

            var linked = new HashSet<string>(StringComparer.Ordinal);
            if (sheet.Columns.Count == 0)
                Add(LayoutCodes.Required, root + ".columns", ("label", "ستون"));
            if (start > 0 && start + sheet.Columns.Count - 1 > 16384)
                Add(LayoutCodes.TooManyColumns, root + ".columns", ("max", LayoutDefaults.MaxColumns));
            for (var j = 0; j < sheet.Columns.Count; j++)
            {
                var c = sheet.Columns[j];
                var cp = $"{root}.columns[{j}]";
                if (c.From is null || !c.From.StartsWith(LayoutDefaults.FooterLinkPrefix, StringComparison.Ordinal))
                {
                    Add(LayoutCodes.Value, cp + ".from", ("value", c.From));
                    continue;
                }

                var id = c.From[LayoutDefaults.FooterLinkPrefix.Length..];
                if (!footerIds.ContainsKey(id))
                    Add(LayoutCodes.UnknownFooter, cp + ".from", ("key", id));
                linked.Add(id);
                if (c.Field is not null || c.Expr is not null)
                    Add(LayoutCodes.ColumnSource, cp, ("key", c.Label));
            }

            if (sheet.Totals is { } totals)
            {
                for (var k = 0; k < totals.Sum.Count; k++)
                {
                    if (!linked.Contains(totals.Sum[k]))
                        Add(LayoutCodes.Totals, $"{root}.totals.sum[{k}]", ("key", totals.Sum[k]));
                }

                for (var k = 0; k < totals.BlankBordered.Count; k++)
                {
                    if (!linked.Contains(totals.BlankBordered[k]))
                        Add(LayoutCodes.Totals, $"{root}.totals.blankBordered[{k}]", ("key", totals.BlankBordered[k]));
                }
            }
        }

        // ── عبارت‌ها ────────────────────────────────────────────────────

        private void CheckScalarExpression(string expr, string path, ExprScope scope)
        {
            var type = CheckExpression(expr, path, scope);
            if (type is ExprType.Bool or ExprType.Date)
                Add(LayoutCodes.ColumnType, path, ("key", "عنوان"));
        }

        private ExprType CheckExpression(string expr, string path, ExprScope scope)
        {
            var parsed = ExpressionParser.Parse(expr);
            if (!parsed.Ok)
            {
                Errors.Add(LayoutMessages.Expression(parsed.Error!, path));
                return ExprType.Error;
            }

            var result = TypeChecker.Check(parsed.Expression!, scope);
            foreach (var error in result.Errors)
                Errors.Add(LayoutMessages.Expression(error, path));
            foreach (var key in ExprWalker.FieldKeys(parsed.Expression!))
            {
                if (_fields.TryGetValue(key, out var field) && field.Money)
                    UsesMoney = true;
            }

            return result.Type;
        }

        internal FieldDefDto? Field(string key) => _fields.GetValueOrDefault(key);
    }

    /// <summary>بافت نوع‌سنجی هر جای قالب.</summary>
    private sealed record ExprScope(Run Owner, IReadOnlySet<string> Scopes, bool ColumnsAllowed, bool AggregatesAllowed, Func<string, ExprType?>? ColumnTypes)
        : ITypeScope
    {
        public static ExprScope Title(Run owner)
            => new(owner, new HashSet<string>(StringComparer.Ordinal) { FieldScope.Shipment, FieldScope.Tenant, FieldScope.Terms }, false, false, null);

        public static ExprScope Footer(Run owner, bool withBale)
        {
            var scopes = new HashSet<string>(StringComparer.Ordinal) { FieldScope.Shipment, FieldScope.Tenant, FieldScope.Terms };
            if (withBale)
                scopes.Add(FieldScope.Bale);
            return new ExprScope(owner, scopes, false, true, null);
        }

        public static ExprScope Column(Run owner, Func<string, ExprType?> columnTypes)
            => new(owner, new HashSet<string>(StringComparer.Ordinal)
            {
                FieldScope.Rug, FieldScope.Line, FieldScope.Bale, FieldScope.Shipment, FieldScope.Tenant, FieldScope.Terms, FieldScope.Purchase
            }, true, false, columnTypes);

        public ExprType? FieldType(string key) => Owner.Field(key) is { } f ? DocumentFields.TypeOf(f) : null;

        public bool FieldAllowed(string key) => Owner.Field(key) is { } f && Scopes.Contains(f.Scope);

        public ExprType? ColumnType(string key) => ColumnTypes?.Invoke(key);
    }
}
