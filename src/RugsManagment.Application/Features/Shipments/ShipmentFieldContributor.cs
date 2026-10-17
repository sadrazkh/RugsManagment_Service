using RugsManagment.Application.Fields;

namespace RugsManagment.Application.Features.Shipments;

/// <summary>
/// فیلدهای خط، نگله، محموله، صادرکننده و واژه‌ها برای کاتالوگ (spec E.3): قالب‌های اکسل و چاپ و جدول نگله‌ها از همین
/// کلیدها می‌خوانند. دلار گمرکی پول داخلی نیست (<c>Money = false</c>) و برای همه می‌آید (A.5). هیچ‌کدام از راه
/// <c>RugFieldWriter</c> نوشتنی نیست: اندازهٔ نهایی کلید فرش است و نرخ دستی از عمل «rate» خط‌ها نوشته می‌شود.
/// </summary>
public sealed class ShipmentFieldContributor : IFieldCatalogContributor
{
    /// <summary>بعد از فیلدهای فرش (۱۰۰) و پول فرش.</summary>
    public int Order => 300;

    // ── کلیدها (یک جا، تا قالب‌ها و جدول همان رشته را به کار ببرند) ────────

    public const string LineLength = "line.length";
    public const string LineWidth = "line.width";
    public const string LineArea = "line.area";
    public const string LineUsdPerM2 = "line.usdPerM2";
    public const string LinePieceUsd = "line.pieceUsd";
    public const string LineRateSource = "line.rateSource";

    public const string BaleNumber = "bale.number";
    public const string BalePieces = "bale.pieces";
    public const string BaleArea = "bale.area";
    public const string BaleUsd = "bale.usd";

    public const string ShipmentNumber = "shipment.number";
    public const string ShipmentTitle = "shipment.title";
    public const string ShipmentDocumentDate = "shipment.documentDate";
    public const string ShipmentDestination = "shipment.destination";
    public const string ShipmentBuyerName = "shipment.buyerName";
    public const string ShipmentDeclarationNo = "shipment.declarationNo";
    public const string ShipmentCustomsOffice = "shipment.customsOffice";
    public const string ShipmentUsdRate = "shipment.usdRate";

    public const string TenantExporterName = "tenant.exporterName";
    public const string TenantExporterCode = "tenant.exporterCode";
    public const string TenantExporterAddress = "tenant.exporterAddress";
    public const string TenantExporterPhone = "tenant.exporterPhone";
    public const string TenantCustomsOffice = "tenant.customsOffice";

    public const string TermsBale = "terms.bale";
    public const string TermsBales = "terms.bales";
    public const string TermsBaleOf = "terms.baleOf";
    public const string TermsBaleFooter = "terms.baleFooter";

    /// <summary>فیلدهای خط — هم برای جدول نگله‌ها و هم برای قالب سند.</summary>
    public static IReadOnlyList<FieldDefDto> LineFields { get; } =
    [
        new(LineLength, "طول", FieldScope.Line, FieldDataType.Dim, "0.00", false, false, false),
        new(LineWidth, "عرض", FieldScope.Line, FieldDataType.Dim, "0.00", false, false, false),
        new(LineArea, "متراژ", FieldScope.Line, FieldDataType.Area, "0.00", true, false, false),
        new(LineUsdPerM2, "ارزش متر مربع", FieldScope.Line, FieldDataType.Number, "0.00", false, false, false),
        new(LinePieceUsd, "ارزش کل", FieldScope.Line, FieldDataType.Number, "0.00", true, false, false),
        new(LineRateSource, "منبع ارزش", FieldScope.Line, FieldDataType.Text, "", false, false, false)
    ];

    /// <summary>فیلدهای سند (نگله، محموله، صادرکننده، واژه‌ها).</summary>
    public static IReadOnlyList<FieldDefDto> DocumentFields { get; } =
    [
        new(BaleNumber, "شمارهٔ نگله", FieldScope.Bale, FieldDataType.Int, "0", false, false, false),
        new(BalePieces, "تعداد تخته‌های نگله", FieldScope.Bale, FieldDataType.Int, "0", true, false, false),
        new(BaleArea, "متراژ نگله", FieldScope.Bale, FieldDataType.Area, "0.00", true, false, false),
        new(BaleUsd, "ارزش نگله", FieldScope.Bale, FieldDataType.Number, "0.00", true, false, false),

        new(ShipmentNumber, "شمارهٔ محموله", FieldScope.Shipment, FieldDataType.Int, "0", false, false, false),
        new(ShipmentTitle, "عنوان محموله", FieldScope.Shipment, FieldDataType.Text, "", false, false, false),
        new(ShipmentDocumentDate, "تاریخ اظهار", FieldScope.Shipment, FieldDataType.Date, "", false, false, false),
        new(ShipmentDestination, "مقصد", FieldScope.Shipment, FieldDataType.Text, "", false, false, false),
        new(ShipmentBuyerName, "خریدار", FieldScope.Shipment, FieldDataType.Text, "", false, false, false),
        new(ShipmentDeclarationNo, "شمارهٔ اظهار", FieldScope.Shipment, FieldDataType.Text, "", false, false, false),
        new(ShipmentCustomsOffice, "گمرک", FieldScope.Shipment, FieldDataType.Text, "", false, false, false),
        new(ShipmentUsdRate, "نرخ دلار", FieldScope.Shipment, FieldDataType.Number, "#,0.######", false, false, false),

        new(TenantExporterName, "نام صادرکننده", FieldScope.Tenant, FieldDataType.Text, "", false, false, false),
        new(TenantExporterCode, "کد صادرکننده", FieldScope.Tenant, FieldDataType.Text, "", false, false, false),
        new(TenantExporterAddress, "نشانی صادرکننده", FieldScope.Tenant, FieldDataType.Text, "", false, false, false),
        new(TenantExporterPhone, "تلفن صادرکننده", FieldScope.Tenant, FieldDataType.Text, "", false, false, false),
        new(TenantCustomsOffice, "گمرک صادرکننده", FieldScope.Tenant, FieldDataType.Text, "", false, false, false),

        new(TermsBale, "واژهٔ نگله", FieldScope.Terms, FieldDataType.Text, "", false, false, false),
        new(TermsBales, "واژهٔ نگله‌ها", FieldScope.Terms, FieldDataType.Text, "", false, false, false),
        new(TermsBaleOf, "واژهٔ نگله پیش از شماره", FieldScope.Terms, FieldDataType.Text, "", false, false, false),
        new(TermsBaleFooter, "واژهٔ پانویس نگله", FieldScope.Terms, FieldDataType.Text, "", false, false, false)
    ];

    public Task<IReadOnlyList<FieldDefDto>> FieldsAsync(FieldCatalogRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        IReadOnlyList<FieldDefDto> fields = request.Purpose == FieldPurpose.Document
            ? [.. LineFields, .. DocumentFields]
            : LineFields;
        return Task.FromResult(fields);
    }
}
