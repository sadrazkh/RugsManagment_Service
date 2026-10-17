using RugsManagment.Application.DTOs.Grid;
using RugsManagment.Application.Features.Rugs;
using RugsManagment.Domain.Entities;

namespace RugsManagment.Application.Features.Intake.Grid;

/// <summary>
/// جدول «ورود فرش» (<c>intake</c>، G.2، H.1): همهٔ فرش‌های ورودی‌های یک روز ایران (همهٔ فروشنده‌ها)، به ترتیب ثبت.
///
/// ستون‌ها به ترتیب G.2: شماره · فروشنده · تعداد · رنگ متن · رنگ حاشیه · نقشه · محل بافت · مواد مصرفی · (فیلدهای سفارشیِ
/// دیگر با «ستون در جدول») · طول · عرض · متراژ · (خانه‌های خرید و «جمع خرید»، فقط Business) · یادداشت. طول و عرض لازم‌اند
/// (فرش تازه بی اندازه ذخیره نمی‌شود)، پس پنهان نمی‌شوند. پانویس برای هر ورودی (فروشنده) جدا جمع می‌خورد (E7).
/// </summary>
public sealed class IntakeGridSource(
    IIntakeGridRepository repository,
    RugGridReader reader,
    ISerialSuggester serials) : IGridSource
{
    public string GridId => TenantGridLayout.IntakeGridId;

    public string Title => "جدول ورود فرش";

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
        columns.Add(RugGridColumns.IntakeLength(required: true));
        columns.Add(RugGridColumns.IntakeWidth(required: true));
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

        var day = request.Date ?? IranDay.Today();
        var rugs = await repository.ListIntakeDayAsync(viewer.TenantId, day, request.BatchId, cancellationToken);

        var builder = await reader.BuilderAsync(viewer, allColumns, cancellationToken);
        var rows = await reader.RowsAsync(builder, rugs, cancellationToken);

        return new GridDataDto(
            columns,
            rows,
            builder.Totals(rugs, groupByBatch: true),
            await serials.SuggestAsync(viewer.TenantId, cancellationToken));
    }
}
