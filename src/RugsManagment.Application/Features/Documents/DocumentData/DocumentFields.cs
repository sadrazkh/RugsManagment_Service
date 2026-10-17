using System.Globalization;
using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Documents.Expressions;
using RugsManagment.Application.Fields;

namespace RugsManagment.Application.Features.Documents.Data;

/// <summary>
/// کلیدهای کاتالوگ که موتور سند خودش می‌شناسد (spec E.3): خط، نگله، محموله، کارگاه و واژه‌ها، به‌اضافهٔ سه کلید فرش
/// که فقط سند لازم دارد (اندازهٔ مؤثر و جای فرش). کلیدهای خودِ فرش (<c>rug.serial</c>، <c>rug.origin</c>، …) در
/// <see cref="RugFieldKeys"/> اند.
/// </summary>
public static class DocumentFieldKeys
{
    // ── خط ──
    public const string LineLength = "line.length";
    public const string LineWidth = "line.width";
    public const string LineArea = "line.area";
    public const string LineUsdPerM2 = "line.usdPerM2";
    public const string LinePieceUsd = "line.pieceUsd";
    public const string LineRateSource = "line.rateSource";

    // ── نگله ──
    public const string BaleNumber = "bale.number";
    public const string BalePieces = "bale.pieces";
    public const string BaleArea = "bale.area";
    public const string BaleUsd = "bale.usd";

    // ── محموله ──
    public const string ShipmentNumber = "shipment.number";
    public const string ShipmentTitle = "shipment.title";
    public const string ShipmentDocumentDate = "shipment.documentDate";
    public const string ShipmentDestination = "shipment.destination";
    public const string ShipmentBuyerName = "shipment.buyerName";
    public const string ShipmentDeclarationNo = "shipment.declarationNo";
    public const string ShipmentCustomsOffice = "shipment.customsOffice";
    public const string ShipmentUsdRate = "shipment.usdRate";

    // ── کارگاه ──
    public const string TenantName = "tenant.name";
    public const string TenantExporterName = "tenant.exporterName";
    public const string TenantExporterCode = "tenant.exporterCode";
    public const string TenantExporterAddress = "tenant.exporterAddress";
    public const string TenantExporterPhone = "tenant.exporterPhone";
    public const string TenantCustomsOffice = "tenant.customsOffice";

    // ── واژه‌ها (I.6) ──
    public const string TermsBale = "terms.bale";
    public const string TermsBales = "terms.bales";
    public const string TermsBaleOf = "terms.baleOf";
    public const string TermsBaleFooter = "terms.baleFooter";

    // ── فرش، فقط برای سند ──
    /// <summary>طول مؤثر فرش (نهایی، وگرنه اول) — برای فهرست فرشِ بی‌محموله.</summary>
    public const string RugLength = "rug.length";

    /// <summary>عرض مؤثر فرش.</summary>
    public const string RugWidth = "rug.width";

    /// <summary>جای فرش به متن (C.10).</summary>
    public const string RugLocation = "rug.location";
}

/// <summary>
/// فیلدهای کاتالوگِ موتور سند و نگاشت هر کلید به مقدارش از <see cref="DocumentData"/>.
///
/// کلیدهای خط، نگله، محموله، صادرکننده و واژه‌ها را <c>ShipmentFieldContributor</c> (P4a) هم برای کاتالوگ سند می‌دهد و
/// کاتالوگ از کلید تکراری همان contributor با Order کوچک‌تر را نگه می‌دارد؛ پس <c>DocumentFieldContributor</c> (P6-wiring)
/// فقط کلیدهایی از <see cref="Builtin"/> را لازم دارد که آن‌جا نیستند (<c>tenant.name</c>، <c>rug.length</c>،
/// <c>rug.width</c>، <c>rug.location</c>). نوع و «پول بودن» هر کلید در هر دو فهرست یکی است؛ موتور برای هر کلیدی که یکی از
/// این دو فهرست بدهد مقدار دارد (<see cref="Value"/>). آزمون‌ها و اعتبارسنجی قالب‌های پیش‌فرض بی دیتابیس با
/// <see cref="Catalogue"/> کار می‌کنند. برچسب‌ها واژهٔ نگله/عدل کارگاه را می‌گیرند.
/// </summary>
public static class DocumentFields
{
    /// <summary>فیلدهای خط، نگله، محموله، کارگاه، واژه‌ها و سه کلید سندیِ فرش.</summary>
    public static IReadOnlyList<FieldDefDto> Builtin(BaleTermWords? terms = null)
    {
        var t = terms ?? BaleTerms.Negleh;
        return
        [
            new(DocumentFieldKeys.LineLength, "طول", FieldScope.Line, FieldDataType.Dim, "0.00", false, false, false),
            new(DocumentFieldKeys.LineWidth, "عرض", FieldScope.Line, FieldDataType.Dim, "0.00", false, false, false),
            new(DocumentFieldKeys.LineArea, "مساحت هر تخته", FieldScope.Line, FieldDataType.Area, "0.00", true, false, false),
            new(DocumentFieldKeys.LineUsdPerM2, "ارزش متر مربع", FieldScope.Line, FieldDataType.Number, "0.00", false, false, false),
            new(DocumentFieldKeys.LinePieceUsd, "دلار هر تخته", FieldScope.Line, FieldDataType.Number, "0.00", true, false, false),
            new(DocumentFieldKeys.LineRateSource, "منبع ارزش گمرکی", FieldScope.Line, FieldDataType.Text, "", false, false, false),

            new(DocumentFieldKeys.BaleNumber, $"شمارهٔ {t.Bale}", FieldScope.Bale, FieldDataType.Int, "0", false, false, false),
            new(DocumentFieldKeys.BalePieces, $"تختهٔ {t.Bale}", FieldScope.Bale, FieldDataType.Int, "0", true, false, false),
            new(DocumentFieldKeys.BaleArea, $"متراژ {t.Bale}", FieldScope.Bale, FieldDataType.Area, "0.00", true, false, false),
            new(DocumentFieldKeys.BaleUsd, $"ارزش گمرکی {t.Bale}", FieldScope.Bale, FieldDataType.Number, "0.00", true, false, false),

            new(DocumentFieldKeys.ShipmentNumber, "شمارهٔ محموله", FieldScope.Shipment, FieldDataType.Int, "0", false, false, false),
            new(DocumentFieldKeys.ShipmentTitle, "عنوان محموله", FieldScope.Shipment, FieldDataType.Text, "", false, false, false),
            new(DocumentFieldKeys.ShipmentDocumentDate, "تاریخ سند", FieldScope.Shipment, FieldDataType.Date, "", false, false, false),
            new(DocumentFieldKeys.ShipmentDestination, "مقصد", FieldScope.Shipment, FieldDataType.Text, "", false, false, false),
            new(DocumentFieldKeys.ShipmentBuyerName, "خریدار", FieldScope.Shipment, FieldDataType.Text, "", false, false, false),
            new(DocumentFieldKeys.ShipmentDeclarationNo, "شمارهٔ اظهارنامه", FieldScope.Shipment, FieldDataType.Text, "", false, false, false),
            new(DocumentFieldKeys.ShipmentCustomsOffice, "گمرک", FieldScope.Shipment, FieldDataType.Text, "", false, false, false),
            // جدول E.3 نرخ دلار را در فیلدهای محموله می‌آورد، نه در ردیف پول؛ پس Money = false (همان ShipmentFieldContributor)
            new(DocumentFieldKeys.ShipmentUsdRate, "نرخ دلار", FieldScope.Shipment, FieldDataType.Number, "#,0.######", false, false, false),

            new(DocumentFieldKeys.TenantName, "نام کارگاه", FieldScope.Tenant, FieldDataType.Text, "", false, false, false),
            new(DocumentFieldKeys.TenantExporterName, "نام صادرکننده", FieldScope.Tenant, FieldDataType.Text, "", false, false, false),
            new(DocumentFieldKeys.TenantExporterCode, "کد صادرکننده", FieldScope.Tenant, FieldDataType.Text, "", false, false, false),
            new(DocumentFieldKeys.TenantExporterAddress, "نشانی صادرکننده", FieldScope.Tenant, FieldDataType.Text, "", false, false, false),
            new(DocumentFieldKeys.TenantExporterPhone, "تلفن صادرکننده", FieldScope.Tenant, FieldDataType.Text, "", false, false, false),
            new(DocumentFieldKeys.TenantCustomsOffice, "گمرک صادرکننده", FieldScope.Tenant, FieldDataType.Text, "", false, false, false),

            new(DocumentFieldKeys.TermsBale, $"واژهٔ «{t.Bale}»", FieldScope.Terms, FieldDataType.Text, "", false, false, false),
            new(DocumentFieldKeys.TermsBales, $"واژهٔ «{t.Bales}»", FieldScope.Terms, FieldDataType.Text, "", false, false, false),
            new(DocumentFieldKeys.TermsBaleOf, $"واژهٔ «{t.BaleOf}»", FieldScope.Terms, FieldDataType.Text, "", false, false, false),
            new(DocumentFieldKeys.TermsBaleFooter, $"واژهٔ «{t.BaleFooter}»", FieldScope.Terms, FieldDataType.Text, "", false, false, false),

            new(DocumentFieldKeys.RugLength, "طول فعلی", FieldScope.Rug, FieldDataType.Dim, "0.00", false, false, false),
            new(DocumentFieldKeys.RugWidth, "عرض فعلی", FieldScope.Rug, FieldDataType.Dim, "0.00", false, false, false),
            new(DocumentFieldKeys.RugLocation, "جای فرش", FieldScope.Rug, FieldDataType.Text, "", false, false, false)
        ];
    }

    /// <summary>
    /// کاتالوگ کامل برای سند بی دیتابیس: فیلدهای ثابت فرش (<see cref="RugFieldContributor.StaticFields"/>)، مبلغ‌های فرش،
    /// <see cref="Builtin"/> و فیلدهای سفارشی داده‌شده. برای اعتبارسنجی قالب‌های پیش‌فرض و آزمون‌ها.
    /// </summary>
    public static IReadOnlyList<FieldDefDto> Catalogue(IEnumerable<FieldDefDto>? custom = null, BaleTermWords? terms = null)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<FieldDefDto>();
        foreach (var field in RugFieldContributor.StaticFields
                     .Concat(RugMoneyFieldContributor.Fields)
                     .Concat(Builtin(terms))
                     .Concat(custom ?? []))
        {
            if (seen.Add(field.Key))
                result.Add(field);
        }

        return result;
    }

    /// <summary>یک فیلد سفارشی متنی برای کاتالوگ آزمون (<c>rug.custom.bg_color</c> «رنگ متن»).</summary>
    public static FieldDefDto CustomText(string key, string label)
        => new(RugFieldKeys.ForCustomField(key), label, FieldScope.Rug, FieldDataType.Combo, "", false, false, true);

    /// <summary>نوع عبارتِ یک فیلد کاتالوگ.</summary>
    public static ExprType TypeOf(FieldDefDto field) => field.DataType switch
    {
        FieldDataType.Int or FieldDataType.Number or FieldDataType.Dim or FieldDataType.Area or FieldDataType.Money
            or FieldDataType.MoneyUnit or FieldDataType.Percent => ExprType.Number,
        FieldDataType.Bool => ExprType.Bool,
        FieldDataType.Date => ExprType.Date,
        _ => ExprType.Text
    };

    /// <summary>
    /// مقدار یک فیلد برای یک خانه. <paramref name="line"/> و <paramref name="bale"/> برای عنوان و خلاصه null‌اند.
    /// فیلد سفارشی با نوعش در کاتالوگ (<paramref name="field"/>) به عدد یا تاریخ تبدیل می‌شود.
    /// </summary>
    public static DocValue Value(string key, DocumentData data, DocumentBale? bale, DocumentLine? line, FieldDefDto? field)
    {
        var rug = line?.Rug;
        switch (key)
        {
            // ── فرش ──
            case RugFieldKeys.Serial: return DocValue.Of(rug?.Serial);
            case RugFieldKeys.Quantity: return rug is null ? DocValue.Blank : DocValue.FromNumber(rug.Qty);
            case RugFieldKeys.Title: return DocValue.Of(rug?.Title);
            case RugFieldKeys.Pattern: return DocValue.Of(rug?.Pattern);
            case RugFieldKeys.Origin: return DocValue.Of(rug?.Origin);
            case RugFieldKeys.Material: return DocValue.Of(rug?.Material);
            case RugFieldKeys.KnotDensity: return DocValue.Of((decimal?)rug?.KnotDensity);
            case RugFieldKeys.IntakeLength: return DocValue.Of(rug?.IntakeLength);
            case RugFieldKeys.IntakeWidth: return DocValue.Of(rug?.IntakeWidth);
            case RugFieldKeys.FinalLength: return DocValue.Of(rug?.FinalLength);
            case RugFieldKeys.FinalWidth: return DocValue.Of(rug?.FinalWidth);
            case RugFieldKeys.Sku: return DocValue.Of(rug?.Sku);
            case RugFieldKeys.Notes: return DocValue.Of(rug?.Notes);
            case DocumentFieldKeys.RugLength: return DocValue.Of(rug?.Length);
            case DocumentFieldKeys.RugWidth: return DocValue.Of(rug?.Width);
            case DocumentFieldKeys.RugLocation: return DocValue.Of(rug?.Location);
            case RugFieldKeys.AreaM2:
                return DocValue.Of(rug?.Length is { } l && rug.Width is { } w ? Domain.Common.AreaMath.PieceArea(l, w) : null);
            case RugFieldKeys.TotalAreaM2:
                return DocValue.Of(rug?.Length is { } tl && rug.Width is { } tw
                    ? Domain.Common.AreaMath.LineArea(Domain.Common.AreaMath.PieceArea(tl, tw), rug.Qty)
                    : null);
            case RugFieldKeys.IntakeAreaM2:
                return DocValue.Of(rug?.IntakeLength is { } il && rug.IntakeWidth is { } iw ? Domain.Common.AreaMath.PieceArea(il, iw) : null);

            // ── خط ──
            case DocumentFieldKeys.LineLength: return DocValue.Of(line?.Length);
            case DocumentFieldKeys.LineWidth: return DocValue.Of(line?.Width);
            case DocumentFieldKeys.LineArea: return DocValue.Of(line?.EffectiveArea);
            case DocumentFieldKeys.LineUsdPerM2: return DocValue.Of(line?.UsdPerM2);
            case DocumentFieldKeys.LinePieceUsd: return DocValue.Of(line?.EffectivePieceUsd);
            case DocumentFieldKeys.LineRateSource: return DocValue.Of(line?.RateSource);

            // ── نگله ──
            case DocumentFieldKeys.BaleNumber: return bale is null ? DocValue.Blank : DocValue.FromNumber(bale.Number);
            case DocumentFieldKeys.BalePieces: return bale is null ? DocValue.Blank : DocValue.FromNumber(bale.EffectivePieces);
            case DocumentFieldKeys.BaleArea: return bale is null ? DocValue.Blank : DocValue.FromNumber(bale.EffectiveAreaM2);
            case DocumentFieldKeys.BaleUsd: return bale is null ? DocValue.Blank : DocValue.FromNumber(bale.EffectiveUsd);

            // ── محموله ──
            case DocumentFieldKeys.ShipmentNumber: return data.Shipment is null ? DocValue.Blank : DocValue.FromNumber(data.Shipment.Number);
            case DocumentFieldKeys.ShipmentTitle: return DocValue.Of(data.Shipment?.Title);
            case DocumentFieldKeys.ShipmentDocumentDate: return DocValue.Of(data.Shipment?.DocumentDate);
            case DocumentFieldKeys.ShipmentDestination: return DocValue.Of(data.Shipment?.Destination);
            case DocumentFieldKeys.ShipmentBuyerName: return DocValue.Of(data.Shipment?.BuyerName);
            case DocumentFieldKeys.ShipmentDeclarationNo: return DocValue.Of(data.Shipment?.DeclarationNo);
            case DocumentFieldKeys.ShipmentCustomsOffice: return DocValue.Of(data.Shipment?.CustomsOffice);
            case DocumentFieldKeys.ShipmentUsdRate: return DocValue.Of(data.Shipment?.UsdRate);

            // ── کارگاه و واژه‌ها ──
            case DocumentFieldKeys.TenantName: return DocValue.Of(data.Tenant.Name);
            case DocumentFieldKeys.TenantExporterName: return DocValue.Of(data.Tenant.ExporterName);
            case DocumentFieldKeys.TenantExporterCode: return DocValue.Of(data.Tenant.ExporterCode);
            case DocumentFieldKeys.TenantExporterAddress: return DocValue.Of(data.Tenant.ExporterAddress);
            case DocumentFieldKeys.TenantExporterPhone: return DocValue.Of(data.Tenant.ExporterPhone);
            case DocumentFieldKeys.TenantCustomsOffice: return DocValue.Of(data.Tenant.CustomsOffice);
            case DocumentFieldKeys.TermsBale: return DocValue.Of(data.Terms.Bale);
            case DocumentFieldKeys.TermsBales: return DocValue.Of(data.Terms.Bales);
            case DocumentFieldKeys.TermsBaleOf: return DocValue.Of(data.Terms.BaleOf);
            case DocumentFieldKeys.TermsBaleFooter: return DocValue.Of(data.Terms.BaleFooter);
        }

        if (key.StartsWith(RugFieldKeys.CustomPrefix, StringComparison.Ordinal))
        {
            string? text = null;
            rug?.Custom?.TryGetValue(key[RugFieldKeys.CustomPrefix.Length..], out text);
            return Typed(text, field);
        }

        if (RugFieldKeys.IsMoney(key))
        {
            decimal? amount = null;
            rug?.Money?.TryGetValue(key, out amount);
            return DocValue.Of(amount);
        }

        return DocValue.Blank;
    }

    /// <summary>متن ذخیره‌شدهٔ فیلد سفارشی به مقدار با نوع کاتالوگ.</summary>
    private static DocValue Typed(string? text, FieldDefDto? field)
    {
        if (string.IsNullOrWhiteSpace(text))
            return DocValue.Blank;
        var type = field is null ? ExprType.Text : TypeOf(field);
        return type switch
        {
            ExprType.Number when decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) => DocValue.FromNumber(n),
            ExprType.Date when DateOnly.TryParse(text, CultureInfo.InvariantCulture, out var d) => DocValue.FromDate(d),
            ExprType.Bool when bool.TryParse(text, out var b) => DocValue.FromBool(b),
            _ => DocValue.FromText(text)
        };
    }

    /// <summary>
    /// نشانه‌های <c>{terms.bale}</c>، <c>{terms.baleFooter}</c>، … در برچسب‌ها و متن‌های ثابت قالب (E.2 «Text tokens»).
    /// </summary>
    public static string ReplaceTokens(string? text, BaleTermWords terms)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains('{', StringComparison.Ordinal))
            return text ?? "";
        return text
            .Replace("{terms.bale}", terms.Bale, StringComparison.Ordinal)
            .Replace("{terms.bales}", terms.Bales, StringComparison.Ordinal)
            .Replace("{terms.baleOf}", terms.BaleOf, StringComparison.Ordinal)
            .Replace("{terms.baleFooter}", terms.BaleFooter, StringComparison.Ordinal);
    }
}
