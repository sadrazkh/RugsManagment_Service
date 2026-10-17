using System.Text.Json;
using System.Text.Json.Serialization;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Grid;
using RugsManagment.Application.Features.Intake.Grid;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Intake;

/// <summary>ستون‌های مؤثر یک جدول برای یک بیننده.</summary>
/// <param name="Columns">ستون‌های دیده‌شده به ترتیب کارگاه.</param>
/// <param name="AllColumns">همهٔ ستون‌های پیش‌فرض (پنهان‌ها هم) — مقدار خانه‌هایشان در ردیف می‌آید.</param>
/// <param name="Version">xmin چیدمان کارگاه؛ ۰ یعنی پیش‌فرض برنامه.</param>
public sealed record EffectiveGridLayout(
    IReadOnlyList<GridColumnDto> Columns,
    IReadOnlyList<GridColumnDto> AllColumns,
    uint Version);

/// <summary>چیدمان ستون‌های جدول‌ها: پیش‌فرض‌های هر منبع (G.2) به‌اضافهٔ تنظیم کارگاه (<see cref="TenantGridLayout"/>).</summary>
public interface IGridLayoutService
{
    /// <summary>همهٔ جدول‌های شناخته‌شده (صفحهٔ تنظیم ستون‌ها).</summary>
    IReadOnlyList<IGridSource> Sources { get; }

    /// <summary>منبع یک جدول؛ ناشناخته → <see cref="KeyNotFoundException"/> (۴۰۴).</summary>
    IGridSource Source(string? gridId);

    /// <summary>ستون‌های مؤثر برای یک بیننده (ستون پولی فقط برای Business).</summary>
    Task<EffectiveGridLayout> EffectiveAsync(GridViewer viewer, IGridSource source, CancellationToken cancellationToken = default);

    /// <summary><c>GET /api/settings/grid-layout/{gridId}</c>.</summary>
    Task<GridLayoutDto> GetAsync(GridViewer viewer, string gridId, CancellationToken cancellationToken = default);

    /// <summary><c>PUT</c>: ترتیب، برچسب، پهنا، چسبان و ایستگاه Tab؛ ستونی که نیامده پنهان است. نسخهٔ کهنه → ۴۰۹.</summary>
    Task<GridLayoutDto> SaveAsync(GridViewer viewer, string gridId, GridLayoutDto request, CancellationToken cancellationToken = default);

    /// <summary><c>DELETE</c>: برگشت به پیش‌فرض برنامه. نسخهٔ کهنه → ۴۰۹.</summary>
    Task<GridLayoutDto> ResetAsync(GridViewer viewer, string gridId, uint version, CancellationToken cancellationToken = default);
}

/// <summary>
/// چیدمان ستون‌ها (G.2، I.2 «Grid layout»).
///
/// <para><b>چه ذخیره می‌شود.</b> فقط آنچه تنظیم‌کننده عوض کرده: ترتیب کلیدها، برچسب، پهنا، چسبان، ایستگاه Tab و — با
/// فهرست «کلیدهایی که آن روز بودند» — پنهان‌ها. نوع، فقط‌خواندنی، لازم، گزینه‌ها و پولی بودن هر ستون همیشه از پیش‌فرض
/// برنامه می‌آید؛ پس چیدمان ذخیره‌شده نمی‌تواند یک ستون پولی را به انباردار نشان دهد یا خانهٔ فقط‌خواندنی را نوشتنی کند.</para>
///
/// <para><b>ستون تازه.</b> ستونی که بعد از ذخیرهٔ چیدمان پیدا شده (فیلد سفارشی تازه، نوع خرید تازه، ستون نسخهٔ بعد) در
/// «کلیدهای آن روز» نیست، پس پنهان حساب نمی‌شود و کنار ستون پیش‌فرضِ قبلی‌اش می‌نشیند. ستون «لازم» (طول و عرض جدول ورود)
/// هرگز پنهان نمی‌ماند.</para>
///
/// <para><b>هم‌زمانی.</b> xmin ردیف؛ ذخیرهٔ کهنه ۴۰۹. نبودِ ردیف یعنی «پیش‌فرض برنامه» (نسخهٔ ۰).</para>
/// </summary>
public sealed class GridLayoutService(
    IEnumerable<IGridSource> sources,
    IIntakeGridRepository repository,
    IUnitOfWork unitOfWork,
    IAuditLog audit) : IGridLayoutService
{
    /// <summary>بیشترین طول برچسب ستون.</summary>
    public const int MaxLabelLength = 60;

    /// <summary>بازهٔ پهنای ستون (ch).</summary>
    public const decimal MinWidth = 4m;
    public const decimal MaxWidth = 60m;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All)
    };

    private readonly IReadOnlyList<IGridSource> _sources = sources
        .GroupBy(s => s.GridId, StringComparer.Ordinal)
        .Select(g => g.First())
        .OrderBy(s => s.GridId == TenantGridLayout.IntakeGridId ? 0 : s.GridId == TenantGridLayout.RugsGridId ? 1 : 2)
        .ThenBy(s => s.GridId, StringComparer.Ordinal)
        .ToList();

    public IReadOnlyList<IGridSource> Sources => _sources;

    public IGridSource Source(string? gridId)
        => _sources.FirstOrDefault(s => string.Equals(s.GridId, gridId?.Trim(), StringComparison.Ordinal))
           ?? throw new KeyNotFoundException(Messages.GridUnknown.Text);

    public async Task<EffectiveGridLayout> EffectiveAsync(
        GridViewer viewer, IGridSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        ArgumentNullException.ThrowIfNull(source);

        var defaults = await source.DefaultColumnsAsync(viewer, cancellationToken);
        var stored = await repository.GetLayoutAsync(viewer.TenantId, source.GridId, cancellationToken);
        if (stored is null)
            return new EffectiveGridLayout(defaults, defaults, 0);

        return new EffectiveGridLayout(Apply(defaults, Parse(stored.Layout.ColumnsJson)), defaults, stored.Version);
    }

    public async Task<GridLayoutDto> GetAsync(GridViewer viewer, string gridId, CancellationToken cancellationToken = default)
    {
        var source = Source(gridId);
        var layout = await EffectiveAsync(viewer, source, cancellationToken);
        return new GridLayoutDto(source.GridId, layout.Columns, layout.Version, layout.AllColumns);
    }

    public async Task<GridLayoutDto> SaveAsync(
        GridViewer viewer, string gridId, GridLayoutDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        ArgumentNullException.ThrowIfNull(request);
        var source = Source(gridId);
        var defaults = await source.DefaultColumnsAsync(viewer, cancellationToken);
        var stored = Validate(defaults, request.Columns ?? []);

        var json = JsonSerializer.Serialize(stored, JsonOptions);
        var row = await repository.GetLayoutForUpdateAsync(viewer.TenantId, source.GridId, request.Version, cancellationToken);
        if (row is null)
        {
            // نسخهٔ غیر صفر یعنی کاربر چیدمانی دیده بود که حالا (با «برگشت به پیش‌فرض») دیگر نیست
            if (request.Version != 0)
                throw new ConcurrencyConflictException(Messages.GridLayoutStale.Text);

            row = new TenantGridLayout { TenantId = viewer.TenantId, GridId = source.GridId, ColumnsJson = json };
            await repository.AddLayoutAsync(row, cancellationToken);
        }
        else
        {
            row.ColumnsJson = json;
        }

        audit.Record(AuditAction.GridLayoutChanged, nameof(TenantGridLayout), null,
            Messages.GridLayoutChanged.Format(("grid", source.Title)), source.Title,
            new { gridId = source.GridId, columns = stored.Columns.Select(c => c.Key).ToList() });

        await SaveAsync(cancellationToken);
        return await GetAsync(viewer, source.GridId, cancellationToken);
    }

    public async Task<GridLayoutDto> ResetAsync(
        GridViewer viewer, string gridId, uint version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        var source = Source(gridId);
        var row = await repository.GetLayoutForUpdateAsync(viewer.TenantId, source.GridId, version, cancellationToken);
        if (row is not null)
        {
            repository.RemoveLayout(row);
            audit.Record(AuditAction.GridLayoutChanged, nameof(TenantGridLayout), null,
                Messages.GridLayoutReset.Format(("grid", source.Title)), source.Title, new { gridId = source.GridId, reset = true });
            await SaveAsync(cancellationToken);
        }

        return await GetAsync(viewer, source.GridId, cancellationToken);
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            throw new ConcurrencyConflictException(Messages.GridLayoutStale.Text);
        }
        catch (DuplicateKeyException)
        {
            // دو «اولین ذخیره» هم‌زمان: دومی ردیفی را می‌سازد که اولی ساخته بود
            throw new ConcurrencyConflictException(Messages.GridLayoutStale.Text);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // قاعده‌های خالص
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>آنچه در <see cref="TenantGridLayout.ColumnsJson"/> می‌نشیند.</summary>
    public sealed record StoredLayout(IReadOnlyList<StoredColumn> Columns, IReadOnlyList<string>? Known);

    /// <summary>یک ستون ذخیره‌شده؛ null یعنی «همان پیش‌فرض».</summary>
    public sealed record StoredColumn(string Key, string? Label, decimal? Width, bool? Sticky, bool? TabStop);

    /// <summary>
    /// سنجش بدنهٔ PUT در برابر ستون‌های پیش‌فرض: کلید ناشناخته، تکراری، برچسب بلند، پهنای بیرون از بازه، ستونِ لازمِ جاافتاده
    /// و جدول بی ستون → ۴۰۰ (همهٔ خطاها با هم، زیر کلید <c>columns</c>).
    /// </summary>
    public static StoredLayout Validate(IReadOnlyList<GridColumnDto> defaults, IReadOnlyList<GridColumnDto> requested)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        ArgumentNullException.ThrowIfNull(requested);
        var byKey = defaults.ToDictionary(c => c.Key, StringComparer.Ordinal);
        var errors = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var columns = new List<StoredColumn>();

        if (requested.Count == 0)
            errors.Add(Messages.GridLayoutEmpty.Text);

        foreach (var column in requested)
        {
            var key = column?.Key?.Trim() ?? string.Empty;
            if (!byKey.TryGetValue(key, out var fallback))
            {
                errors.Add(Messages.GridLayoutUnknownColumn.Format(("key", key)));
                continue;
            }

            if (!seen.Add(key))
            {
                errors.Add(Messages.GridLayoutDuplicate.Format(("label", fallback.Label)));
                continue;
            }

            var label = string.IsNullOrWhiteSpace(column!.Label) ? null : PersianText.Normalize(column.Label);
            if (label is { Length: > MaxLabelLength })
                errors.Add(Messages.GridLayoutLabel.Format(("n", MaxLabelLength)));
            if (column.Width is < MinWidth or > MaxWidth)
                errors.Add(Messages.GridLayoutWidth.Format(("min", MinWidth), ("max", MaxWidth)));

            columns.Add(new StoredColumn(
                key,
                label == fallback.Label ? null : label,
                column.Width == fallback.Width ? null : decimal.Round(column.Width, 1),
                column.Sticky == fallback.Sticky ? null : column.Sticky,
                fallback.ReadOnly || column.TabStop == fallback.TabStop ? null : column.TabStop));
        }

        foreach (var required in defaults.Where(c => c.Required && !seen.Contains(c.Key)))
            errors.Add(Messages.GridLayoutRequired.Format(("label", required.Label)));

        if (errors.Count > 0)
        {
            var distinct = errors.Distinct(StringComparer.Ordinal).ToArray();
            throw new AppValidationException(distinct[0], new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["columns"] = distinct
            });
        }

        return new StoredLayout(columns, defaults.Select(c => c.Key).ToList());
    }

    /// <summary>
    /// ستون‌های مؤثر: ترتیب و تنظیم‌های ذخیره‌شده روی پیش‌فرض‌ها؛ پیش‌فرضی که در «کلیدهای آن روز» نبود (تازه است)، و هر
    /// ستون «لازم»، کنار ستون پیش‌فرضِ قبلی‌اش می‌نشیند. ستون ذخیره‌شده‌ای که حالا پیش‌فرض این بیننده نیست (ستون پولی برای
    /// انباردار، فیلد حذف‌شده) کنار می‌رود.
    /// </summary>
    public static IReadOnlyList<GridColumnDto> Apply(IReadOnlyList<GridColumnDto> defaults, StoredLayout? stored)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        if (stored is null || stored.Columns.Count == 0)
            return defaults;

        var byKey = defaults.ToDictionary(c => c.Key, StringComparer.Ordinal);
        var known = stored.Known is null ? null : new HashSet<string>(stored.Known, StringComparer.Ordinal);
        var result = new List<GridColumnDto>();
        var placed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var column in stored.Columns)
        {
            if (!byKey.TryGetValue(column.Key, out var fallback) || !placed.Add(column.Key))
                continue;
            result.Add(Merge(fallback, column));
        }

        for (var i = 0; i < defaults.Count; i++)
        {
            var column = defaults[i];
            if (placed.Contains(column.Key)) continue;
            var isNew = known is null || !known.Contains(column.Key);
            if (!isNew && !column.Required) continue; // پنهان

            // بعد از نزدیک‌ترین ستون پیش‌فرضِ قبلی که سر جایش هست؛ وگرنه اول
            var at = 0;
            for (var j = i - 1; j >= 0; j--)
            {
                var index = result.FindIndex(c => c.Key == defaults[j].Key);
                if (index >= 0)
                {
                    at = index + 1;
                    break;
                }
            }

            result.Insert(at, column);
            placed.Add(column.Key);
        }

        return result;
    }

    private static GridColumnDto Merge(GridColumnDto fallback, StoredColumn stored) => fallback with
    {
        Label = string.IsNullOrWhiteSpace(stored.Label) ? fallback.Label : stored.Label,
        Width = stored.Width is { } width && width >= MinWidth && width <= MaxWidth ? width : fallback.Width,
        Sticky = stored.Sticky ?? fallback.Sticky,
        TabStop = !fallback.ReadOnly && (stored.TabStop ?? fallback.TabStop)
    };

    /// <summary>
    /// خواندن <see cref="TenantGridLayout.ColumnsJson"/>: شیء <c>{columns, known}</c>، یا آرایهٔ سادهٔ ستون‌ها (بی «کلیدهای آن
    /// روز»؛ پس هیچ پیش‌فرضی پنهان حساب نمی‌شود). JSON خراب = پیش‌فرض برنامه (جدول نباید به‌خاطر یک ردیف بد باز نشود).
    /// </summary>
    public static StoredLayout? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind switch
            {
                JsonValueKind.Object => JsonSerializer.Deserialize<StoredLayout>(json, JsonOptions) is { } layout
                    ? layout with { Columns = layout.Columns ?? [] }
                    : null,
                JsonValueKind.Array => new StoredLayout(
                    JsonSerializer.Deserialize<List<StoredColumn>>(json, JsonOptions) ?? [], null),
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
