using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.IntegrationTests.Support;

namespace RugsManagment.IntegrationTests.Ledger;

/// <summary>
/// پرس‌وجوهای فرش نسخهٔ ۲ (spec I.2، I.3، G.4، C.10، E17): فهرست با بهای <c>RugCostTotal</c>، جست‌وجو و فیلتر و مرتب‌سازی
/// شماره، جای فرش، شمارهٔ بعدی، یافتن با شماره (دقیق/داخل دسته)، پیشنهاد مقدار (فیلد سفارشی با کلید پارامتری)، سطل زباله،
/// و ماندهٔ طرف خدمات که فقط مراحل همین کارگاه را می‌شمارد.
/// </summary>
public sealed class RugRepositoryV2Tests(LedgerFixture fixture) : IClassFixture<LedgerFixture>
{
    private async Task<(Guid Tenant, Rug Single, Rug Lot, Rug Text, Rug Unnumbered)> SeedAsync()
    {
        var tenant = await fixture.NewTenantAsync();
        var single = LedgerFixture.NewRug(tenant, 2.08m, 2.10m, "3778");
        single.Origin = "گبه";
        LedgerFixture.Line(single, CostBasis.Total, 1_000m);
        var lot = LedgerFixture.NewRug(tenant, 0.40m, 0.40m, "3911 - 3960", 50);
        lot.Origin = "گبه";
        lot.MetadataJson = """{"bg_color":"لاکی"}""";
        LedgerFixture.Line(lot, CostBasis.PerPiece, 10m);
        var text = LedgerFixture.NewRug(tenant, 1m, 1m, "A-12");
        text.Origin = "كاشان"; // کاف عربی
        text.MetadataJson = """{"bg_color":"لاکی"}""";
        var unnumbered = LedgerFixture.NewRug(tenant, 1m, 1m);
        await fixture.SaveNewAsync(tenant, single, lot, text, unnumbered);
        return (tenant, single, lot, text, unnumbered);
    }

    private async Task<T> WithRugsAsync<T>(Guid tenant, Func<IRugRepository, Task<T>> work)
    {
        await using var scope = fixture.Scope(tenant);
        return await work(scope.ServiceProvider.GetRequiredService<IRugRepository>());
    }

    [PostgresFact]
    public async Task ListRow_ReadsTheStoredTotal_AreaAndVersion()
    {
        var (tenant, single, lot, _, unnumbered) = await SeedAsync();
        var page = await WithRugsAsync(tenant, r => r.SearchAsync(tenant, new RugQuery { SortBy = RugSortBy.Serial, Descending = false }));

        var lotRow = page.Items.Single(i => i.Id == lot.Id);
        Assert.Equal(500m, lotRow.TotalInvestment);   // ۱۰ × ۵۰
        Assert.True(lotRow.CostComputed);
        Assert.Equal(63m, lotRow.CostPerM2);          // ROUND(۵۰۰ ÷ ۸٫۰۰، ۰) = ۶۲٫۵ → ۶۳ (نصفه دور از صفر)
        Assert.Equal((0.16m, 8.00m, 50), (lotRow.AreaSquareMeters, lotRow.TotalAreaM2, lotRow.Quantity));
        Assert.Equal("3911 - 3960", lotRow.SerialNo);
        Assert.Equal(await fixture.RugVersionAsync(lot.Id), lotRow.Version);
        Assert.Equal(RugLocation.InStock, lotRow.Location);

        // مرتب‌سازی شماره: عددی به ترتیب اولِ بازه، بعد متنی، بعد بی‌شماره
        Assert.Equal([single.Id, lot.Id], page.Items.Take(2).Select(i => i.Id));
        Assert.Equal("A-12", page.Items[2].SerialNo);
        Assert.Equal(unnumbered.Id, page.Items[3].Id);

        var descending = await WithRugsAsync(tenant, r => r.SearchAsync(tenant, new RugQuery { SortBy = RugSortBy.Serial, Descending = true }));
        Assert.Equal([lot.Id, single.Id], descending.Items.Take(2).Select(i => i.Id));
        Assert.Equal(unnumbered.Id, descending.Items[3].Id);

        var byCost = await WithRugsAsync(tenant, r => r.SearchAsync(tenant, new RugQuery { SortBy = RugSortBy.TotalCost, Descending = true }));
        Assert.Equal(single.Id, byCost.Items[0].Id);
    }

    [PostgresFact]
    public async Task SerialFilter_IsExactOrInRange_AndSearchFindsSerials()
    {
        var (tenant, single, lot, text, _) = await SeedAsync();

        async Task<IReadOnlyList<Guid>> Find(RugQuery query)
            => (await WithRugsAsync(tenant, r => r.SearchAsync(tenant, query))).Items.Select(i => i.Id).ToList();

        Assert.Equal([lot.Id], await Find(new RugQuery { Serial = "۳۹۲۵" }));         // عدد داخل دسته
        Assert.Equal([single.Id], await Find(new RugQuery { Serial = "3778" }));
        Assert.Equal([lot.Id], await Find(new RugQuery { Serial = "3950 - 3999" }));  // همپوشانی بازه
        Assert.Equal([text.Id], await Find(new RugQuery { Serial = "A-12" }));
        Assert.Empty(await Find(new RugQuery { Serial = "3960-3911" }));             // نامعتبر: هیچ، نه خطا
        Assert.Equal([single.Id], await Find(new RugQuery { Search = "۳۷۷۸" }));      // جست‌وجو هم شماره را می‌گردد
        Assert.Contains(text.Id, await Find(new RugQuery { Search = "کاشان" }));      // ی/ک عربی یکسان
    }

    [PostgresFact]
    public async Task LocationAndShipmentFilters_BeforeShipmentsExist()
    {
        var (tenant, single, _, _, _) = await SeedAsync();
        await using (var db = fixture.CreateContext())
        {
            db.RugSales.Add(new RugSale { TenantId = tenant, RugId = single.Id, BuyerName = "x", SalePrice = 1m, ReceivedAmount = 1m });
            await db.SaveChangesAsync();
        }

        async Task<int> Count(RugQuery query) => (await WithRugsAsync(tenant, r => r.SearchAsync(tenant, query))).TotalCount;

        Assert.Equal(1, await Count(new RugQuery { Location = RugLocation.Sold }));
        Assert.Equal(3, await Count(new RugQuery { Location = RugLocation.InStock }));
        Assert.Equal(0, await Count(new RugQuery { Location = RugLocation.InBale }));
        Assert.Equal(0, await Count(new RugQuery { Location = RugLocation.Trashed }));
        Assert.Equal(0, await Count(new RugQuery { ShipmentId = Guid.NewGuid() }));
        Assert.Equal(4, await Count(new RugQuery { NotBaled = true }));

        var sold = (await WithRugsAsync(tenant, r => r.SearchAsync(tenant, new RugQuery { Location = RugLocation.Sold }))).Items.Single();
        Assert.Equal(RugLocation.Sold, sold.Location);
    }

    [PostgresFact]
    public async Task MaxSerial_AndFindBySerial()
    {
        var (tenant, single, lot, text, _) = await SeedAsync();

        Assert.Equal(3960, await WithRugsAsync(tenant, r => r.GetMaxSerialAsync(tenant)));

        var exact = await WithRugsAsync(tenant, r => r.FindBySerialAsync(tenant, SerialNumber.Parse("03778")!));
        Assert.Equal((single.Id, SerialMatchType.Exact), (exact!.Rug.Id, exact.MatchType));

        var inRange = await WithRugsAsync(tenant, r => r.FindBySerialAsync(tenant, SerialNumber.Parse("3925")!));
        Assert.Equal((lot.Id, SerialMatchType.InRange, RugLocation.InStock), (inRange!.Rug.Id, inRange.MatchType, inRange.Location));

        var range = await WithRugsAsync(tenant, r => r.FindBySerialAsync(tenant, SerialNumber.Parse("3911 - 3960")!));
        Assert.Equal(SerialMatchType.Exact, range!.MatchType);

        Assert.Null(await WithRugsAsync(tenant, r => r.FindBySerialAsync(tenant, SerialNumber.Parse("3920 - 3930")!)));
        Assert.Equal(text.Id, (await WithRugsAsync(tenant, r => r.FindBySerialAsync(tenant, SerialNumber.Parse("A-12")!)))!.Rug.Id);

        // کارگاه دیگر چیزی نمی‌بیند
        var other = await fixture.NewTenantAsync();
        Assert.Null(await WithRugsAsync(other, r => r.FindBySerialAsync(other, SerialNumber.Parse("3778")!)));
        Assert.Null(await WithRugsAsync(other, r => r.GetMaxSerialAsync(other)));
    }

    [PostgresFact]
    public async Task DistinctValues_MostFrequentFirst_CustomKeyAsAParameter()
    {
        var (tenant, _, _, _, _) = await SeedAsync();

        var origins = await WithRugsAsync(tenant, r => r.ListDistinctValuesAsync(tenant, new RugValuesQuery { Column = RugValueColumn.Origin }));
        Assert.Equal(["گبه", "كاشان"], origins);

        var filtered = await WithRugsAsync(tenant, r => r.ListDistinctValuesAsync(tenant,
            new RugValuesQuery { Column = RugValueColumn.Origin, Search = "کاش" }));
        Assert.Equal(["كاشان"], filtered);

        Assert.True(RugValuesQuery.TryFromFieldKey("rug.custom.bg_color", null, out var custom));
        Assert.Equal(["لاکی"], await WithRugsAsync(tenant, r => r.ListDistinctValuesAsync(tenant, custom)));

        // کلیدی که تزریق SQL می‌خواهد فقط فهرست خالی است
        var injected = new RugValuesQuery { Column = RugValueColumn.Custom, CustomKey = "x'); DROP TABLE \"Rugs\"; --" };
        Assert.Empty(await WithRugsAsync(tenant, r => r.ListDistinctValuesAsync(tenant, injected)));
    }

    [PostgresFact]
    public async Task Trash_ListsTheStoredTotalAndSerial()
    {
        var (tenant, single, _, _, _) = await SeedAsync();
        await using (var scope = fixture.Scope(tenant))
        {
            var rugs = scope.ServiceProvider.GetRequiredService<IRugRepository>();
            var loaded = (await rugs.GetWithWorkflowAsync(single.Id, tenant))!;
            loaded.DeletedAt = DateTimeOffset.UtcNow;
            await scope.ServiceProvider.GetRequiredService<Application.Abstractions.IUnitOfWork>().SaveChangesAsync();
        }

        var trash = await WithRugsAsync(tenant, r => r.ListDeletedAsync(tenant));
        var row = Assert.Single(trash);
        Assert.Equal((1_000m, "3778", 1), (row.TotalInvestment, row.SerialNo, row.Quantity));
    }

    [PostgresFact]
    public async Task ProviderBalances_CountOnlyThisTenantsSteps_E17()
    {
        var tenantA = await fixture.NewTenantAsync();
        var tenantB = await fixture.NewTenantAsync();
        var providerB = new Domain.Entities.ServiceProvider { TenantId = tenantB, Name = "B" };
        await using (var db = fixture.CreateContext())
        {
            db.ServiceProviders.Add(providerB);
            await db.SaveChangesAsync();
        }

        // مرحلهٔ فرشِ کارگاه A که (از پیش از رفع نشت) شناسهٔ طرفِ کارگاه B را دارد
        var rugA = LedgerFixture.NewRug(tenantA, 1m, 1m);
        rugA.WorkflowSteps.Add(new RugWorkflowStep
        {
            RugId = rugA.Id, ProcessStepTypeId = fixture.WashTypeId, Status = WorkflowStepStatus.Completed,
            CalculatedCost = 900m, ServiceProviderId = providerB.Id
        });
        var rugB = LedgerFixture.NewRug(tenantB, 1m, 1m);
        rugB.WorkflowSteps.Add(new RugWorkflowStep
        {
            RugId = rugB.Id, ProcessStepTypeId = fixture.WashTypeId, Status = WorkflowStepStatus.Completed,
            CalculatedCost = 100m, Adjustment = -150m, ServiceProviderId = providerB.Id
        });
        rugB.WorkflowSteps.Add(new RugWorkflowStep
        {
            RugId = rugB.Id, ProcessStepTypeId = fixture.WashTypeId, OrderIndex = 1, Status = WorkflowStepStatus.InProgress,
            CalculatedCost = 40m, ServiceProviderId = providerB.Id
        });
        await fixture.SaveNewAsync(tenantA, rugA);
        await fixture.SaveNewAsync(tenantB, rugB);

        await using var scope = fixture.Scope(tenantB);
        var providers = scope.ServiceProvider.GetRequiredService<IServiceProviderRepository>();
        var balance = (await providers.ListBalancesAsync(tenantB)).Single();
        Assert.Equal((0m, 40m, 1, 1), (balance.CompletedWorkTotal, balance.InProgressTotal, balance.CompletedStepCount, balance.InProgressStepCount));

        var work = await providers.ListWorkAsync(tenantB, providerB.Id);
        Assert.Equal(2, work.Count);
        Assert.All(work, w => Assert.Equal(rugB.Id, w.RugId));
        Assert.Equal([0m, 40m], work.Select(w => w.Cost).Order());
    }
}
