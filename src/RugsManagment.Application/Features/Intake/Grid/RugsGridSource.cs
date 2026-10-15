using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Grid;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.Features.Rugs;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Features.Intake.Grid;

/// <summary>
/// جدول «فرش‌ها» (<c>rugs</c>، صفحهٔ انبار در نمای جدول): همان فیلترها و مرتب‌سازی فهرست فرش‌ها
/// (<see cref="RugQuery"/>)، صفحه‌به‌صفحه با <c>skip</c>/<c>take</c>.
///
/// فیلتر و ترتیب را خودِ <c>IRugRepository.SearchAsync</c> می‌دهد (همان SQL صفحهٔ فهرست، با فیلترهای محموله‌ای که فاز
/// محموله کامل می‌کند)؛ این‌جا فقط شناسه‌های همان صفحه را به ردیف جدول تبدیل می‌کنیم. پس جدول و فهرست هرگز دو جواب
/// متفاوت برای یک فیلتر نمی‌دهند. برای غیر Business مرتب‌سازی «هزینه» به «تاریخ ثبت» برمی‌گردد (A.5).
///
/// ستون‌ها: مثل جدول ورود به‌اضافهٔ اندازهٔ نهایی؛ طول و عرض این‌جا لازم نیستند چون این جدول فرش تازه نمی‌سازد.
/// </summary>
public sealed class RugsGridSource(
    IRugRepository rugs,
    IIntakeGridRepository repository,
    RugGridReader reader,
    ISerialSuggester serials,
    IMoneyVisibility money) : IGridSource
{
    /// <summary>اندازهٔ پیش‌فرض و بیشینهٔ یک برش (همان سقف صفحهٔ فهرست).</summary>
    public const int DefaultTake = RugQuery.MaxPageSize;

    public string GridId => TenantGridLayout.RugsGridId;

    public string Title => "جدول فرش‌ها";

    public bool EditsRugs => true;

    public async Task<IReadOnlyList<GridColumnDto>> DefaultColumnsAsync(GridViewer viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        var catalog = await reader.CatalogAsync(viewer.TenantId, cancellationToken);

        var columns = new List<GridColumnDto>
        {
            RugGridColumns.Serial(),
            RugGridColumns.Seller(),
            RugGridColumns.Quantity()
        };
        columns.AddRange(RugGridColumns.ColorColumns(catalog));
        columns.Add(RugGridColumns.Pattern());
        columns.Add(RugGridColumns.Origin());
        columns.Add(RugGridColumns.Material());
        columns.AddRange(RugGridColumns.OtherCustomColumns(catalog));
        columns.Add(RugGridColumns.IntakeLength(required: false));
        columns.Add(RugGridColumns.IntakeWidth(required: false));
        columns.Add(RugGridColumns.FinalLength());
        columns.Add(RugGridColumns.FinalWidth());
        columns.Add(RugGridColumns.Area());
        if (viewer.CanSeeMoney)
        {
            columns.AddRange(RugGridColumns.PurchaseColumns(catalog, canSeeMoney: true));
            columns.Add(RugGridColumns.PurchaseTotal());
        }
        columns.Add(RugGridColumns.Notes());
        return columns;
    }

    public async Task<GridDataDto> LoadAsync(
        GridViewer viewer, GridLoadRequest request, IReadOnlyList<GridColumnDto> columns,
        IReadOnlyList<GridColumnDto> allColumns, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        ArgumentNullException.ThrowIfNull(request);

        var take = Math.Clamp(request.Take ?? DefaultTake, 5, RugQuery.MaxPageSize);
        var skip = Math.Max(request.Skip ?? 0, 0);
        var query = money.GuardSort((request.Query ?? new RugQuery()) with { Page = skip / take + 1, PageSize = take });

        var page = await rugs.SearchAsync(viewer.TenantId, query, cancellationToken);
        var snapshots = await repository.ListByIdsAsync(viewer.TenantId, page.Items.Select(i => i.Id).ToList(), cancellationToken);

        var builder = await reader.BuilderAsync(viewer, allColumns, cancellationToken);
        var rows = await reader.RowsAsync(builder, snapshots, cancellationToken);

        return new GridDataDto(
            columns,
            rows,
            builder.Totals(snapshots, groupByBatch: false, totalRows: page.TotalCount),
            await serials.SuggestAsync(viewer.TenantId, cancellationToken));
    }
}
