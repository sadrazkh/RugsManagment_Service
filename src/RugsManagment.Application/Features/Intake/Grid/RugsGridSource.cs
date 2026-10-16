using System.Globalization;
using System.Text.Json;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Grid;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.Features.Rugs;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

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
///
/// «جا» (فاز ۴b، C.10، H.1): ستون فقط‌خواندنی که می‌گوید فرش کجاست — «در انبار»، «در نگلهٔ ۳ (محمولهٔ ۷)»، «صادر شده
/// (محمولهٔ ۷)» یا «فروخته‌شده» — از همان جای حساب‌شدهٔ SQL فهرست (<see cref="RugListItemDto.Location"/>)، با واژهٔ نگله/عدل
/// کارگاه. پس جدول و فهرست هرگز دو جای متفاوت نشان نمی‌دهند.
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

    /// <summary>کلید ستون «جا» (فقط‌خواندنی؛ مقدارش در Derived).</summary>
    public const string LocationKey = "rug.location";

    /// <summary>جای فرش به عدد (<see cref="RugLocation"/>) در Derived، برای نماد چیپ در جزیره.</summary>
    public const string LocationCodeKey = "rug.locationCode";

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
        columns.Add(LocationColumn());
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
        var rows = WithLocation(await reader.RowsAsync(builder, snapshots, cancellationToken), page.Items, viewer.Terms);

        return new GridDataDto(
            columns,
            rows,
            builder.Totals(snapshots, groupByBatch: false, totalRows: page.TotalCount),
            await serials.SuggestAsync(viewer.TenantId, cancellationToken));
    }

    /// <summary>ستون «جا» (فقط‌خواندنی، هرگز ایستگاه Tab).</summary>
    public static GridColumnDto LocationColumn() =>
        new(LocationKey, "جا", GridColumnKind.ReadOnly, 18, false, true, false, null, false, TabStop: false);

    /// <summary>«جا»ی هر ردیف از ردیف فهرست همان فرش (همان صفحهٔ SearchAsync) در Derived.</summary>
    public static IReadOnlyList<GridRowDto> WithLocation(
        IReadOnlyList<GridRowDto> rows, IReadOnlyList<RugListItemDto> items, BaleTermWords terms)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(items);
        var byId = items.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());
        return rows.Select(row =>
        {
            if (!byId.TryGetValue(row.RugId, out var item)) return row;
            var label = LocationLabel(item.Location, item.BaleNumber, item.ShipmentNumber, terms);
            var derived = new Dictionary<string, JsonElement?>(row.Derived, StringComparer.Ordinal)
            {
                [LocationKey] = JsonSerializer.SerializeToElement(label),
                [LocationCodeKey] = JsonSerializer.SerializeToElement((int)item.Location)
            };
            return row with { Derived = derived };
        }).ToList();
    }

    /// <summary>
    /// برچسب «جا» (C.10، واژه‌نامهٔ A.2): «در انبار»، «در نگلهٔ ۳ (محمولهٔ ۷)»، «صادر شده (محمولهٔ ۷)»، «فروخته‌شده».
    /// </summary>
    public static string LocationLabel(RugLocation location, int? baleNumber, int? shipmentNumber, BaleTermWords terms)
    {
        ArgumentNullException.ThrowIfNull(terms);
        string Shipment() => shipmentNumber is { } n ? $" (محمولهٔ {Digits(n)})" : string.Empty;
        return location switch
        {
            RugLocation.InStock => "در انبار",
            RugLocation.InBale => (baleNumber is { } bale ? $"در {terms.BaleOf} {Digits(bale)}" : $"در {terms.Bale}") + Shipment(),
            RugLocation.Exported => "صادر شده" + Shipment(),
            RugLocation.Sold => "فروخته‌شده",
            RugLocation.Trashed => "در سطل زباله",
            _ => "—"
        };
    }

    private static string Digits(int value) => PersianText.ToPersianDigits(value.ToString(CultureInfo.InvariantCulture));
}
