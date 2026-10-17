using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.DTOs.Grid;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Features.Intake.Grid;

/// <summary>
/// خواندن ردیف‌های فرشی جدول‌ها (ورود و انبار) و پاسخ ذخیره: کاتالوگ کارگاه یک بار در هر درخواست، سازندهٔ ردیف، و
/// «حذف این فرش» برای هر ردیف با یک پرس‌وجو برای همه. scoped، چون کاتالوگ را برای همان درخواست نگه می‌دارد.
/// </summary>
public sealed class RugGridReader(
    IIntakeGridRepository repository,
    IRepository<CustomFieldDefinition> customFields,
    IRepository<CostCategory> categories)
{
    private readonly Dictionary<Guid, RugGridCatalog> _catalogs = [];

    /// <summary>فیلدهای سفارشی و نوع‌های خرید کارگاه (یک بار در هر درخواست).</summary>
    public async Task<RugGridCatalog> CatalogAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (_catalogs.TryGetValue(tenantId, out var cached))
            return cached;

        var catalog = await RugGridCatalog.LoadAsync(tenantId, customFields, categories, cancellationToken);
        _catalogs[tenantId] = catalog;
        return catalog;
    }

    /// <summary>
    /// سازندهٔ ردیف برای همهٔ خانه‌های نوشتنیِ <paramref name="allColumns"/> (حتی ستون‌های پنهان، تا ادغام سه‌طرفه و
    /// «نوشتهٔ او را ببینم» همهٔ خانه‌ها را داشته باشند).
    /// </summary>
    public async Task<RugGridRowBuilder> BuilderAsync(
        GridViewer viewer, IReadOnlyList<GridColumnDto> allColumns, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        var catalog = await CatalogAsync(viewer.TenantId, cancellationToken);
        var keys = allColumns.Where(c => !c.ReadOnly).Select(c => c.Key).Distinct(StringComparer.Ordinal).ToList();
        return new RugGridRowBuilder(viewer, catalog, keys);
    }

    /// <summary>ردیف‌ها، با «حذف این فرش» برای همین بیننده (قاعدهٔ <see cref="RugDeletePolicy"/>).</summary>
    public async Task<IReadOnlyList<GridRowDto>> RowsAsync(
        RugGridRowBuilder builder, IReadOnlyList<GridRugSnapshot> rugs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (rugs.Count == 0) return [];

        var canDelete = await CanDeleteAsync(builder.Viewer, rugs.Select(r => r.Id).ToList(), cancellationToken);
        return rugs.Select(r => builder.Row(r, canDelete.Contains(r.Id))).ToList();
    }

    /// <summary>شناسهٔ فرش‌هایی که این بیننده می‌تواند حذف کند.</summary>
    public async Task<IReadOnlySet<Guid>> CanDeleteAsync(
        GridViewer viewer, IReadOnlyCollection<Guid> rugIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        var facts = await repository.DeleteFactsAsync(viewer.TenantId, viewer.UserId, rugIds, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        return facts.Values
            .Where(f => RugDeletePolicy.MayDelete(f, viewer.CanSeeMoney, now))
            .Select(f => f.RugId)
            .ToHashSet();
    }
}
