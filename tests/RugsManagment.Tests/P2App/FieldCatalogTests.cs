using RugsManagment.Application.Fields;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using static RugsManagment.Tests.P2App.P2Data;

namespace RugsManagment.Tests.P2App;

/// <summary>
/// کاتالوگ فیلدها (spec E.3): فیلدهای فرش و فیلدهای سفارشی کارگاه، مبلغ‌ها فقط برای Business، سند فقط فیلد سفارشیِ
/// «خروجی‌پذیر»، و کلید تکراری را contributor زودتر نگه می‌دارد.
/// </summary>
public class FieldCatalogTests
{
    private readonly P2MemoryRepository<CustomFieldDefinition> _fields = new(
        new CustomFieldDefinition { TenantId = TenantId, Key = "bg_color", Label = "رنگ متن", GridOrder = 2 },
        new CustomFieldDefinition { TenantId = TenantId, Key = "border_color", Label = "رنگ حاشیه", GridOrder = 1, Exportable = false },
        new CustomFieldDefinition { TenantId = TenantId, Key = "grade", Label = "درجه", FieldType = CustomFieldType.Select },
        new CustomFieldDefinition { TenantId = TenantId, Key = "gone", Label = "حذف‌شده", IsActive = false },
        new CustomFieldDefinition { TenantId = Guid.NewGuid(), Key = "other_tenant", Label = "کارگاه دیگر" });

    private FieldCatalog Catalog(bool business, params IFieldCatalogContributor[] extra)
        => new([new RugFieldContributor(_fields), new RugMoneyFieldContributor(), .. extra], business ? P2Money.Business : P2Money.Operator);

    [Fact]
    public async Task Grid_HasTheRugFields_AndOnlyThisTenantsActiveCustomFields()
    {
        var keys = (await Catalog(business: true).ListAsync(TenantId, FieldPurpose.Grid, null)).Select(f => f.Key).ToList();

        Assert.Equal(RugFieldKeys.Serial, keys[0]);
        Assert.Contains(RugFieldKeys.Quantity, keys);
        Assert.Contains(RugFieldKeys.IntakeLength, keys);
        Assert.Contains("rug.custom.bg_color", keys);
        Assert.Contains("rug.custom.grade", keys);
        Assert.DoesNotContain("rug.custom.gone", keys);
        Assert.DoesNotContain("rug.custom.other_tenant", keys);
        // ترتیب ستون جدول: GridOrder
        Assert.True(keys.IndexOf("rug.custom.border_color") < keys.IndexOf("rug.custom.bg_color"));
    }

    [Fact]
    public async Task MoneyFields_OnlyForBusiness()
    {
        var business = await Catalog(business: true).ListAsync(TenantId, FieldPurpose.Grid, null);
        var op = await Catalog(business: false).ListAsync(TenantId, FieldPurpose.Grid, null);

        Assert.Contains(business, f => f.Key == RugFieldKeys.TotalCost && f.Money);
        Assert.DoesNotContain(op, f => f.Money);
        Assert.Contains(op, f => f.Key == RugFieldKeys.Serial);
    }

    [Fact]
    public async Task Document_SkipsNonExportableCustomFields()
    {
        var keys = (await Catalog(business: true).ListAsync(TenantId, FieldPurpose.Document, null)).Select(f => f.Key).ToList();
        Assert.Contains("rug.custom.bg_color", keys);
        Assert.DoesNotContain("rug.custom.border_color", keys);
    }

    [Fact]
    public async Task DuplicateKey_KeepsTheEarlierContributor()
    {
        var fields = await Catalog(business: true, new LateDuplicate()).ListAsync(TenantId, FieldPurpose.Grid, null);
        var serial = Assert.Single(fields, f => f.Key == RugFieldKeys.Serial);
        Assert.Equal("شماره", serial.Label);
        Assert.Contains(fields, f => f.Key == "line.area");
    }

    [Fact]
    public void CustomField_DataTypes()
    {
        Assert.Equal(FieldDataType.Combo, RugFieldContributor.ToField(new CustomFieldDefinition { Key = "a", FieldType = CustomFieldType.Text }).DataType);
        Assert.Equal(FieldDataType.Select, RugFieldContributor.ToField(new CustomFieldDefinition { Key = "a", FieldType = CustomFieldType.Select }).DataType);
        Assert.Equal(FieldDataType.Date, RugFieldContributor.ToField(new CustomFieldDefinition { Key = "a", FieldType = CustomFieldType.Date }).DataType);
        Assert.Equal("rug.custom.a", RugFieldContributor.ToField(new CustomFieldDefinition { Key = "a" }).Key);
    }

    [Fact]
    public void MoneyKeys_IncludeEveryPurchaseCell()
    {
        Assert.True(RugFieldKeys.IsMoney("purchase.price"));
        Assert.True(RugFieldKeys.IsMoney("purchase.cat:" + Guid.NewGuid()));
        Assert.True(RugFieldKeys.IsMoney(RugFieldKeys.TargetSalePrice));
        Assert.False(RugFieldKeys.IsMoney(RugFieldKeys.Serial));
        Assert.Equal("purchase.price", RugFieldKeys.ForPurchaseCategory(Guid.NewGuid(), "purchase.price"));
        var id = Guid.NewGuid();
        Assert.Equal("purchase.cat:" + id.ToString("D"), RugFieldKeys.ForPurchaseCategory(id, null));
    }

    private sealed class LateDuplicate : IFieldCatalogContributor
    {
        public int Order => 900;

        public Task<IReadOnlyList<FieldDefDto>> FieldsAsync(FieldCatalogRequest request, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<FieldDefDto>>(
            [
                new(RugFieldKeys.Serial, "دیگر", FieldScope.Rug, FieldDataType.Text, "", false, false, false),
                new("line.area", "متراژ خط", FieldScope.Line, FieldDataType.Area, "0.00", true, false, false)
            ]);
    }
}
