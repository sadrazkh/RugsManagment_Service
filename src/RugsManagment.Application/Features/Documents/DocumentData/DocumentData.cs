using RugsManagment.Application.Common;
using RugsManagment.Domain.Common;

namespace RugsManagment.Application.Features.Documents.Data;

/// <summary>
/// ورودی موتور سند (spec E.5): یک گراف رکوردیِ خالص — سرِ محموله، نگله‌ها و خط‌ها با همهٔ فیلدهای کاتالوگ — که
/// <see cref="Composer.DocumentComposer"/> از آن پیش‌نمایش، صفحهٔ چاپ و فایل اکسل را می‌سازد.
///
/// چرا رکورد خالص و نه موجودیت EF: موتور سند باید بی دیتابیس آزمون شود (آزمون طلایی با فایل نمونهٔ بازرگان) و
/// محمولهٔ «قطعی» باید عیناً از عکس لحظهٔ قطعی شدن (<c>SnapshotJson</c>) ساخته شود، نه از دادهٔ امروز. پس هر دو مسیر
/// (دیتابیس و عکس) فقط همین رکورد را پر می‌کنند و موتور فرقی نمی‌بیند. این رکورد با System.Text.Json رفت‌وبرگشت دارد.
/// </summary>
/// <param name="Shipment">سر محموله؛ برای فهرست فرش (بی محموله) null.</param>
/// <param name="Bales">نگله‌ها به ترتیب شماره، هر کدام با خط‌هایش به ترتیب <c>Seq</c>. نگلهٔ خالی هم می‌آید و موتور آن را
/// با <c>W_EMPTY_BALE</c> کنار می‌گذارد.</param>
/// <param name="Lines">ردیف‌های بی‌نگله (فهرست فرش، <c>POST /documents/rugs.xlsx</c>)؛ برای اظهار محموله خالی.</param>
/// <param name="Terms">واژه‌های نگله/عدل کارگاه (<see cref="BaleTerms"/>).</param>
/// <param name="Tenant">مشخصات کارگاه برای فیلدهای <c>tenant.*</c>.</param>
/// <param name="IsDraft">پیش‌نویس (محمولهٔ باز): سرصفحهٔ «پیش‌نویس» می‌گیرد؛ قطعی سرصفحه ندارد.</param>
public sealed record DocumentData(
    DocumentShipment? Shipment,
    IReadOnlyList<DocumentBale> Bales,
    IReadOnlyList<DocumentLine> Lines,
    BaleTermWords Terms,
    DocumentTenant Tenant,
    bool IsDraft)
{
    /// <summary>دست‌کم یک ردیف دسته دارد (تعداد ≠ ۱)؛ ستون «تعداد» با <c>visibleWhen: hasLots</c> فقط آن‌وقت دیده می‌شود.</summary>
    public bool HasLots => AllLines().Any(l => l.Rug.Qty != 1);

    /// <summary>همهٔ خط‌ها: اول خط‌های نگله‌ها به ترتیب، بعد ردیف‌های بی‌نگله.</summary>
    public IEnumerable<DocumentLine> AllLines() => Bales.SelectMany(b => b.Lines).Concat(Lines);
}

/// <summary>سر محموله (فیلدهای <c>shipment.*</c>، E.3).</summary>
/// <param name="Id">شناسهٔ محموله (فقط برای ردگیری؛ در سند نمی‌آید).</param>
/// <param name="Number">شمارهٔ محموله.</param>
/// <param name="Title">عنوان.</param>
/// <param name="DocumentDate">تاریخ سند (عنوان اظهار با <c>jdot</c> شمسی می‌شود: «1405.06.15»).</param>
/// <param name="Destination">مقصد.</param>
/// <param name="BuyerName">خریدار.</param>
/// <param name="DeclarationNo">شمارهٔ اظهارنامه.</param>
/// <param name="CustomsOffice">گمرک.</param>
/// <param name="UsdRate">نرخ دلار محموله (تومان).</param>
public sealed record DocumentShipment(
    Guid? Id,
    int Number,
    string? Title,
    DateOnly? DocumentDate,
    string? Destination,
    string? BuyerName,
    string? DeclarationNo,
    string? CustomsOffice,
    decimal? UsdRate);

/// <summary>
/// یک نگله با خط‌هایش. جمع‌های دفتر (<paramref name="Pieces"/>، <paramref name="AreaM2"/>، <paramref name="Usd"/>) همان
/// <c>bale.pieces/area/usd</c> کاتالوگ‌اند که پانویس سند با آن‌ها سنجیده می‌شود (<c>checkAgainst</c>، <c>W_CHECK_MISMATCH</c>)؛
/// null یعنی دفتر چیزی نگفته و سنجش انجام نمی‌شود (فیلد هم از خود خط‌ها حساب می‌شود).
/// </summary>
public sealed record DocumentBale(
    int Number,
    IReadOnlyList<DocumentLine> Lines,
    int? Pieces = null,
    decimal? AreaM2 = null,
    decimal? Usd = null)
{
    /// <summary>تعداد تخته: دفتر، وگرنه Σ تعداد.</summary>
    public int EffectivePieces => Pieces ?? Lines.Sum(l => l.Rug.Qty);

    /// <summary>متراژ: دفتر، وگرنه Σ (مساحت تخته × تعداد) — قاعدهٔ دسته (D.3).</summary>
    public decimal EffectiveAreaM2 => AreaM2 ?? Lines.Sum(l => (l.EffectiveArea ?? 0m) * l.Rug.Qty);

    /// <summary>دلار: دفتر، وگرنه Σ (دلار تخته × تعداد).</summary>
    public decimal EffectiveUsd => Usd ?? Lines.Sum(l => (l.EffectivePieceUsd ?? 0m) * l.Rug.Qty);
}

/// <summary>
/// یک خط محموله (یا یک ردیف فهرست فرش): فرش + اندازهٔ مؤثر و ارزش گمرکی (فیلدهای <c>line.*</c>). همه «برای یک تخته»اند؛
/// ضرب در تعداد کار پانویس است (SUMPRODUCT)، نه خط.
/// </summary>
/// <param name="Seq">ترتیب خط در نگله.</param>
/// <param name="Rug">فرش.</param>
/// <param name="Length">طول مؤثر (اندازهٔ نهایی، وگرنه اندازهٔ اول) — <c>line.length</c>.</param>
/// <param name="Width">عرض مؤثر — <c>line.width</c>.</param>
/// <param name="Area">مساحت یک تخته از دفتر — <c>line.area</c>؛ null یعنی از طول و عرض حساب شود.</param>
/// <param name="UsdPerM2">ارزش گمرکی هر متر — <c>line.usdPerM2</c>؛ null یعنی نامعلوم (خانهٔ خالی).</param>
/// <param name="PieceUsd">دلار یک تخته از دفتر — <c>line.pieceUsd</c>.</param>
/// <param name="RateSource">منبع نرخ («دستی»، «از جدول»، …) — <c>line.rateSource</c>.</param>
public sealed record DocumentLine(
    int Seq,
    DocumentRug Rug,
    decimal? Length,
    decimal? Width,
    decimal? Area = null,
    decimal? UsdPerM2 = null,
    decimal? PieceUsd = null,
    string? RateSource = null)
{
    /// <summary>مساحت تخته: دفتر، وگرنه ROUND(L×W, 2).</summary>
    public decimal? EffectiveArea => Area ?? (Length is { } l && Width is { } w ? AreaMath.PieceArea(l, w) : null);

    /// <summary>دلار تخته: دفتر، وگرنه ROUND(مساحت × نرخ، 2).</summary>
    public decimal? EffectivePieceUsd => PieceUsd
        ?? (EffectiveArea is { } a && UsdPerM2 is { } r ? AreaMath.PieceUsd(a, r) : null);
}

/// <summary>
/// فرشِ یک خط (فیلدهای <c>rug.*</c>). مقدار فیلدهای سفارشی متن است (همان شکلی که ذخیره شده)؛ موتور آن را با نوعِ فیلد در
/// کاتالوگ به عدد/تاریخ تبدیل می‌کند. مبلغ‌ها فقط برای Business پر می‌شوند (A.5) — موتور خودش چیزی را پنهان نمی‌کند.
/// </summary>
/// <param name="Id">شناسهٔ فرش.</param>
/// <param name="Serial">شماره («3778» یا «3911 - 3960»).</param>
/// <param name="Qty">تعداد تخته (۱ برای فرش تک).</param>
/// <param name="Title">عنوان.</param>
/// <param name="Pattern">نقشه.</param>
/// <param name="Origin">محل بافت.</param>
/// <param name="Material">مواد مصرفی.</param>
/// <param name="KnotDensity">رجشمار.</param>
/// <param name="IntakeLength">طول اول.</param>
/// <param name="IntakeWidth">عرض اول.</param>
/// <param name="FinalLength">طول نهایی.</param>
/// <param name="FinalWidth">عرض نهایی.</param>
/// <param name="Sku">کد.</param>
/// <param name="Notes">یادداشت.</param>
/// <param name="Location">جای فرش به متن («در انبار»، «در نگله»، …؛ C.10) — <c>rug.location</c>.</param>
/// <param name="Custom">فیلدهای سفارشی با کلید خودشان (<c>bg_color</c> → <c>rug.custom.bg_color</c>).</param>
/// <param name="Money">مبلغ‌ها با کلید کامل کاتالوگ (<c>rug.purchaseCost</c>، <c>purchase.price</c>، …).</param>
public sealed record DocumentRug(
    Guid? Id,
    string? Serial,
    int Qty,
    string? Title = null,
    string? Pattern = null,
    string? Origin = null,
    string? Material = null,
    int? KnotDensity = null,
    decimal? IntakeLength = null,
    decimal? IntakeWidth = null,
    decimal? FinalLength = null,
    decimal? FinalWidth = null,
    string? Sku = null,
    string? Notes = null,
    string? Location = null,
    IReadOnlyDictionary<string, string?>? Custom = null,
    IReadOnlyDictionary<string, decimal?>? Money = null)
{
    /// <summary>طول مؤثر: نهایی، وگرنه اول.</summary>
    public decimal? Length => FinalLength ?? IntakeLength;

    /// <summary>عرض مؤثر: نهایی، وگرنه اول.</summary>
    public decimal? Width => FinalWidth ?? IntakeWidth;
}

/// <summary>
/// مشخصات کارگاه و صادرکننده برای فیلدهای <c>tenant.*</c> (همان بخش «exporter» عکس قطعی، C.7، و ترجیحات کارگاه).
/// </summary>
/// <param name="Name">نام کارگاه — <c>tenant.name</c>.</param>
/// <param name="ExporterName">نام صادرکننده — <c>tenant.exporterName</c>.</param>
/// <param name="ExporterCode">کد صادرکننده — <c>tenant.exporterCode</c>.</param>
/// <param name="ExporterAddress">نشانی صادرکننده — <c>tenant.exporterAddress</c>.</param>
/// <param name="ExporterPhone">تلفن صادرکننده — <c>tenant.exporterPhone</c>.</param>
/// <param name="CustomsOffice">گمرک پیش‌فرض صادرکننده — <c>tenant.customsOffice</c>.</param>
public sealed record DocumentTenant(
    string? Name = null,
    string? ExporterName = null,
    string? ExporterCode = null,
    string? ExporterAddress = null,
    string? ExporterPhone = null,
    string? CustomsOffice = null);
