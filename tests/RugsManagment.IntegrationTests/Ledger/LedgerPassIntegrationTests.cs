using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.DTOs.Sales;
using RugsManagment.Application.Ledger;
using RugsManagment.Application.Mapping;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.Infrastructure.Persistence;
using RugsManagment.IntegrationTests.Support;

namespace RugsManagment.IntegrationTests.Ledger;

/// <summary>
/// پاس سازگاری روی پستگرس واقعی (spec D.4، D.7، L.2): سطرهای خرید و جمع بهای هر فرش با یک ذخیره و در همان درخواست تازه‌اند؛
/// اندازهٔ اول/نهایی و تعداد درست پخش می‌شوند؛ نوشتن مرحله‌ها نسخهٔ فرش را عوض نمی‌کند؛ شمارهٔ هم‌پوشان ۴۰۹ با nextFree است؛
/// فروش تازه عکس بها می‌گیرد؛ رقم اعشار بعد از اولین مبلغ قفل است؛ تراکنش بیرونی و savepoint؛ و سنجش/بازسازی.
/// </summary>
public sealed class LedgerPassIntegrationTests(LedgerFixture fixture) : IClassFixture<LedgerFixture>
{
    // ── D.7 #1–#3 و «یک درخواست، عددهای تازه» ──

    [PostgresFact]
    public async Task WorkedExample_PurchaseLinesAndTotals_AreWrittenByThePass()
    {
        var tenant = await fixture.NewTenantAsync();
        var a = LedgerFixture.NewRug(tenant, 2.08m, 2.10m, "3778");
        LedgerFixture.Line(a, CostBasis.PerM2, 9_000_000m, 0, "قیمت خرید");
        LedgerFixture.Line(a, CostBasis.Percent, 2m, 1, "دلالی");
        var b = LedgerFixture.NewRug(tenant, 0.40m, 0.40m, "3911 - 3960", 50);
        LedgerFixture.Line(b, CostBasis.PerPiece, 300_000m, 0, "قیمت خرید");
        LedgerFixture.Line(b, CostBasis.Percent, 2m, 1, "دلالی");
        var c = LedgerFixture.NewRug(tenant, 3.35m, 2.52m, "3782");
        LedgerFixture.Line(c, CostBasis.PerM2, 8_500_000m, 0, "قیمت خرید");
        LedgerFixture.Line(c, CostBasis.Percent, 2m, 1, "دلالی");

        await fixture.SaveNewAsync(tenant, a, b, c);

        Assert.Equal([39_330_000m, 786_600m], await fixture.LineAmountsAsync(a.Id));
        Assert.Equal([15_000_000m, 300_000m], await fixture.LineAmountsAsync(b.Id));
        Assert.Equal([71_740_000m, 1_434_800m], await fixture.LineAmountsAsync(c.Id));

        var totalA = (await fixture.TotalAsync(a.Id))!;
        var totalB = (await fixture.TotalAsync(b.Id))!;
        var totalC = (await fixture.TotalAsync(c.Id))!;
        Assert.Equal((40_116_600m, 40_116_600m), (totalA.PurchaseCost, totalA.Total));
        Assert.Equal((15_300_000m, 15_300_000m), (totalB.PurchaseCost, totalB.Total));
        Assert.Equal((73_174_800m, 73_174_800m), (totalC.PurchaseCost, totalC.Total));
        Assert.Equal(1_912_500m, totalB.CostPerM2);  // ÷ ۸٫۰۰ م² (۰٫۱۶ × ۵۰)
        Assert.Equal(306_000m, totalB.CostPerPiece); // ÷ ۵۰
        Assert.Equal(LedgerVersion.Current, totalB.LedgerVersion);

        await using var db = fixture.CreateContext();
        var areas = await db.Rugs.AsNoTracking().Where(r => r.TenantId == tenant)
            .Select(r => new { r.SerialNo, r.AreaM2, r.TotalAreaM2 }).ToListAsync();
        Assert.Equal(4.37m, areas.Single(r => r.SerialNo == "3778").AreaM2);
        Assert.Equal((0.16m, 8.00m), areas.Where(r => r.SerialNo == "3911 - 3960").Select(r => (r.AreaM2, r.TotalAreaM2)).Single());
        Assert.Equal(8.44m, areas.Single(r => r.SerialNo == "3782").AreaM2);
        Assert.Equal(20.81m, areas.Sum(r => r.TotalAreaM2));

        await AssertHealthyAsync(tenant);
    }

    [PostgresFact]
    public async Task IntakeSizeChange_ReturnsTheNewTotal_InTheSameRequest()
    {
        var tenant = await fixture.NewTenantAsync();
        var rug = LedgerFixture.NewRug(tenant, 2.08m, 2.10m, "100");
        LedgerFixture.Line(rug, CostBasis.PerM2, 9_000_000m, 0);
        LedgerFixture.Line(rug, CostBasis.Percent, 2m, 1);
        await fixture.SaveNewAsync(tenant, rug);

        await using (var scope = fixture.Scope(tenant))
        {
            var rugs = scope.ServiceProvider.GetRequiredService<IRugRepository>();
            var engine = scope.ServiceProvider.GetRequiredService<IWorkflowEngine>();
            var loaded = (await rugs.GetWithWorkflowAsync(rug.Id, tenant))!;
            Assert.Equal(40_116_600m, engine.CalculateRugCosts(loaded).TotalInvestment);

            loaded.SetIntakeSize(2.10m, 2.10m); // ۴٫۴۱ م²
            loaded.UpdatedAt = DateTimeOffset.UtcNow;
            rugs.Update(loaded);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

            // همان DbContext، بی بارگذاری دوباره: جمع و مبلغ سطرها تازه‌اند (D.4 گام ۸)
            var costs = engine.CalculateRugCosts(loaded);
            Assert.True(costs.Computed);
            Assert.Equal(40_483_800m, costs.TotalInvestment); // ۳۹٬۶۹۰٬۰۰۰ + ۷۹۳٬۸۰۰
            Assert.Equal([39_690_000m, 793_800m], loaded.PurchaseLines.OrderBy(l => l.SortOrder).Select(l => l.Amount));

            var dto = loaded.ToDto(engine);
            Assert.Equal(40_483_800m, dto.Costs!.TotalInvestment);
            Assert.Equal(40_483_800m, dto.PurchaseCost);
            Assert.Equal(await fixture.RugVersionAsync(rug.Id), dto.Version);
        }

        Assert.Equal(40_483_800m, (await fixture.TotalAsync(rug.Id))!.Total);
    }

    [PostgresFact]
    public async Task FinalSize_ChangesCostPerM2_ButNotThePerM2PurchaseLine()
    {
        // D.7 #7: قیمت خرید هر متر روی اندازهٔ اول است (M.2 #12)
        var tenant = await fixture.NewTenantAsync();
        var c = LedgerFixture.NewRug(tenant, 3.35m, 2.52m, "3782");
        LedgerFixture.Line(c, CostBasis.PerM2, 8_500_000m, 0);
        LedgerFixture.Line(c, CostBasis.Percent, 2m, 1);
        await fixture.SaveNewAsync(tenant, c);

        await using (var scope = fixture.Scope(tenant))
        {
            var rugs = scope.ServiceProvider.GetRequiredService<IRugRepository>();
            var loaded = (await rugs.GetWithWorkflowAsync(c.Id, tenant))!;
            loaded.SetFinalSize(3.30m, 2.50m, DateTimeOffset.UtcNow, null);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        var total = (await fixture.TotalAsync(c.Id))!;
        Assert.Equal(73_174_800m, total.Total);
        Assert.Equal(8_869_673m, total.CostPerM2); // ÷ ۸٫۲۵ م²
        Assert.Equal([71_740_000m, 1_434_800m], await fixture.LineAmountsAsync(c.Id));
        await AssertHealthyAsync(tenant);
    }

    [PostgresFact]
    public async Task Quantity_MovesPerPieceLines_AndTheTotalArea()
    {
        var tenant = await fixture.NewTenantAsync(moneyScale: 2);
        var lot = LedgerFixture.NewRug(tenant, 0.40m, 0.40m, "5000 - 5049", 50);
        LedgerFixture.Line(lot, CostBasis.PerM2, 45.55m, 0);
        await fixture.SaveNewAsync(tenant, lot);
        Assert.Equal([364.50m], await fixture.LineAmountsAsync(lot.Id)); // ۷٫۲۹ × ۵۰ (رقم اعشار ۲)

        await using (var scope = fixture.Scope(tenant))
        {
            var loaded = (await scope.ServiceProvider.GetRequiredService<IRugRepository>().GetWithWorkflowAsync(lot.Id, tenant))!;
            loaded.SetQuantity(30);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        Assert.Equal([218.70m], await fixture.LineAmountsAsync(lot.Id)); // ۷٫۲۹ × ۳۰
        var total = (await fixture.TotalAsync(lot.Id))!;
        Assert.Equal(218.70m, total.Total);
        Assert.Equal(45.56m, total.CostPerM2); // ۲۱۸٫۷۰ ÷ ۴٫۸۰
        Assert.Equal(7.29m, total.CostPerPiece);
    }

    [PostgresFact]
    public async Task StepCosts_CountOnlyCompletedAndInProgress_EachFlooredAtZero()
    {
        var tenant = await fixture.NewTenantAsync();
        var rug = LedgerFixture.NewRug(tenant, 2m, 3m);
        LedgerFixture.Line(rug, CostBasis.Total, 1_000_000m);
        rug.WorkflowSteps.Add(new RugWorkflowStep { RugId = rug.Id, ProcessStepTypeId = fixture.WashTypeId, OrderIndex = 0, Status = WorkflowStepStatus.Completed, CalculatedCost = 500_000m, Adjustment = -100_000m });
        rug.WorkflowSteps.Add(new RugWorkflowStep { RugId = rug.Id, ProcessStepTypeId = fixture.WashTypeId, OrderIndex = 1, Status = WorkflowStepStatus.InProgress, CalculatedCost = 999m, ManualCostOverride = 300_000m });
        rug.WorkflowSteps.Add(new RugWorkflowStep { RugId = rug.Id, ProcessStepTypeId = fixture.WashTypeId, OrderIndex = 2, Status = WorkflowStepStatus.Completed, CalculatedCost = 100m, Adjustment = -500m });
        rug.WorkflowSteps.Add(new RugWorkflowStep { RugId = rug.Id, ProcessStepTypeId = fixture.WashTypeId, OrderIndex = 3, Status = WorkflowStepStatus.Pending, CalculatedCost = 777_000m });
        await fixture.SaveNewAsync(tenant, rug);

        var total = (await fixture.TotalAsync(rug.Id))!;
        Assert.Equal(700_000m, total.StepCost);
        Assert.Equal(1_700_000m, total.Total);

        // پیش بردن مرحلهٔ در صف از مسیر موتور: هزینه‌اش (۸۵٬۰۰۰ × ۶ م²) در همان درخواست به جمع می‌رسد
        await using (var scope = fixture.Scope(tenant))
        {
            var rugs = scope.ServiceProvider.GetRequiredService<IRugRepository>();
            var engine = scope.ServiceProvider.GetRequiredService<IWorkflowEngine>();
            var loaded = (await rugs.GetWithWorkflowAsync(rug.Id, tenant))!;
            var pending = loaded.WorkflowSteps.Single(s => s.OrderIndex == 3);
            pending.CalculatedCost = null;
            await engine.AdvanceStepAsync(loaded, pending.Id, new AdvanceStepRequest(null, null, null, null, null, null, null));
            rugs.Update(loaded);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
            Assert.Equal(2_210_000m, engine.CalculateRugCosts(loaded).TotalInvestment);
            // عکس اندازهٔ قیمت‌گذاری (B.5): طول ۲، عرض ۳، یک تخته
            Assert.Equal((2m, 3m, 1), (pending.PricedLength, pending.PricedWidth, pending.PricedQuantity));
        }

        Assert.Equal(2_210_000m, (await fixture.TotalAsync(rug.Id))!.Total);
        await AssertHealthyAsync(tenant);
    }

    // ── نسخهٔ فرش ──

    [PostgresFact]
    public async Task StageWrites_NeverChangeTheRugVersion()
    {
        var tenant = await fixture.NewTenantAsync();
        var rug = LedgerFixture.NewRug(tenant, 2m, 3m);
        var line = LedgerFixture.Line(rug, CostBasis.PerM2, 1_000m);
        await fixture.SaveNewAsync(tenant, rug);
        var version = await fixture.RugVersionAsync(rug.Id);

        // فقط نرخ سطر: سطر و جمع عوض می‌شوند، ردیف فرش نه
        await using (var scope = fixture.Scope(tenant))
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tracked = await db.Set<RugPurchaseLine>().SingleAsync(l => l.Id == line.Id);
            tracked.Rate = 2_000m;
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
            Assert.Equal(12_000m, tracked.Amount); // تازه، بی بارگذاری دوباره
        }

        Assert.Equal(version, await fixture.RugVersionAsync(rug.Id));
        Assert.Equal(12_000m, (await fixture.TotalAsync(rug.Id))!.Total);
    }

    // ── شماره (E1، E2، G.4) ──

    [PostgresFact]
    public async Task OverlappingRange_Is409_WithNextFree_AndNothingIsSaved()
    {
        var tenant = await fixture.NewTenantAsync();
        await fixture.SaveNewAsync(tenant, LedgerFixture.NewRug(tenant, 0.40m, 0.40m, "3911 - 3960", 50));

        var overlapping = LedgerFixture.NewRug(tenant, 0.40m, 0.40m, "3950 - 3970", 21);
        var ex = await Assert.ThrowsAsync<SerialOverlapException>(() => fixture.SaveNewAsync(tenant, overlapping));

        Assert.Equal("rug.serial", ex.Field);
        Assert.Equal(3961, ex.NextFree);
        Assert.Contains("۳۹۵۰ - ۳۹۷۰", ex.Message);
        Assert.Contains("۳۹۱۱ - ۳۹۶۰", ex.Message);

        await using var db = fixture.CreateContext();
        Assert.False(await db.Rugs.IgnoreQueryFilters().AnyAsync(r => r.Id == overlapping.Id));
    }

    [PostgresFact]
    public async Task SameSerialText_FromTheUniqueIndex_Is409_WithNextFree()
    {
        var tenant = await fixture.NewTenantAsync(serialStart: 10);
        await fixture.SaveNewAsync(tenant, LedgerFixture.NewRug(tenant, 1m, 1m, "3985 - 4003", 19));

        var duplicate = LedgerFixture.NewRug(tenant, 1m, 1m, "3985 - 4003", 19);
        var ex = await Assert.ThrowsAsync<SerialOverlapException>(() => fixture.SaveNewAsync(tenant, duplicate));

        Assert.Equal(4004, ex.NextFree); // G.4: بعد از «3985 - 4003» ← ۴۰۰۴
    }

    [PostgresFact]
    public async Task SerialEdit_IsOverlapChecked_E1()
    {
        var tenant = await fixture.NewTenantAsync();
        var first = LedgerFixture.NewRug(tenant, 1m, 1m, "100 - 109", 10);
        var second = LedgerFixture.NewRug(tenant, 1m, 1m, "200");
        await fixture.SaveNewAsync(tenant, first, second);

        await using var scope = fixture.Scope(tenant);
        var loaded = (await scope.ServiceProvider.GetRequiredService<IRugRepository>().GetWithWorkflowAsync(second.Id, tenant))!;
        loaded.SetSerial(SerialNumber.Parse("105"));

        var ex = await Assert.ThrowsAsync<SerialOverlapException>(
            () => scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync());
        Assert.Equal(110, ex.NextFree);
    }

    [PostgresFact]
    public async Task RestoringARug_RechecksItsSerial()
    {
        // سطل زباله شماره را آزاد می‌کند؛ بازگردانی دوباره می‌سنجد (M.2 #4)
        var tenant = await fixture.NewTenantAsync();
        var trashed = LedgerFixture.NewRug(tenant, 1m, 1m, "777");
        await fixture.SaveNewAsync(tenant, trashed);

        await using (var scope = fixture.Scope(tenant))
        {
            var loaded = (await scope.ServiceProvider.GetRequiredService<IRugRepository>().GetWithWorkflowAsync(trashed.Id, tenant))!;
            loaded.DeletedAt = DateTimeOffset.UtcNow;
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        await fixture.SaveNewAsync(tenant, LedgerFixture.NewRug(tenant, 1m, 1m, "777"));

        await using (var scope = fixture.Scope(tenant))
        {
            var deleted = (await scope.ServiceProvider.GetRequiredService<IRugRepository>().GetDeletedAsync(trashed.Id, tenant))!;
            deleted.DeletedAt = null;
            await Assert.ThrowsAsync<SerialOverlapException>(
                () => scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync());
        }
    }

    [PostgresFact]
    public async Task LeadingZeros_OverlapTheSameNumber()
    {
        var tenant = await fixture.NewTenantAsync();
        await fixture.SaveNewAsync(tenant, LedgerFixture.NewRug(tenant, 1m, 1m, "123"));

        await Assert.ThrowsAsync<SerialOverlapException>(
            () => fixture.SaveNewAsync(tenant, LedgerFixture.NewRug(tenant, 1m, 1m, "0123")));
    }

    // ── فروش (D.6) ──

    [PostgresFact]
    public async Task NewSale_SnapshotsThePostChangeTotal_AndEditsKeepIt()
    {
        var tenant = await fixture.NewTenantAsync();
        var rug = LedgerFixture.NewRug(tenant, 2m, 3m);
        LedgerFixture.Line(rug, CostBasis.Total, 1_000_000m);
        await fixture.SaveNewAsync(tenant, rug);

        // فروش و تغییر هزینه در یک درخواست: عکس بها شامل همان تغییر است
        await using (var scope = fixture.Scope(tenant))
        {
            var loaded = (await scope.ServiceProvider.GetRequiredService<IRugRepository>().GetWithWorkflowAsync(rug.Id, tenant))!;
            loaded.PurchaseLines.Single().Rate = 1_200_000m;
            var sale = new RugSale { TenantId = tenant, RugId = rug.Id, BuyerName = "خریدار", SalePrice = 2_000_000m, ReceivedAmount = 2_000_000m };
            loaded.Sale = sale;
            await scope.ServiceProvider.GetRequiredService<IRepository<RugSale>>().AddAsync(sale);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

            Assert.Equal(1_200_000m, sale.CostAtSale); // ردیابی‌شده تازه شده
            var engine = scope.ServiceProvider.GetRequiredService<IWorkflowEngine>();
            Assert.Equal(800_000m, loaded.ToDto(engine).Costs!.ActualProfit);
        }

        // ویرایش فروش و بعد تغییر هزینه: عکس دست نمی‌خورد
        await using (var scope = fixture.Scope(tenant))
        {
            var loaded = (await scope.ServiceProvider.GetRequiredService<IRugRepository>().GetWithWorkflowAsync(rug.Id, tenant))!;
            loaded.Sale!.SalePrice = 2_500_000m;
            loaded.PurchaseLines.Single().Rate = 1_500_000m;
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        await using var db = fixture.CreateContext();
        var stored = await db.RugSales.AsNoTracking().SingleAsync(s => s.RugId == rug.Id);
        Assert.Equal(1_200_000m, stored.CostAtSale);
        Assert.Equal(1_500_000m, (await fixture.TotalAsync(rug.Id))!.Total);
        // سود واقعی از عکس: ۲٬۵۰۰٬۰۰۰ − ۱٬۲۰۰٬۰۰۰، نه از بهای امروز (۱٬۵۰۰٬۰۰۰)
        Assert.Equal(1_300_000m, stored.ActualProfit(1_500_000m));

        // ویرایش فروش از سرویس هم عکس را نگه می‌دارد؛ «به‌روزرسانی بهای تمام‌شده» آن را با بهای امروز عوض می‌کند
        await using (var scope = fixture.Scope(tenant))
        {
            var sales = scope.ServiceProvider.GetRequiredService<IRugSaleService>();
            var edited = await sales.SaveAsync(tenant, rug.Id, new SaveRugSaleRequest(
                "خریدار", null, 2_600_000m, 0m, null, SalePaymentMethod.Cash, null, null, null));
            Assert.Equal((1_200_000m, 1_400_000m), (edited.TotalInvestment, edited.ActualProfit));
        }

        await using (var scope = fixture.Scope(tenant))
        {
            var refreshed = await scope.ServiceProvider.GetRequiredService<IRugSaleService>().RefreshCostAtSaleAsync(tenant, rug.Id);
            Assert.Equal((1_500_000m, 1_100_000m), (refreshed.TotalInvestment, refreshed.ActualProfit));
        }

        await using var after = fixture.CreateContext();
        var refreshedSale = await after.RugSales.AsNoTracking().SingleAsync(s => s.RugId == rug.Id);
        Assert.Equal(1_500_000m, refreshedSale.CostAtSale);
        Assert.NotNull(refreshedSale.CostAtSaleAt);
    }

    // ── قفل رقم اعشار (D.5 #22) ──

    [PostgresFact]
    public async Task MoneyScale_IsLockedOnceMoneyExists()
    {
        var empty = await fixture.NewTenantAsync();
        await ChangeScaleAsync(empty, 2); // هنوز مبلغی نیست

        var withMoney = await fixture.NewTenantAsync();
        var rug = LedgerFixture.NewRug(withMoney, 1m, 1m);
        LedgerFixture.Line(rug, CostBasis.Total, 1m);
        await fixture.SaveNewAsync(withMoney, rug);

        var ex = await Assert.ThrowsAsync<DomainConflictException>(() => ChangeScaleAsync(withMoney, 2));
        Assert.Equal("moneyScale", ex.Field);

        await using var scope = fixture.Scope(withMoney);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = await db.Tenants.SingleAsync(t => t.Id == withMoney);
        tenant.Currency = CurrencyUnit.Rial;
        var currency = await Assert.ThrowsAsync<DomainConflictException>(
            () => scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync());
        Assert.Equal("currency", currency.Field);
    }

    private async Task ChangeScaleAsync(Guid tenantId, short scale)
    {
        await using var scope = fixture.Scope(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var preferences = await db.Set<TenantPreferences>().SingleAsync(p => p.TenantId == tenantId);
        preferences.MoneyScale = scale;
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
    }

    // ── تراکنش بیرونی (D.4 «Ambient transactions») ──

    [PostgresFact]
    public async Task AmbientTransaction_AFailedSaveRollsBackToItsSavepoint_TheRestCommits()
    {
        var tenant = await fixture.NewTenantAsync();
        await fixture.SaveNewAsync(tenant, LedgerFixture.NewRug(tenant, 1m, 1m, "900"));
        var good = LedgerFixture.NewRug(tenant, 1m, 1m, "901");
        LedgerFixture.Line(good, CostBasis.Total, 5_000m);

        await using (var scope = fixture.Scope(tenant))
        {
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var rugs = scope.ServiceProvider.GetRequiredService<IRugRepository>();
            await uow.ExecuteInTransactionAsync(async ct =>
            {
                await rugs.AddAsync(LedgerFixture.NewRug(tenant, 1m, 1m, "900"), ct);
                await Assert.ThrowsAsync<SerialOverlapException>(() => uow.SaveChangesAsync(ct));

                await rugs.AddAsync(good, ct);
                await uow.SaveChangesAsync(ct);
            });
        }

        Assert.Equal(5_000m, (await fixture.TotalAsync(good.Id))!.Total);
        await using var db = fixture.CreateContext();
        Assert.Equal(1, await db.Rugs.CountAsync(r => r.TenantId == tenant && r.SerialNo == "900"));
    }

    [PostgresFact]
    public async Task AmbientTransaction_AFailureRollsBackEverySave()
    {
        var tenant = await fixture.NewTenantAsync();
        var first = LedgerFixture.NewRug(tenant, 1m, 1m, "1");

        await using (var scope = fixture.Scope(tenant))
        {
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var rugs = scope.ServiceProvider.GetRequiredService<IRugRepository>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => uow.ExecuteInTransactionAsync(async ct =>
            {
                await rugs.AddAsync(first, ct);
                await uow.SaveChangesAsync(ct);
                throw new InvalidOperationException("بعد از ذخیرهٔ اول");
            }));
        }

        await using var db = fixture.CreateContext();
        Assert.False(await db.Rugs.IgnoreQueryFilters().AnyAsync(r => r.Id == first.Id));
        Assert.Null(await fixture.TotalAsync(first.Id));
    }

    // ── سنجش و بازسازی (D.4 «Recovery») ──

    [PostgresFact]
    public async Task Verifier_FindsDrift_AndRebuildFixesIt()
    {
        var tenant = await fixture.NewTenantAsync();
        var rug = LedgerFixture.NewRug(tenant, 2m, 2m, "42");
        LedgerFixture.Line(rug, CostBasis.PerM2, 1_000m);
        await fixture.SaveNewAsync(tenant, rug);
        await AssertHealthyAsync(tenant);

        // مسیری که از پاس رد نشد (همان «جاماندگی» که سنجش باید ببیند)
        await using (var db = fixture.CreateContext())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "RugCostTotals" SET "Total" = 1 WHERE "RugId" = {rug.Id}""");
            await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "RugPurchaseLines" SET "Amount" = 2 WHERE "RugId" = {rug.Id}""");
        }

        await using (var scope = fixture.Scope(tenant))
        {
            var report = await scope.ServiceProvider.GetRequiredService<ILedgerVerifier>().VerifyAsync(tenant);
            Assert.Equal(1, report.Checked);
            Assert.Contains(report.Mismatches, m => m.Check == CostMismatchCheck.PurchaseLine && m.Stored == 2m && m.Expected == 4_000m);
            Assert.Contains(report.Mismatches, m => m.Check == CostMismatchCheck.Total && m.Stored == 1m && m.Expected == 4_000m);

            var rebuilt = await scope.ServiceProvider.GetRequiredService<ILedgerMaintenance>().RebuildAsync(tenant);
            Assert.Equal(1, rebuilt.Rugs);
        }

        await AssertHealthyAsync(tenant);
    }

    [PostgresFact]
    public async Task StartupCheck_BuildsMissingTotals()
    {
        // درست بعد از M2 هیچ ردیف RugCostTotal نیست (M2 عمداً نمی‌سازد)
        var tenant = await fixture.NewTenantAsync();
        var rug = LedgerFixture.NewRug(tenant, 1m, 1m);
        LedgerFixture.Line(rug, CostBasis.Total, 7_000m);
        await fixture.SaveNewAsync(tenant, rug);
        await using (var db = fixture.CreateContext())
            await db.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "RugCostTotals" WHERE "TenantId" = {tenant}""");

        await using (var scope = fixture.Root.CreateAsyncScope())
        {
            var check = scope.ServiceProvider.GetServices<IStartupTask>().OfType<RugsManagment.Infrastructure.Persistence.Ledger.LedgerStartupCheck>().Single();
            Assert.Equal(200, check.Order);
            await check.RunAsync(fixture.Root, CancellationToken.None);
        }

        Assert.Equal(7_000m, (await fixture.TotalAsync(rug.Id))!.Total);
        await AssertHealthyAsync(tenant);
    }

    private async Task AssertHealthyAsync(Guid tenantId)
    {
        await using var scope = fixture.Scope(tenantId);
        var report = await scope.ServiceProvider.GetRequiredService<ILedgerVerifier>().VerifyAsync(tenantId);
        Assert.True(report.Mismatches.Count == 0,
            "ناهمخوانی: " + string.Join("; ", report.Mismatches.Select(m => $"{m.Check} {m.Detail} {m.Stored}/{m.Expected}")));
    }
}
