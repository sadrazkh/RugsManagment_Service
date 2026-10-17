using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Documents.Data;
using RugsManagment.Application.Features.Documents.Expressions;
using RugsManagment.Application.Features.Documents.Layout;
using RugsManagment.Application.Fields;

namespace RugsManagment.Application.Features.Documents.Composer;

/// <summary>گزینه‌های ساختن سند.</summary>
/// <param name="MaxGroups">پیش‌نمایش: حداکثر این تعداد نگله (یا ردیف در سند بی‌گروه)؛ فایل و چاپ همیشه کامل‌اند.</param>
/// <param name="FallbackScale">حالت جایگزین E.6 #5 (<c>fitToPage = 0</c> با مقیاس صحیح). آزمایش روز اول نشان داد لازم
/// نیست؛ برای اطمینان نگه داشته شده.</param>
/// <param name="Fields">کاتالوگ فیلدها برای نوعِ فیلدهای سفارشی؛ پیش‌فرض <see cref="DocumentFields.Catalogue"/>.</param>
public sealed record ComposeOptions(int? MaxGroups = null, bool FallbackScale = false, IReadOnlyCollection<FieldDefDto>? Fields = null);

/// <summary>
/// سازندهٔ سند (spec E.5 گام ۲ تا ۴): از قالب و <see cref="DocumentData"/> مدل <see cref="RenderModel"/> را می‌سازد —
/// ردیف عنوان، سرستون، گروه هر نگله (ردیف‌های داده و پانویس)، برگهٔ خلاصه و شکستن‌های دستی صفحه — بعد نشانی‌ها را پیوند
/// می‌دهد (<see cref="FormulaResolver"/>) و همهٔ مقدارها را با قاعده‌های <c>Num</c> حساب می‌کند (<see cref="Evaluator"/>).
///
/// قاعده‌های سند اظهار (E.7، E.9): هر خط یک ردیف «برای یک تخته» (دسته با تعداد ≠ ۱ در ستون «تعداد»، که فقط وقتی دسته‌ای
/// هست دیده می‌شود)؛ شمارهٔ نگله وقتی نگله ≥ ۲ ردیف دارد عمودی ادغام می‌شود؛ نگلهٔ خالی کنار گذاشته می‌شود
/// (<c>W_EMPTY_BALE</c>)؛ شمارهٔ تمام‌رقمی خانهٔ عددی است؛ متنِ داده که با <c>= + - @</c> شروع شود پیشوند گیومه می‌گیرد.
/// </summary>
public static class DocumentComposer
{
    /// <summary>پهنای پیش‌فرض ستون‌ها (همان نمونهٔ بازرگان).</summary>
    public const decimal DefaultColumnWidth = 9.109375m;

    /// <summary>قلم Normal کتاب کار؛ B Nazanin فقط از سبک ستون‌ها و خانه‌ها می‌آید (E.7: قاعدهٔ پهنای صفحه به آن بسته است).</summary>
    public const string NormalFont = "Calibri";

    public const decimal NormalFontSize = 11m;

    /// <summary>پسوند سبکِ مشتقِ بی‌کادر (ردیف «=» خلاصه).</summary>
    public const string NoBorderSuffix = "-noborder";

    public static RenderModel Compose(DocumentLayout layout, DocumentData data, ComposeOptions? options = null)
    {
        var run = new Run(layout, data, options ?? new ComposeOptions());
        return run.Execute();
    }

    private sealed class Run
    {
        private readonly DocumentLayout _layout;
        private readonly DocumentData _data;
        private readonly ComposeOptions _options;
        private readonly Dictionary<string, FieldDefDto> _fields;
        private readonly Dictionary<string, RenderStyle> _styles = new(StringComparer.Ordinal);
        private readonly List<DocumentIssue> _warnings = [];
        private readonly Dictionary<string, Expr> _parsed = new(StringComparer.Ordinal);
        private readonly bool _hasLots;
        private bool _truncated;

        public Run(DocumentLayout layout, DocumentData data, ComposeOptions options)
        {
            _layout = layout;
            _data = data;
            _options = options;
            _fields = (options.Fields ?? DocumentFields.Catalogue(terms: data.Terms))
                .GroupBy(f => f.Key, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            _hasLots = data.HasLots;
            foreach (var (name, style) in layout.Styles.Entries)
            {
                _styles.TryAdd(name, new RenderStyle(style.Font, style.Size, style.ColorRgb, style.FillRgb, style.Border,
                    style.HAlign, style.VAlign, style.ShrinkToFit ?? false));
            }
        }

        public RenderModel Execute()
        {
            var main = _layout.Main ?? throw new AppValidationException(Messages.Text(LayoutCodes.Sheets));
            var mainResult = ComposeMain(main);
            var sheets = new List<RenderSheet> { mainResult.Sheet };
            if (_layout.Summary is { } summary)
                sheets.Add(ComposeSummary(summary, main, mainResult));

            var page = _layout.Page;
            var fallback = _options.FallbackScale;
            var renderPage = new RenderPage(
                PaperSize(page.Paper),
                page.Paper,
                page.Orientation,
                FitToPage: page.FitToWidth && !fallback,
                FitToWidth: 1,
                FitToHeight: 0,
                Scale: fallback && mainResult.Sheet.Pages is { } plan ? plan.WrittenScale : page.Scale,
                Margins(),
                page.Zoom,
                page.Rtl,
                page.RowHeight,
                DefaultColumnWidth,
                NormalFont,
                NormalFontSize,
                _data.IsDraft && !string.IsNullOrEmpty(page.DraftHeader) ? page.DraftHeader : null,
                page.KeepGroupTogether);

            return new RenderModel(_layout.Kind, renderPage, sheets, _styles, _warnings, _data.IsDraft, _hasLots, _truncated);
        }

        private RenderMargins Margins()
        {
            var m = _layout.Page.Margins;
            return new RenderMargins(m.Left, m.Right, m.Top, m.Bottom, m.Header, m.Footer);
        }

        // ── برگهٔ اصلی ──────────────────────────────────────────────────

        private sealed record GroupResult(int Number, int FooterRow, Dictionary<string, (string Address, DocValue Value)> Anchors);

        private sealed record MainResult(RenderSheet Sheet, SheetColumnMap Map, IReadOnlyList<GroupResult> Groups);

        private MainResult ComposeMain(SheetLayout sheet)
        {
            var map = new SheetColumnMap(sheet, _hasLots, _layout.LotRow);
            var visible = map.Visible;
            var rows = new List<RenderRow>();
            var merges = new List<CellRange>();
            var height = _layout.Page.RowHeight;
            var row = 0;
            var columnStyle = sheet.ColumnStyle;

            // ردیف عنوان
            if (sheet.Title is { } title)
            {
                row++;
                var from = Math.Max(1, map.IndexOf(title.FromCol));
                var to = title.ToCol == LayoutDefaults.LastColumn ? visible.Count : map.IndexOf(title.ToCol);
                if (to < from)
                    to = from;
                var value = EvaluateScalar(title.Expr, "$.title.expr", bale: null);
                var style = Style(title.Style, LayoutDefaults.HeaderStyle);
                rows.Add(new RenderRow(row, "title", RenderRowKinds.Title, null, height,
                    [TextCell(from, row, to - from + 1, 1, value, style, LayoutDefaults.Format)]));
                if (to > from)
                    merges.Add(new CellRange(row, from, row, to));
            }

            // سرستون
            row++;
            var headerCells = new List<RenderCell>();
            for (var i = 0; i < visible.Count; i++)
            {
                var label = DocumentFields.ReplaceTokens(visible[i].Label, _data.Terms);
                headerCells.Add(TextCell(i + 1, row, 1, 1, DocValue.Of(label), Style(visible[i].EffectiveHeaderStyle, LayoutDefaults.HeaderStyle), LayoutDefaults.Format));
            }

            rows.Add(new RenderRow(row, "header", RenderRowKinds.Header, null, height, headerCells));
            var printTitleRows = Math.Min(_layout.Page.PrintTitleRows, row);
            var firstBodyRow = row + 1;

            var groups = new List<GroupResult>();
            var blocks = new List<PageBlock>();
            var allSpans = new List<RowSpan>();
            var allRows = new List<IRowValues>();

            if (sheet.Group is { } group)
            {
                var printed = 0;
                foreach (var bale in _data.Bales.OrderBy(b => b.Number))
                {
                    var lines = bale.Lines.OrderBy(l => l.Seq).ToList();
                    if (lines.Count == 0)
                    {
                        // E.7: نگلهٔ خالی کنار گذاشته می‌شود، وگرنه بازه‌های وارونه مثل SUM(J8:J7) درست می‌شد
                        _warnings.Add(new DocumentIssue("W_EMPTY_BALE",
                            Messages.DocumentEmptyBale.Format(_data.Terms, ("n", bale.Number)), bale.Number));
                        continue;
                    }

                    if (_options.MaxGroups is { } max && printed >= max)
                    {
                        _truncated = true;
                        break;
                    }

                    printed++;
                    var first = row + 1;
                    var rowValues = new List<IRowValues>();
                    for (var i = 0; i < lines.Count; i++)
                    {
                        row++;
                        var context = new RowContext(this, map, bale, lines[i], row);
                        rowValues.Add(context);
                        var mergeKey = group.MergeKey && lines.Count >= 2;
                        rows.Add(new RenderRow(row, $"bale:{bale.Number}:line:{lines[i].Seq}", RenderRowKinds.Data, bale.Number, height,
                            DataCells(map, context, row, group.KeyColumn, mergeKey ? (i == 0 ? lines.Count : 0) : 1)));
                    }

                    var span = new RowSpan(first, row);
                    if (group.MergeKey && lines.Count >= 2 && map.IndexOf(group.KeyColumn) is > 0 and var keyIndex)
                        merges.Add(new CellRange(first, keyIndex, row, keyIndex));

                    row++;
                    var anchors = new Dictionary<string, (string, DocValue)>(StringComparer.Ordinal);
                    var footerStyle = Style(group.FooterStyle, columnStyle ?? LayoutDefaults.CellStyle);
                    rows.Add(new RenderRow(row, $"bale:{bale.Number}:footer", RenderRowKinds.Footer, bale.Number, height,
                        FooterCells(map, group.Footer, footerStyle, row, [span], rowValues, bale, anchors, merges, "$.group.footer", labelText: null)));
                    groups.Add(new GroupResult(bale.Number, row, anchors));
                    blocks.Add(new PageBlock(first, row - first + 1, bale.Number));
                    allSpans.Add(span);
                    allRows.AddRange(rowValues);
                    Check(sheet, group, bale, anchors);
                }
            }
            else
            {
                var source = _data.Lines.Count > 0
                    ? _data.Lines.Select(l => (Bale: (DocumentBale?)null, Line: l)).ToList()
                    : _data.Bales.OrderBy(b => b.Number).SelectMany(b => b.Lines.OrderBy(l => l.Seq).Select(l => (Bale: (DocumentBale?)b, Line: l))).ToList();
                if (_options.MaxGroups is { } max && source.Count > max)
                {
                    source = source.Take(max).ToList();
                    _truncated = true;
                }

                var first = row + 1;
                for (var i = 0; i < source.Count; i++)
                {
                    row++;
                    var context = new RowContext(this, map, source[i].Bale, source[i].Line, row);
                    allRows.Add(context);
                    rows.Add(new RenderRow(row, $"line:{i + 1}", RenderRowKinds.Data, source[i].Bale?.Number, height,
                        DataCells(map, context, row, keyColumn: null, keyRowSpan: 1)));
                }

                if (source.Count > 0)
                    allSpans.Add(new RowSpan(first, row));
            }

            if (sheet.GrandTotal is { } grand)
            {
                row++;
                var style = Style(grand.Style, columnStyle ?? LayoutDefaults.CellStyle);
                var anchors = new Dictionary<string, (string, DocValue)>(StringComparer.Ordinal);
                rows.Add(new RenderRow(row, "grandTotal", RenderRowKinds.GrandTotal, null, height,
                    FooterCells(map, grand.Cells, style, row, allSpans, allRows, bale: null, anchors, merges, "$.grandTotal.cells", grand.Label)));
            }

            // شکستن صفحه با رعایت نگله
            PageBreakPlan? plan = null;
            var breaks = new List<int>();
            if (blocks.Count > 0)
            {
                var (paperW, paperH) = PaperInches(_layout.Page.Paper, _layout.Page.Orientation);
                plan = PageBreakPlanner.Plan(new PageBreakInput(
                    visible.Select(c => c.Width ?? DefaultColumnWidth).ToList(),
                    paperW, paperH, Margins(), height, printTitleRows,
                    firstBodyRow,
                    blocks,
                    _options.FallbackScale,
                    FitToWidth: _layout.Page.FitToWidth,
                    ScalePercent: _layout.Page.Scale));
                if (_layout.Page.KeepGroupTogether)
                {
                    breaks.AddRange(plan.RowBreaksAfter);
                    foreach (var number in plan.TooLongGroups)
                    {
                        _warnings.Add(new DocumentIssue("W_BALE_TOO_LONG",
                            Messages.DocumentBaleTooLong.Format(_data.Terms, ("n", number)), number));
                    }
                }
            }

            var columns = visible.Select((c, i) => new RenderColumn(i + 1, LayoutColumns.Letter(i + 1), c.Key,
                DocumentFields.ReplaceTokens(c.Label, _data.Terms), c.Width ?? DefaultColumnWidth, c.EffectiveFormat)).ToList();
            var renderSheet = new RenderSheet(sheet.Name, SheetRoles.Main, TabSelected: true, columns, columnStyle is null ? null : Style(columnStyle, columnStyle),
                rows, merges, printTitleRows, breaks, plan);
            return new MainResult(renderSheet, map, groups);
        }

        private List<RenderCell> DataCells(SheetColumnMap map, RowContext context, int row, string? keyColumn, int keyRowSpan)
        {
            var cells = new List<RenderCell>();
            for (var i = 0; i < map.Visible.Count; i++)
            {
                var column = map.Visible[i];
                var isKey = keyColumn is not null && column.Key == keyColumn;
                if (isKey && keyRowSpan == 0)
                    continue; // زیر ادغام شمارهٔ نگله

                var value = context.Value(column.Key!);
                string? formula = null;
                if (!string.IsNullOrWhiteSpace(column.Expr))
                    formula = ExcelCompiler.Compile(Parse(column.Expr, $"$.columns.{column.Key}.expr"), context).Formula;

                if (column.Field == RugFieldKeys.Serial && value.Kind == DocValueKind.Text)
                    value = SerialCell(value.Text!);

                // متن ردیف داده از دادهٔ فرش می‌آید (فیلد، یا عبارتی روی فیلدها): با = + - @ پیشوند گیومه می‌گیرد تا اکسل
                // هنگام ویرایش آن را فرمول نخواند (E.7)
                var quote = formula is null && value.Kind == DocValueKind.Text && value.Text!.Length > 0 && value.Text[0] is '=' or '+' or '-' or '@';

                cells.Add(new RenderCell(i + 1, FormulaResolver.Address(i + 1, row), 1, isKey ? keyRowSpan : 1, value, formula,
                    Style(column.EffectiveStyle, LayoutDefaults.CellStyle), column.EffectiveFormat, quote, Display(value, column.EffectiveFormat)));
            }

            return cells;
        }

        private List<RenderCell> FooterCells(
            SheetColumnMap map, IReadOnlyList<FooterCellLayout> layoutCells, string rowStyle, int row, IReadOnlyList<RowSpan> spans,
            IReadOnlyList<IRowValues> rowValues, DocumentBale? bale, Dictionary<string, (string, DocValue)> anchors,
            List<CellRange> merges, string path, string? labelText)
        {
            var visible = map.Visible;
            var taken = new bool[visible.Count + 1];
            var cells = new List<RenderCell>();
            var context = new FooterContext(this, map, bale, row, spans, rowValues);

            for (var k = 0; k < layoutCells.Count; k++)
            {
                var spec = layoutCells[k];
                var at = map.IndexOf(spec.Col);
                if (at == 0)
                    continue;
                var span = Math.Clamp(spec.Span, 1, visible.Count - at + 1);
                if (Enumerable.Range(at, span).Any(x => taken[x]))
                    continue;
                for (var x = at; x < at + span; x++)
                    taken[x] = true;

                var style = Style(spec.Style, rowStyle);
                DocValue value;
                string? formula = null;
                if (spec.Text is not null)
                {
                    // متن ثابت قالب: بی پیشوند گیومه، حتی اگر با «=» شروع شود (E.7)
                    value = DocValue.Of(DocumentFields.ReplaceTokens(spec.Text, _data.Terms));
                }
                else
                {
                    var expr = spec.Agg is not null ? AggregateCall(spec) : Parse(spec.Expr, $"{path}[{k}].expr");
                    value = Evaluator.Evaluate(expr, context);
                    formula = ExcelCompiler.Compile(expr, context).Formula;
                }

                var address = FormulaResolver.Address(at, row);
                cells.Add(new RenderCell(at, address, span, 1, value, formula, style, LayoutDefaults.Format, false, Display(value, LayoutDefaults.Format)));
                if (span > 1)
                    merges.Add(new CellRange(row, at, row, at + span - 1));
                if (!string.IsNullOrEmpty(spec.Id))
                    anchors.TryAdd(spec.Id, (address, value));
            }

            // برچسب ردیف جمع کل در اولین ستون آزاد
            if (labelText is not null && visible.Count > 0 && !taken[1])
            {
                taken[1] = true;
                var label = DocValue.Of(DocumentFields.ReplaceTokens(labelText, _data.Terms));
                cells.Add(new RenderCell(1, FormulaResolver.Address(1, row), 1, 1, label, null, rowStyle, LayoutDefaults.Format, false, label.AsText()));
            }

            // کل ردیف پانویس زرد و کادردار است، حتی خانه‌های خالی (E.9)
            for (var x = 1; x <= visible.Count; x++)
            {
                if (!taken[x])
                    cells.Add(new RenderCell(x, FormulaResolver.Address(x, row), 1, 1, DocValue.Blank, null, rowStyle, LayoutDefaults.Format, false, ""));
            }

            cells.Sort((a, b) => a.Col.CompareTo(b.Col));
            return cells;
        }

        private static Expr AggregateCall(FooterCellLayout spec) => spec.Agg == AggKinds.Count
            ? new CallExpr(ExpressionFunctions.Count, [], 0)
            : new CallExpr(ExpressionFunctions.Sum, [new ColumnRef(spec.Of ?? "", 0)], 0);

        private void Check(SheetLayout sheet, GroupLayout group, DocumentBale bale, Dictionary<string, (string Address, DocValue Value)> anchors)
        {
            foreach (var check in sheet.CheckAgainst)
            {
                if (!anchors.TryGetValue(check.FooterId, out var anchor) || anchor.Value.Kind != DocValueKind.Number)
                    continue;
                decimal? ledger = check.LedgerField switch
                {
                    DocumentFieldKeys.BalePieces => bale.Pieces,
                    DocumentFieldKeys.BaleArea => bale.AreaM2,
                    DocumentFieldKeys.BaleUsd => bale.Usd,
                    _ => DocumentFields.Value(check.LedgerField, _data, bale, null, Field(check.LedgerField)) is { Kind: DocValueKind.Number } v ? v.Number : null
                };
                if (ledger is not { } expected || expected == anchor.Value.Number)
                    continue;

                var spec = group.Footer.FirstOrDefault(f => f.Id == check.FooterId);
                var label = spec?.Agg == AggKinds.Count
                    ? "تعداد تخته"
                    : DocumentFields.ReplaceTokens(sheet.Columns.FirstOrDefault(c => c.Key == spec?.Of)?.Label ?? check.FooterId, _data.Terms);
                _warnings.Add(new DocumentIssue("W_CHECK_MISMATCH",
                    Messages.DocumentCheckMismatch.Format(_data.Terms, ("label", label), ("n", bale.Number), ("value", anchor.Value.Number), ("total", expected)),
                    bale.Number));
            }
        }

        // ── برگهٔ خلاصه ─────────────────────────────────────────────────

        private RenderSheet ComposeSummary(SheetLayout sheet, SheetLayout mainLayout, MainResult main)
        {
            var rows = new List<RenderRow>();
            var merges = new List<CellRange>();
            var height = _layout.Page.RowHeight;
            var start = Math.Max(1, LayoutColumns.Index(sheet.StartCol));
            var row = 0;
            var rowStyle = Style(sheet.RowStyle, LayoutDefaults.CellStyle);

            if (sheet.Title is { } title)
            {
                row++;
                var from = Math.Max(1, LayoutColumns.Index(title.FromCol));
                var to = Math.Max(from, LayoutColumns.Index(title.ToCol));
                var value = EvaluateScalar(title.Expr, "$.summary.title.expr", bale: null);
                rows.Add(new RenderRow(row, "summary:title", RenderRowKinds.Title, null, height,
                    [TextCell(from, row, to - from + 1, 1, value, Style(title.Style, LayoutDefaults.HeaderStyle), LayoutDefaults.Format)]));
                if (to > from)
                    merges.Add(new CellRange(row, from, row, to));
            }

            row++;
            var headerStyle = Style(sheet.HeaderStyle, LayoutDefaults.HeaderStyle);
            rows.Add(new RenderRow(row, "summary:header", RenderRowKinds.Header, null, height,
                sheet.Columns.Select((c, j) => TextCell(start + j, row, 1, 1, DocValue.Of(DocumentFields.ReplaceTokens(c.Label, _data.Terms)), headerStyle, LayoutDefaults.Format)).ToList()));

            var ids = sheet.Columns.Select(c => c.From is { } f && f.StartsWith(LayoutDefaults.FooterLinkPrefix, StringComparison.Ordinal)
                ? f[LayoutDefaults.FooterLinkPrefix.Length..]
                : null).ToList();
            var firstData = row + 1;
            var sums = new decimal[sheet.Columns.Count];
            foreach (var group in main.Groups)
            {
                row++;
                var cells = new List<RenderCell>();
                for (var j = 0; j < ids.Count; j++)
                {
                    var col = start + j;
                    if (ids[j] is not { } id || !group.Anchors.TryGetValue(id, out var anchor))
                    {
                        cells.Add(new RenderCell(col, FormulaResolver.Address(col, row), 1, 1, DocValue.Blank, null, rowStyle, LayoutDefaults.Format, false, ""));
                        continue;
                    }

                    sums[j] += anchor.Value.AsNumber();
                    cells.Add(new RenderCell(col, FormulaResolver.Address(col, row), 1, 1, anchor.Value,
                        FormulaResolver.SheetReference(mainLayout.Name, anchor.Address), rowStyle, LayoutDefaults.Format, false, Display(anchor.Value, LayoutDefaults.Format)));
                }

                rows.Add(new RenderRow(row, $"summary:{group.Number}", RenderRowKinds.Summary, group.Number, height, cells));
            }

            var lastData = row;
            if (sheet.EqualsRow)
            {
                row++;
                var style = NoBorder(rowStyle);
                rows.Add(new RenderRow(row, "summary:equals", RenderRowKinds.EqualsRow, null, height,
                    sheet.Columns.Select((_, j) => TextCell(start + j, row, 1, 1, DocValue.FromText("="), style, LayoutDefaults.Format)).ToList()));
            }

            if (sheet.Totals is { } totals)
            {
                row++;
                var cells = new List<RenderCell>();
                for (var j = 0; j < ids.Count; j++)
                {
                    var col = start + j;
                    var id = ids[j];
                    if (id is not null && totals.Sum.Contains(id))
                    {
                        var value = DocValue.FromNumber(sums[j]);
                        // بی ردیف داده بازهٔ وارونه (E3:E2) درست می‌شد؛ پس فقط مقدار
                        var formula = lastData >= firstData
                            ? FormulaResolver.ColumnSum(LayoutColumns.Letter(col), new RowSpan(firstData, lastData))
                            : null;
                        cells.Add(new RenderCell(col, FormulaResolver.Address(col, row), 1, 1, value, formula, rowStyle, LayoutDefaults.Format, false, Display(value, LayoutDefaults.Format)));
                    }
                    else if (id is not null && totals.BlankBordered.Contains(id))
                    {
                        cells.Add(new RenderCell(col, FormulaResolver.Address(col, row), 1, 1, DocValue.Blank, null, rowStyle, LayoutDefaults.Format, false, ""));
                    }
                }

                rows.Add(new RenderRow(row, "summary:totals", RenderRowKinds.Totals, null, height, cells));
            }

            // همان نما و پهنای ستون‌های برگهٔ اصلی (E.9)
            var mainColumns = main.Sheet.Columns;
            var lastCol = Math.Max(mainColumns.Count, Math.Max(start + sheet.Columns.Count - 1, LayoutColumns.Index(sheet.Title?.ToCol)));
            var columns = Enumerable.Range(1, lastCol).Select(i => new RenderColumn(i, LayoutColumns.Letter(i), null, "",
                i <= mainColumns.Count ? mainColumns[i - 1].Width : DefaultColumnWidth, LayoutDefaults.Format)).ToList();
            var columnStyle = sheet.ColumnStyle ?? mainLayout.ColumnStyle;
            return new RenderSheet(sheet.Name, SheetRoles.Summary, TabSelected: false, columns, columnStyle is null ? null : Style(columnStyle, columnStyle),
                rows, merges, PrintTitleRows: 0, RowBreaksAfter: [], Pages: null);
        }

        // ── کمکی‌ها ─────────────────────────────────────────────────────

        private DocValue EvaluateScalar(string expr, string path, DocumentBale? bale)
            => Evaluator.Evaluate(Parse(expr, path), new FooterContext(this, null, bale, 0, [], []));

        internal Expr Parse(string? text, string path)
        {
            var key = text ?? "";
            if (_parsed.TryGetValue(key, out var cached))
                return cached;
            var result = ExpressionParser.Parse(text);
            if (!result.Ok)
            {
                var issue = LayoutMessages.Expression(result.Error!, path);
                throw AppValidationException.ForField(path, issue.Message);
            }

            _parsed[key] = result.Expression!;
            return result.Expression!;
        }

        internal FieldDefDto? Field(string key) => _fields.GetValueOrDefault(key);

        internal DocValue FieldValue(string key, DocumentBale? bale, DocumentLine? line)
            => DocumentFields.Value(key, _data, bale, line, Field(key));

        private string Style(string? name, string fallback)
        {
            if (name is not null && _styles.ContainsKey(name))
                return name;
            if (_styles.ContainsKey(fallback))
                return fallback;
            // سبکی که قالب ندارد: سبک ساده تا نویسنده‌ها کلید ناشناخته نبینند
            _styles.TryAdd(fallback, new RenderStyle(_layout.Page.Font, _layout.Page.FontSize, null, null, BorderStyles.None, "center", "center", false));
            return fallback;
        }

        private string NoBorder(string style)
        {
            var name = style + NoBorderSuffix;
            if (!_styles.ContainsKey(name))
                _styles[name] = _styles[style] with { Border = BorderStyles.None };
            return name;
        }

        private static RenderCell TextCell(int col, int row, int colSpan, int rowSpan, DocValue value, string style, string format)
            => new(col, FormulaResolver.Address(col, row), colSpan, rowSpan, value, null, style, format, false, Display(value, format));

        private static string Display(DocValue value, string format)
            => value.Kind == DocValueKind.Number ? DocNumber.Format(value.Number, format) : value.AsText();

        /// <summary>
        /// E.7: شماره فقط وقتی خانهٔ عددی است که همه رقم باشد، با صفر شروع نشود و حداکثر ۱۵ رقم باشد («3778»)؛ بازه
        /// («3911 - 3960»)، «0123» و بقیه متن می‌مانند (اکسل صفر اول و رقم شانزدهم را می‌خورد).
        /// </summary>
        internal static DocValue SerialCell(string serial)
        {
            if (serial.Length is > 0 and <= 15 && serial[0] != '0' && serial.All(char.IsAsciiDigit))
                return DocValue.FromNumber(decimal.Parse(serial, System.Globalization.CultureInfo.InvariantCulture));
            return DocValue.FromText(serial);
        }

        private static int PaperSize(string paper) => paper switch
        {
            "A3" => 8,
            "A5" => 11,
            "Letter" => 1,
            _ => 9
        };

        private static (decimal Width, decimal Height) PaperInches(string paper, string orientation)
        {
            var (w, h) = paper switch
            {
                "A3" => (11.69m, 16.54m),
                "A5" => (5.83m, 8.27m),
                "Letter" => (8.5m, 11m),
                _ => (8.27m, 11.69m)
            };
            return orientation == "landscape" ? (h, w) : (w, h);
        }
    }

    /// <summary>
    /// بافت یک ردیف داده: مقدار هر ستون (فیلد یا عبارت، با حافظه و جلوگیری از دور) و نشانی‌ها برای مترجم.
    /// </summary>
    private sealed class RowContext(Run run, SheetColumnMap map, DocumentBale? bale, DocumentLine line, int row)
        : IEvaluationContext, ICompileContext, IRowValues
    {
        private readonly Dictionary<string, DocValue> _values = new(StringComparer.Ordinal);
        private readonly HashSet<string> _evaluating = new(StringComparer.Ordinal);

        public int Row => row;

        public DocValue Value(string columnKey)
        {
            if (_values.TryGetValue(columnKey, out var known))
                return known;
            var column = map.Column(columnKey);
            if (column is null || !_evaluating.Add(columnKey))
                return DocValue.Blank; // ستون ناشناخته یا دور (اعتبارسنج پیش‌تر رد می‌کند)

            var value = !string.IsNullOrWhiteSpace(column.Field)
                ? run.FieldValue(column.Field, bale, line)
                : Evaluator.Evaluate(run.Parse(column.Expr, $"$.columns.{columnKey}.expr"), this);
            _evaluating.Remove(columnKey);
            _values[columnKey] = value;
            return value;
        }

        public DocValue Field(string key) => run.FieldValue(key, bale, line);

        DocValue IEvaluationContext.Column(string key) => Value(key);

        DocValue IEvaluationContext.Aggregate(string function, IReadOnlyList<string> columnKeys) => DocValue.Blank;

        ColumnBinding ICompileContext.Column(string key)
            => map.LetterOf(key) is { } letter ? ColumnBinding.At(letter) : ColumnBinding.Hidden(Value(key));

        string? ICompileContext.Aggregate(string function, IReadOnlyList<string> columnKeys) => null;
    }

    /// <summary>بافت پانویس، جمع کل و عنوان: فیلدهای نگله/محموله و جمع‌ها روی بازه‌های داده.</summary>
    private sealed class FooterContext(Run run, SheetColumnMap? map, DocumentBale? bale, int row, IReadOnlyList<RowSpan> spans, IReadOnlyList<IRowValues> rows)
        : IEvaluationContext, ICompileContext
    {
        public int Row => row;

        public DocValue Field(string key) => run.FieldValue(key, bale, null);

        DocValue IEvaluationContext.Column(string key) => DocValue.Blank;

        DocValue IEvaluationContext.Aggregate(string function, IReadOnlyList<string> columnKeys)
            => map is null ? DocValue.Blank : FormulaResolver.AggregateValue(function, columnKeys, rows, map);

        ColumnBinding ICompileContext.Column(string key)
            => map?.LetterOf(key) is { } letter ? ColumnBinding.At(letter) : ColumnBinding.Hidden(DocValue.Blank);

        string? ICompileContext.Aggregate(string function, IReadOnlyList<string> columnKeys)
            => map is null ? null : FormulaResolver.Aggregate(function, columnKeys, spans, map);
    }
}
