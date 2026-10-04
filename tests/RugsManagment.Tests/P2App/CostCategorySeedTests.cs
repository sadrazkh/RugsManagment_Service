using RugsManagment.Application.Seed;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using static RugsManagment.Tests.P2App.P2Data;

namespace RugsManagment.Tests.P2App;

/// <summary>
/// سه نوع خرید پایه (K.2 فاز ۲، H.8): یک بار و با SeedKey؛ اجرای دوباره چیزی اضافه نمی‌کند و نام عوض‌شده را دست نمی‌زند؛
/// سطرهای «قیمت خرید» بی‌نوعِ M2 به <c>purchase.price</c> وصل می‌شوند (G.2: سلول «قیمت خرید» فرش قدیمی «کل …» است).
/// </summary>
public class CostCategorySeedTests
{
    [Fact]
    public async Task Seeds_TheThreePurchaseCategories_Once()
    {
        var categories = new P2MemoryRepository<CostCategory>();
        var contributor = new CostCategorySeedContributor(categories, new P2MemoryRepository<RugPurchaseLine>());

        await contributor.SeedAsync(TenantId, CancellationToken.None);
        await contributor.SeedAsync(TenantId, CancellationToken.None);

        Assert.Equal(3, categories.Items.Count);
        Assert.All(categories.Items.Values, c => Assert.Equal(CostGroup.Purchase, c.Group));

        var price = categories.Items.Values.Single(c => c.SeedKey == CostCategorySeedContributor.PriceKey);
        Assert.Equal((CostBasis.PerM2, true), (price.DefaultBasis, price.ShowInIntake));
        var broker = categories.Items.Values.Single(c => c.SeedKey == CostCategorySeedContributor.BrokerKey);
        Assert.Equal((CostBasis.Percent, 2m, true), (broker.DefaultBasis, broker.DefaultRate!.Value, broker.ShowInIntake));
        var freight = categories.Items.Values.Single(c => c.SeedKey == CostCategorySeedContributor.FreightInKey);
        Assert.Equal((CostBasis.PerPiece, false), (freight.DefaultBasis, freight.ShowInIntake));
        Assert.True(contributor.Order > 100);
    }

    [Fact]
    public async Task ExistingCategory_IsNotOverwritten()
    {
        var renamed = CostCategorySeedContributor.Defaults(TenantId)[0];
        renamed.Name = "خرید";
        var categories = new P2MemoryRepository<CostCategory>(renamed);

        await new CostCategorySeedContributor(categories, new P2MemoryRepository<RugPurchaseLine>()).SeedAsync(TenantId, CancellationToken.None);

        Assert.Equal(3, categories.Items.Count);
        Assert.Equal("خرید", categories.Items[renamed.Id].Name);
    }

    [Fact]
    public async Task LegacyM2Lines_AreLinkedToThePriceCategory()
    {
        var rugId = Guid.NewGuid();
        var legacy = new RugPurchaseLine
        {
            TenantId = TenantId, RugId = rugId, Label = RugPurchaseLine.LegacyPurchaseLabel, Basis = CostBasis.Total, Rate = 40_000_000m
        };
        var free = new RugPurchaseLine { TenantId = TenantId, RugId = rugId, Label = "کرایه", Basis = CostBasis.Total, Rate = 1m };
        var otherTenant = new RugPurchaseLine
        {
            TenantId = Guid.NewGuid(), RugId = Guid.NewGuid(), Label = RugPurchaseLine.LegacyPurchaseLabel, Rate = 1m
        };
        var lines = new P2MemoryRepository<RugPurchaseLine>(legacy, free, otherTenant);
        var categories = new P2MemoryRepository<CostCategory>();

        await new CostCategorySeedContributor(categories, lines).SeedAsync(TenantId, CancellationToken.None);

        var price = categories.Items.Values.Single(c => c.SeedKey == CostCategorySeedContributor.PriceKey);
        Assert.Equal(price.Id, legacy.CategoryId);
        Assert.Equal((CostBasis.Total, 40_000_000m), (legacy.Basis, legacy.Rate)); // فقط نوع عوض می‌شود
        Assert.Null(free.CategoryId);
        Assert.Null(otherTenant.CategoryId);
    }

    [Fact]
    public void SeedIcons_ExistInTheSprite()
    {
        var root = Fixtures.RepositoryRoot();
        if (root is null) return;
        var sprite = File.ReadAllText(Path.Combine(root.FullName, "src", "RugsManagment.Web", "wwwroot", "icons.svg"));
        foreach (var icon in CostCategorySeedContributor.Defaults(TenantId).Select(c => c.Icon!).Distinct())
            Assert.Contains($"id=\"{icon}\"", sprite);
    }
}
