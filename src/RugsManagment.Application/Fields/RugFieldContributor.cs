using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Fields;

/// <summary>
/// فیلدهای خودِ فرش برای کاتالوگ (دامنهٔ <c>rug</c>، E.3): شماره، تعداد، مشخصات، اندازهٔ اول و نهایی، متراژها،
/// یادداشت و فیلدهای سفارشی فعال کارگاه.
///
/// برچسب‌ها واژه‌نامهٔ بازرگان را دنبال می‌کنند (A.2، G.2): «شماره»، «تعداد»، «محل بافت»، «نقشه»، «مواد مصرفی»، «طول»
/// و «عرض» اندازهٔ اول؛ اندازهٔ نهایی جدا برچسب می‌خورد چون فقط در نگله‌بندی نوشته می‌شود. فیلد سفارشی در کاتالوگ
/// سند فقط وقتی می‌آید که «خروجی‌پذیر» باشد (B.3).
/// </summary>
public sealed class RugFieldContributor(IRepository<CustomFieldDefinition> customFields) : IFieldCatalogContributor
{
    /// <summary>پیش از هر contributor دیگری، تا کلیدهای فرش معنای اصلی را داشته باشند.</summary>
    public int Order => 100;

    /// <summary>فیلدهای ثابت فرش، به ترتیب ستون‌های جدول ورود (G.2).</summary>
    public static IReadOnlyList<FieldDefDto> StaticFields { get; } =
    [
        new(RugFieldKeys.Serial, "شماره", FieldScope.Rug, FieldDataType.Serial, "", false, false, true),
        new(RugFieldKeys.Quantity, "تعداد", FieldScope.Rug, FieldDataType.Int, "0", true, false, true),
        new(RugFieldKeys.Title, "عنوان", FieldScope.Rug, FieldDataType.Combo, "", false, false, true),
        new(RugFieldKeys.Pattern, "نقشه", FieldScope.Rug, FieldDataType.Combo, "", false, false, true),
        new(RugFieldKeys.Origin, "محل بافت", FieldScope.Rug, FieldDataType.Combo, "", false, false, true),
        new(RugFieldKeys.Material, "مواد مصرفی", FieldScope.Rug, FieldDataType.Combo, "", false, false, true),
        new(RugFieldKeys.KnotDensity, "رجشمار", FieldScope.Rug, FieldDataType.Int, "0", false, false, true),
        new(RugFieldKeys.IntakeLength, "طول", FieldScope.Rug, FieldDataType.Dim, "0.00", false, false, true),
        new(RugFieldKeys.IntakeWidth, "عرض", FieldScope.Rug, FieldDataType.Dim, "0.00", false, false, true),
        new(RugFieldKeys.FinalLength, "طول نهایی", FieldScope.Rug, FieldDataType.Dim, "0.00", false, false, true),
        new(RugFieldKeys.FinalWidth, "عرض نهایی", FieldScope.Rug, FieldDataType.Dim, "0.00", false, false, true),
        new(RugFieldKeys.AreaM2, "متراژ", FieldScope.Rug, FieldDataType.Area, "0.00", false, false, false),
        new(RugFieldKeys.TotalAreaM2, "متراژ کل", FieldScope.Rug, FieldDataType.Area, "0.00", true, false, false),
        new(RugFieldKeys.IntakeAreaM2, "متراژ اول", FieldScope.Rug, FieldDataType.Area, "0.00", false, false, false),
        new(RugFieldKeys.Sku, "کد", FieldScope.Rug, FieldDataType.Text, "", false, false, false),
        new(RugFieldKeys.Notes, "یادداشت", FieldScope.Rug, FieldDataType.Text, "", false, false, true)
    ];

    public async Task<IReadOnlyList<FieldDefDto>> FieldsAsync(FieldCatalogRequest request, CancellationToken cancellationToken)
    {
        var forDocument = request.Purpose == FieldPurpose.Document;
        var tenantId = request.TenantId;
        var custom = await customFields.ListAsync(
            f => f.TenantId == tenantId && f.IsActive && f.Scope == CustomFieldScope.Rug && (!forDocument || f.Exportable),
            cancellationToken);

        var result = new List<FieldDefDto>(StaticFields.Count + custom.Count);
        result.AddRange(StaticFields);
        result.AddRange(custom
            .OrderBy(f => f.GridOrder ?? int.MaxValue)
            .ThenBy(f => f.SortOrder)
            .ThenBy(f => f.Label, StringComparer.Ordinal)
            .Select(ToField));
        return result;
    }

    /// <summary>یک فیلد سفارشی به‌صورت فیلد کاتالوگ؛ متن آزاد «combo» است تا مقدارهای قبلی پیشنهاد شوند.</summary>
    public static FieldDefDto ToField(CustomFieldDefinition definition) => new(
        RugFieldKeys.ForCustomField(definition.Key),
        definition.Label,
        FieldScope.Rug,
        definition.FieldType switch
        {
            CustomFieldType.Number => FieldDataType.Number,
            CustomFieldType.Date => FieldDataType.Date,
            CustomFieldType.Select => FieldDataType.Select,
            CustomFieldType.Boolean => FieldDataType.Bool,
            _ => FieldDataType.Combo
        },
        definition.FieldType == CustomFieldType.Number ? "0.####" : "",
        false,
        false,
        true);
}
