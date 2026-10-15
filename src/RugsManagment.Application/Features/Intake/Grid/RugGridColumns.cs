using System.Text.Json;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.DTOs.Grid;
using RugsManagment.Application.Fields;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Features.Intake.Grid;

/// <summary>نام نوع خانه‌ها در <see cref="GridColumnDto.Type"/> (I.3) — همان رشته‌های <c>GridColumnType</c> در TS.</summary>
public static class GridColumnKind
{
    public const string Serial = "serial";
    public const string Text = "text";
    public const string Combo = "combo";
    public const string Select = "select";
    public const string Int = "int";
    public const string Dim = "dim";
    public const string Money = "money";
    public const string MoneyUnit = "money+unit";
    public const string Percent = "percent";
    public const string ReadOnly = "readonly";
}

/// <summary>کلیدهای جدول که در FieldCatalog فرش نیستند.</summary>
public static class GridKeys
{
    /// <summary>فروشندهٔ ورودی ردیف («فروشنده»، G.2). ورودی ردیف از (روز سربرگ، همین فروشنده) پیدا یا ساخته می‌شود.</summary>
    public const string Seller = "batch.seller";

    /// <summary>کلیدهای حساب‌شده در <see cref="GridRowDto.Derived"/> (قرارداد با جزیره).</summary>
    public const string CanDelete = "row.canDelete";
    public const string BatchId = "batch.id";
    public const string BatchNumber = "batch.number";
    public const string SerialFrom = "rug.serialFrom";
    public const string SerialTo = "rug.serialTo";
    public const string CreatedAt = "rug.createdAt";

    /// <summary>کلید رنگ متن و رنگ حاشیه (بستهٔ «صادرات فرش»، H.8) — در جدول ورود «چسبان»اند.</summary>
    public const string FieldColorKey = "bg_color";
    public const string BorderColorKey = "border_color";
}

/// <summary>
/// آنچه ستون‌های فرشیِ یک کارگاه را می‌سازد: فیلدهای سفارشی فعال و نوع‌های خرید «ستون در جدول ورود» (G.2 «Purchase
/// columns»). یک بار در هر درخواست خوانده می‌شود و هم ستون‌ها و هم مقدار خانه‌ها از آن ساخته می‌شوند.
/// </summary>
public sealed record RugGridCatalog(
    IReadOnlyList<CustomFieldDefinition> CustomFields,
    IReadOnlyList<CostCategory> PurchaseCategories)
{
    /// <summary>کلید جدول هر نوع خرید (<c>purchase.price</c>، <c>purchase.broker</c>، <c>purchase.cat:{Id}</c>).</summary>
    public string KeyOf(CostCategory category) => RugFieldKeys.ForPurchaseCategory(category.Id, category.SeedKey);

    /// <summary>بارگذاری برای یک کارگاه.</summary>
    public static async Task<RugGridCatalog> LoadAsync(
        Guid tenantId,
        IRepository<CustomFieldDefinition> customFields,
        IRepository<CostCategory> categories,
        CancellationToken cancellationToken)
    {
        var custom = await customFields.ListAsync(
            f => f.TenantId == tenantId && f.IsActive && f.Scope == CustomFieldScope.Rug, cancellationToken);
        var purchase = await categories.ListAsync(
            c => c.TenantId == tenantId && c.Group == CostGroup.Purchase && c.IsActive && c.ShowInIntake, cancellationToken);

        return new RugGridCatalog(
            custom.OrderBy(f => f.GridOrder ?? int.MaxValue).ThenBy(f => f.SortOrder).ThenBy(f => f.Label, StringComparer.Ordinal).ToList(),
            purchase.OrderBy(c => c.SortOrder).ThenBy(c => c.Name, StringComparer.Ordinal).ThenBy(c => c.Id).ToList());
    }

    /// <summary>نوع خرید یک کلید جدول؛ null اگر این کلید ستون خرید نیست.</summary>
    public CostCategory? PurchaseCategoryOf(string key)
        => PurchaseCategories.FirstOrDefault(c => KeyOf(c) == key);
}

/// <summary>
/// سازندهٔ ستون‌های فرشی جدول‌ها (G.2): برچسب‌ها واژه‌نامهٔ بازرگان را دنبال می‌کنند و پهنا به <c>ch</c> است. خانهٔ
/// فقط‌خواندنی هرگز ایستگاه Tab نیست.
///
/// پهنای ستون‌های پولی از آزمون در مرورگر آمده: با ۱۸ ستون «قیمت خرید» کنار چیپ «چند سطر» عدد ۳۵٬۰۰۰٬۰۰۰ را از سر
/// می‌برید و «٬۰۰۰٬۰۰۰» نشان می‌داد (عدد کوچک‌تری خوانده می‌شد)، و «جمع خرید» در بیشتر ردیف‌ها حاصل را پنهان می‌کرد.
/// </summary>
public static class RugGridColumns
{
    /// <summary>واحدهای خانهٔ «مبلغ + واحد» به ترتیب G.2 («هر متر | هر تخته | کل»)؛ نام enum، چون نویسنده نام را هم می‌پذیرد.</summary>
    private static readonly CostBasis[] UnitOrder = [CostBasis.PerM2, CostBasis.PerPiece, CostBasis.Total];

    public static GridColumnDto Serial() => new(RugFieldKeys.Serial, "شماره", GridColumnKind.Serial, 14, false, false, false, null, false);

    public static GridColumnDto Seller() => new(GridKeys.Seller, "فروشنده", GridColumnKind.Combo, 16, true, false, false, null, false);

    public static GridColumnDto Quantity() => new(RugFieldKeys.Quantity, "تعداد", GridColumnKind.Int, 7, false, false, false, null, false);

    public static GridColumnDto Pattern() => new(RugFieldKeys.Pattern, "نقشه", GridColumnKind.Combo, 12, false, false, false, null, false);

    public static GridColumnDto Origin() => new(RugFieldKeys.Origin, "محل بافت", GridColumnKind.Combo, 12, true, false, false, null, false);

    public static GridColumnDto Material() => new(RugFieldKeys.Material, "مواد مصرفی", GridColumnKind.Combo, 12, true, false, false, null, false);

    public static GridColumnDto IntakeLength(bool required) =>
        new(RugFieldKeys.IntakeLength, "طول", GridColumnKind.Dim, 8, false, false, required, null, false);

    public static GridColumnDto IntakeWidth(bool required) =>
        new(RugFieldKeys.IntakeWidth, "عرض", GridColumnKind.Dim, 8, false, false, required, null, false);

    public static GridColumnDto FinalLength() =>
        new(RugFieldKeys.FinalLength, "طول نهایی", GridColumnKind.Dim, 9, false, false, false, null, false);

    public static GridColumnDto FinalWidth() =>
        new(RugFieldKeys.FinalWidth, "عرض نهایی", GridColumnKind.Dim, 9, false, false, false, null, false);

    /// <summary>«متراژ» (فقط‌خواندنی؛ برای دسته «۰٫۱۶ × ۵۰ = ۸٫۰۰» از Derived).</summary>
    public static GridColumnDto Area() =>
        new(RugFieldKeys.AreaM2, "متراژ", GridColumnKind.ReadOnly, 16, false, true, false, null, false, TabStop: false);

    /// <summary>«جمع خرید» (Business؛ فقط‌خواندنی).</summary>
    public static GridColumnDto PurchaseTotal() =>
        new(RugFieldKeys.PurchaseTotal, "جمع خرید", GridColumnKind.ReadOnly, 22, false, true, false, null, true, TabStop: false);

    public static GridColumnDto Notes() => new(RugFieldKeys.Notes, "یادداشت", GridColumnKind.Text, 24, false, false, false, null, false);

    /// <summary>یک فیلد سفارشی؛ گزینه‌ها برای «انتخابی»، بقیه پیشنهاد مقدار (combo) یا متن.</summary>
    public static GridColumnDto Custom(CustomFieldDefinition definition, bool sticky)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var (type, options) = definition.FieldType switch
        {
            CustomFieldType.Select => (GridColumnKind.Select, ParseOptions(definition.OptionsJson)),
            CustomFieldType.Boolean => (GridColumnKind.Select, (IReadOnlyList<string>?)["true", "false"]),
            CustomFieldType.Number or CustomFieldType.Date => (GridColumnKind.Text, null),
            _ => (GridColumnKind.Combo, null)
        };
        return new GridColumnDto(RugFieldKeys.ForCustomField(definition.Key), definition.Label, type, 12, sticky, false,
            definition.IsRequired, options, false);
    }

    /// <summary>
    /// یک نوع خرید: درصدی «percent»، بقیه «money+unit» با چیپ واحد که پیش‌فرضش مبنای پیش‌فرض نوع است
    /// (<see cref="GridColumnDto.Options"/>: نام مبناها، پیش‌فرض اول). «قیمت خرید» و «دلالی» چسبان‌اند (G.2).
    /// </summary>
    public static GridColumnDto Purchase(CostCategory category, string key)
    {
        ArgumentNullException.ThrowIfNull(category);
        var sticky = key is "purchase.price" or "purchase.broker";
        if (category.DefaultBasis == CostBasis.Percent)
            return new GridColumnDto(key, category.Name, GridColumnKind.Percent, 8, sticky, false, false, null, true);

        var first = UnitOrder.Contains(category.DefaultBasis) ? category.DefaultBasis : CostBasis.PerM2;
        var options = new[] { first }.Concat(UnitOrder.Where(b => b != first)).Select(b => b.ToString()).ToList();
        return new GridColumnDto(key, category.Name, GridColumnKind.MoneyUnit, 21, sticky, false, false, options, true);
    }

    /// <summary>ستون‌های خرید یک کارگاه به ترتیب نوع‌ها؛ برای غیر Business هیچ.</summary>
    public static IEnumerable<GridColumnDto> PurchaseColumns(RugGridCatalog catalog, bool canSeeMoney)
        => canSeeMoney ? catalog.PurchaseCategories.Select(c => Purchase(c, catalog.KeyOf(c))) : [];

    /// <summary>ستون رنگ متن و رنگ حاشیه (اگر فیلدشان در کارگاه هست)، چسبان.</summary>
    public static IEnumerable<GridColumnDto> ColorColumns(RugGridCatalog catalog)
    {
        foreach (var key in new[] { GridKeys.FieldColorKey, GridKeys.BorderColorKey })
        {
            var definition = catalog.CustomFields.FirstOrDefault(f => f.Key == key);
            if (definition is not null)
                yield return Custom(definition, sticky: true);
        }
    }

    /// <summary>فیلدهای سفارشی دیگری که «ستون در جدول» دارند (به‌جز دو رنگ).</summary>
    public static IEnumerable<GridColumnDto> OtherCustomColumns(RugGridCatalog catalog)
        => catalog.CustomFields
            .Where(f => f.ShowInGrid && f.Key is not GridKeys.FieldColorKey and not GridKeys.BorderColorKey)
            .Select(f => Custom(f, sticky: false));

    private static IReadOnlyList<string>? ParseOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(optionsJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
