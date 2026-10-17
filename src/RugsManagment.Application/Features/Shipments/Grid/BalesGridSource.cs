using System.Globalization;
using System.Text.Json;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Grid;
using RugsManagment.Application.Features.Intake;
using RugsManagment.Application.Features.Intake.Grid;
using RugsManagment.Application.Features.Platform;
using RugsManagment.Application.Fields;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Shipments.Grid;

/// <summary>
/// کلیدهای جدول نگله‌ها که در ردیف فرش نیستند — قرارداد این منبع با جزیرهٔ <c>baling-board</c>. کلیدهای کاتالوگ (خط، نگله)
/// همان رشته‌های <see cref="ShipmentFieldContributor"/>‌اند تا قالب سند و جدول یک نام داشته باشند.
/// </summary>
public static class BalesGridKeys
{
    /// <summary>«نگله» (نوشتنی تا محموله باز است؛ عوض کردنش یعنی عمل «انتقال»، نه نوشتن فرش).</summary>
    public const string BaleNumber = ShipmentFieldContributor.BaleNumber;

    /// <summary>شناسهٔ نگلهٔ ردیف (Derived).</summary>
    public const string BaleId = "bale.id";

    /// <summary>Seq خط در نگله (Derived؛ «برگردان» پس از «خارج کردن از نگله» همان جا را می‌خواهد).</summary>
    public const string LineSeq = "line.seq";

    /// <summary>«اندازهٔ اول» به شکل نمایشی «۲٫۰۸ × ۲٫۱۰» (فقط‌خواندنی).</summary>
    public const string IntakeSize = "rug.intakeSize";

    /// <summary>اندازهٔ اولِ عددی (Derived)؛ placeholder خانهٔ «طول» نهایی (C.4).</summary>
    public const string IntakeLength = RugFieldKeys.IntakeLength;

    /// <inheritdoc cref="IntakeLength"/>
    public const string IntakeWidth = RugFieldKeys.IntakeWidth;

    /// <summary>تغییر بزرگ‌تر طول یا عرض به سانت، با علامت («↓۵ سانت»)؛ null بی اندازهٔ نهایی.</summary>
    public const string SizeChangeCm = "rug.sizeChangeCm";

    /// <summary>اندازهٔ نهایی با اندازهٔ اول فرق دارد (در نمای ساده «اندازهٔ اول» فقط آن‌وقت دیده می‌شود، A.3).</summary>
    public const string SizeDiffers = "rug.sizeDiffers";

    /// <summary>«مساحت» خط (یک تخته × تعداد) به عدد.</summary>
    public const string LineArea = ShipmentFieldContributor.LineArea;

    /// <summary>متن «مساحت»: تک «۴٫۳۷»، دسته «۰٫۱۶ × ۵۰ = ۸٫۰۰» (قاعدهٔ هر تخته، G.4).</summary>
    public const string LineAreaText = "line.areaText";

    /// <summary>مساحت یک تخته در اندازهٔ مؤثر (Derived).</summary>
    public const string PieceArea = RugFieldKeys.AreaM2;

    /// <summary>«ارزش متر مربع» (دلار؛ پول داخلی نیست، برای همه، A.5).</summary>
    public const string UsdPerM2 = ShipmentFieldContributor.LineUsdPerM2;

    /// <summary>منبع نرخ به عدد (<see cref="DeclaredRateSource"/>).</summary>
    public const string RateSource = ShipmentFieldContributor.LineRateSource;

    /// <summary>برچسب چیپ منبع نرخ: «دستی» / «از جدول» / «نرخ پیش‌فرض» / «ندارد».</summary>
    public const string RateSourceLabel = "line.rateSourceLabel";

    /// <summary>نرخ دستیِ خط (بازرگان)؛ null یعنی از جدول یا پیش‌فرض.</summary>
    public const string UsdPerM2Override = "line.usdPerM2Override";

    /// <summary>مقدار فیلد کلید ارزش گمرکی همین فرش (برای فرم «ارزش گمرکی «کاشان» برای هر متر»، C.5).</summary>
    public const string DeclaredKey = "line.declaredKey";

    /// <summary>دلار یک تخته.</summary>
    public const string PieceUsd = ShipmentFieldContributor.LinePieceUsd;

    /// <summary>«جمع کل دلار» خط (دلار یک تخته × تعداد).</summary>
    public const string LineUsd = "line.usd";
}

/// <summary>
/// جدول «نگله‌ها» (<c>bales</c>، spec G.2، H.1): یک ردیف برای هر خطِ محموله (فرش یا دسته)، به ترتیب شمارهٔ نگله و Seq —
/// همان ترتیب اکسل اظهار، تا جزیره زیر هر نگله پانویس «نگلهٔ ۱ · شامل ۴ تخته · ۱۳٫۵۱ متر · ۶۰۷٫۹۵ دلار» بگذارد.
///
/// <para><b>ستون‌ها (G.2):</b> نگله · شماره · تعداد · رنگ متن · رنگ حاشیه · نقشه · محل بافت · مواد مصرفی · اندازهٔ اول · طول ·
/// عرض · مساحت · ارزش متر مربع · جمع کل دلار. برچسب «نگله» از واژهٔ کارگاه است (<see cref="GridViewer.Terms"/>؛ «عدل» برای
/// کارگاهی که عدل می‌گوید). در نمای ساده خانه‌های توصیفی ایستگاه Tab نیستند (با لمس، دوبار کلیک یا F2 ویرایش می‌شوند)، پس
/// Tab از «شماره» مستقیم به «طول» نهایی می‌رود؛ خانهٔ فقط‌خواندنی هم هرگز ایستگاه Tab نیست.</para>
///
/// <para><b>نوشتن:</b> این منبع خودش چیزی نمی‌نویسد. خانه‌های توصیفی ردیف فرش‌اند و از <c>POST /api/grid/rugs/batch</c> با
/// <c>gridId = "bales"</c> می‌روند (<see cref="EditsRugs"/>؛ ادغام سه‌طرفه همان سازندهٔ ردیف جدول‌های دیگر را دارد، پس
/// «مقدار سرور» در GET و در ذخیره یکی است). عضویت و اندازهٔ نهایی و نرخ دستی عمل‌های
/// <c>POST /api/shipments/{id}/lines/batch</c>‌اند: «شماره» در ردیف تازه = <c>add</c>، «نگله» = <c>move</c>، «طول/عرض» =
/// <c>measure</c>. قفل «قطعی» را پاس سازگاری روی هر دو مسیر می‌سنجد (C.8).</para>
///
/// <para><b>ارزش متر مربع:</b> دلار است نه تومان، پس هیچ نوع خانهٔ پولی جدول (که با رقم اعشار تومان می‌خواند) به آن
/// نمی‌خورد؛ ستون فقط‌خواندنی است با چیپ منبع (<see cref="BalesGridKeys.RateSourceLabel"/>)، و نرخ دستی بازرگان و فرم «نرخ
/// بگذارید» کار خودِ جزیره‌اند (عمل <c>rate</c> و <c>POST /api/declared-values</c>).</para>
///
/// <para>هیچ مبلغ تومانی در این جدول نیست (صفحه‌های محموله بی‌پول‌اند، A.5)؛ «حذف این فرش» هم نیست — منوی ردیف «خارج
/// کردن از نگله» و «انتقال» دارد (G.3).</para>
/// </summary>
public sealed class BalesGridSource(
    IShipmentRepository shipments,
    IShipmentSettingsReader settingsReader,
    IIntakeGridRepository repository,
    RugGridReader reader,
    IUserPreferencesService userPreferences) : IGridSource
{
    public string GridId => TenantGridLayout.BalesGridId;

    /// <summary>عنوان در صفحهٔ «ستون‌های جدول»؛ بی واژهٔ نگله، چون عنوان بیننده ندارد و «عدل» را نمی‌شناسد.</summary>
    public string Title => "جدول بسته‌بندی محموله";

    public bool EditsRugs => true;

    public async Task<IReadOnlyList<GridColumnDto>> DefaultColumnsAsync(GridViewer viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        var catalog = await reader.CatalogAsync(viewer.TenantId, cancellationToken);
        var simple = await IsSimpleAsync(viewer, cancellationToken);
        return Columns(viewer.Terms, catalog, simple);
    }

    /// <summary>
    /// ستون‌های پیش‌فرض (خالص، برای آزمون): خانه‌های توصیفی در نمای ساده ایستگاه Tab نیستند و در این جدول «چسبان» هم نیستند
    /// (ردیف تازهٔ نگله فرشی است که از پیش ثبت شده، نه فرش تازه).
    /// </summary>
    public static IReadOnlyList<GridColumnDto> Columns(BaleTermWords terms, RugGridCatalog catalog, bool simple)
    {
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(catalog);

        GridColumnDto Descriptive(GridColumnDto column) => column with { Sticky = false, TabStop = !simple };

        var columns = new List<GridColumnDto>
        {
            // ردیف تازه نگله‌اش را از «نگلهٔ جاری» می‌گیرد (C.2)، پس این خانه لازم نیست
            new(BalesGridKeys.BaleNumber, terms.Bale, GridColumnKind.Int, 7, false, false, false, null, false),
            RugGridColumns.Serial(),
            new(RugFieldKeys.Quantity, "تعداد", GridColumnKind.ReadOnly, 7, false, true, false, null, false, TabStop: false)
        };
        columns.AddRange(RugGridColumns.ColorColumns(catalog).Select(Descriptive));
        columns.Add(Descriptive(RugGridColumns.Pattern()));
        columns.Add(Descriptive(RugGridColumns.Origin()));
        columns.Add(Descriptive(RugGridColumns.Material()));
        columns.Add(new GridColumnDto(BalesGridKeys.IntakeSize, "اندازهٔ اول", GridColumnKind.ReadOnly, 14, false, true, false,
            null, false, TabStop: false));
        columns.Add(new GridColumnDto(RugFieldKeys.FinalLength, "طول", GridColumnKind.Dim, 8, false, false, false, null, false));
        columns.Add(new GridColumnDto(RugFieldKeys.FinalWidth, "عرض", GridColumnKind.Dim, 8, false, false, false, null, false));
        columns.Add(new GridColumnDto(BalesGridKeys.LineArea, "مساحت", GridColumnKind.ReadOnly, 16, false, true, false, null,
            false, TabStop: false));
        columns.Add(new GridColumnDto(BalesGridKeys.UsdPerM2, "ارزش متر مربع", GridColumnKind.ReadOnly, 12, false, true, false,
            null, false, TabStop: false));
        columns.Add(new GridColumnDto(BalesGridKeys.LineUsd, "جمع کل دلار", GridColumnKind.ReadOnly, 12, false, true, false,
            null, false, TabStop: false));
        return columns;
    }

    public async Task<GridDataDto> LoadAsync(
        GridViewer viewer, GridLoadRequest request, IReadOnlyList<GridColumnDto> columns,
        IReadOnlyList<GridColumnDto> allColumns, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        ArgumentNullException.ThrowIfNull(request);

        // بی محموله، یا محمولهٔ کارگاه دیگر: درست مثل محمولهٔ ناموجود ۴۰۴ (وجودش لو نمی‌رود)
        var shipmentId = request.ShipmentId ?? throw new KeyNotFoundException("محموله یافت نشد.");
        _ = await shipments.GetHeaderAsync(viewer.TenantId, shipmentId, cancellationToken)
            ?? throw new KeyNotFoundException("محموله یافت نشد.");

        var settings = await settingsReader.GetAsync(viewer.TenantId, cancellationToken);
        var lines = await shipments.ListRowsAsync(viewer.TenantId, shipmentId, settings.KeyField, cancellationToken: cancellationToken);

        // خانه‌های فرش با همان سازنده‌ای که ذخیرهٔ دسته‌ای «مقدار سرور» را با آن می‌سازد (ادغام سه‌طرفه یک شکل دارد)
        var snapshots = (await repository.ListByIdsAsync(
                viewer.TenantId, lines.Select(l => l.RugId).Distinct().ToList(), cancellationToken))
            .ToDictionary(s => s.Id);
        var builder = await reader.BuilderAsync(viewer, allColumns, cancellationToken);

        var rows = new List<GridRowDto>(lines.Count);
        foreach (var line in lines)
        {
            if (!snapshots.TryGetValue(line.RugId, out var rug)) continue; // فرش همین حالا به سطل رفت
            rows.Add(Row(builder, rug, line, viewer.Terms));
        }

        return new GridDataDto(columns, rows, Totals(lines), NextSerial: null);
    }

    /// <summary>
    /// یک ردیف: خانه‌های فرش از سازندهٔ مشترک، به‌اضافهٔ «نگله» در Values و داده‌های خط (مساحت، نرخ، دلار، اندازهٔ اول) در
    /// Derived. نسخهٔ ردیف نسخهٔ فرش است (برای ذخیرهٔ خانه‌های توصیفی)، و نسخهٔ خط جدا در <see cref="GridRowDto.LineVersion"/>.
    /// </summary>
    public static GridRowDto Row(RugGridRowBuilder builder, GridRugSnapshot rug, ShipmentRowData line, BaleTermWords terms)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(rug);
        ArgumentNullException.ThrowIfNull(line);

        var values = builder.Values(rug);
        values[BalesGridKeys.BaleNumber] = Json(line.BaleNumber);

        var derived = builder.Derived(rug, canDelete: false);
        derived.Remove(RugFieldKeys.PurchaseTotal); // جدول محموله بی‌پول است، حتی برای بازرگان

        var row = ShipmentMapping.ToRow(line);
        derived[BalesGridKeys.BaleId] = Json(line.BaleId);
        derived[BalesGridKeys.BaleNumber] = Json(line.BaleNumber);
        derived[BalesGridKeys.LineSeq] = Json(line.Seq);
        derived[RugFieldKeys.Quantity] = Json(line.Quantity);
        derived[BalesGridKeys.IntakeLength] = Json(RugGridRowBuilder.Norm(line.IntakeLength));
        derived[BalesGridKeys.IntakeWidth] = Json(RugGridRowBuilder.Norm(line.IntakeWidth));
        derived[BalesGridKeys.IntakeSize] = Json(SizeText(line.IntakeLength, line.IntakeWidth));
        derived[BalesGridKeys.SizeChangeCm] = Json(row.SizeChangeCm);
        derived[BalesGridKeys.SizeDiffers] = Json(row.FinalLength is { } fl && row.FinalWidth is { } fw
                                                  && (fl != row.IntakeLength || fw != row.IntakeWidth));
        derived[BalesGridKeys.PieceArea] = Json(RugGridRowBuilder.Norm(row.AreaM2));
        derived[BalesGridKeys.LineArea] = Json(RugGridRowBuilder.Norm(row.LineAreaM2));
        derived[BalesGridKeys.LineAreaText] = Json(AreaText(row.AreaM2, row.Qty, row.LineAreaM2));
        derived[BalesGridKeys.UsdPerM2] = Json(RugGridRowBuilder.Norm(row.UsdPerM2));
        derived[BalesGridKeys.RateSource] = Json((int)row.RateSource);
        derived[BalesGridKeys.RateSourceLabel] = Json(RateSourceLabel(row.RateSource));
        derived[BalesGridKeys.UsdPerM2Override] = Json(RugGridRowBuilder.Norm(row.UsdPerM2Override));
        derived[BalesGridKeys.DeclaredKey] = Json(row.DeclaredKey);
        derived[BalesGridKeys.PieceUsd] = Json(RugGridRowBuilder.Norm(row.PieceUsd));
        derived[BalesGridKeys.LineUsd] = Json(RugGridRowBuilder.Norm(row.LineUsd));

        return new GridRowDto(rug.Id, rug.Version, rug.Sku, values, derived, line.LineId, line.LineVersion);
    }

    /// <summary>
    /// جمع محموله: ردیف‌ها (خط‌ها)، Σ تخته و Σ مساحت خط‌ها (قاعدهٔ هر تخته)؛ بی‌پول. پانویس هر نگله (با دلار) را جزیره از
    /// همین ردیف‌ها یا از <c>BaleSummaryDto</c> پاسخ عمل‌ها می‌سازد.
    /// </summary>
    public static GridTotalsDto Totals(IReadOnlyList<ShipmentRowData> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var area = lines.Sum(l => l.HasValue ? l.LineAreaM2 : l.AreaM2 * l.Quantity);
        return new GridTotalsDto(lines.Count, lines.Sum(l => l.Quantity), RugGridRowBuilder.Norm(area), Purchase: null);
    }

    /// <summary>«۲٫۰۸ × ۲٫۱۰» (طول × عرض، دو رقم اعشار، ارقام فارسی).</summary>
    public static string SizeText(decimal length, decimal width) => $"{Dim(length)} × {Dim(width)}";

    /// <summary>متن مساحت خط: تک «۴٫۳۷»، دسته «۰٫۱۶ × ۵۰ = ۸٫۰۰».</summary>
    public static string AreaText(decimal pieceArea, int quantity, decimal lineArea)
        => quantity <= 1
            ? Dim(pieceArea)
            : $"{Dim(pieceArea)} × {PersianText.ToPersianDigits(quantity.ToString(CultureInfo.InvariantCulture))} = {Dim(lineArea)}";

    /// <summary>برچسب چیپ منبع نرخ (H.3b «RateSource»).</summary>
    public static string RateSourceLabel(DeclaredRateSource source) => source switch
    {
        DeclaredRateSource.Override => Messages.RateSourceOverride.Text,
        DeclaredRateSource.Table => Messages.RateSourceTable.Text,
        DeclaredRateSource.Fallback => Messages.RateSourceFallback.Text,
        _ => Messages.RateSourceMissing.Text
    };

    /// <summary>
    /// نمای ساده از حالت ذخیره‌شدهٔ همین کاربر (بازرگان و انباردار پیش‌فرض ساده‌اند). بی کاربر (کار سیستمی) یا اگر خوانده
    /// نشد: کامل، که فقط Tab بیشتری دارد و چیزی پنهان نمی‌کند.
    /// </summary>
    private async Task<bool> IsSimpleAsync(GridViewer viewer, CancellationToken cancellationToken)
    {
        if (viewer.UserId is not { } userId) return false;
        try
        {
            return (await userPreferences.GetAsync(userId, cancellationToken)).UiMode == UiMode.Simple;
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
    }

    private static string Dim(decimal value)
        => PersianText.ToPersianDigits(value.ToString("0.00", CultureInfo.InvariantCulture)).Replace('.', '٫');

    private static JsonElement? Json<T>(T? value) => value is null ? null : JsonSerializer.SerializeToElement(value);
}
