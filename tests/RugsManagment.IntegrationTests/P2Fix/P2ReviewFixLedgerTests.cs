using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.DTOs.Sales;
using RugsManagment.Application.DTOs.Workflows;
using RugsManagment.Application.Features.Rugs;
using RugsManagment.Application.Ledger;
using RugsManagment.Application.Seed;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.Infrastructure.Persistence;
using RugsManagment.Infrastructure.Persistence.Repositories;
using RugsManagment.IntegrationTests.Ledger;
using RugsManagment.IntegrationTests.Support;

namespace RugsManagment.IntegrationTests.P2Fix;

/// <summary>
/// یافته‌های تأییدشدهٔ بازبینی فاز ۲ روی پستگرس واقعی (دیتابیس دورریختنی LedgerFixture، DI واقعی برنامه): حذف نوع مرحله‌ای
/// که فقط روی فرش سطل زباله است، عکس بهای فروش زیر قفل و ویرایش فروشی که آن را برنمی‌گرداند، پیام ۴۰۹ بازگردانی با شمارهٔ
/// واقعی، کارت انبار خانه با قاعدهٔ دسته، وصل شدن سطر قیمت خرید قدیمیِ فرش سطل زباله، و مبلغ سطر خرید بیرون از ستون (۴۰۰).
/// </summary>
public sealed class P2ReviewFixLedgerTests(LedgerFixture fixture) : IClassFixture<LedgerFixture>
{
    // ── #0: نوع مرحله‌ای که فقط روی فرش سطل زباله است ─────────────────────

    [PostgresFact]
    public async Task DeletingAStepType_UsedOnlyOnATrashedRug_DeactivatesIt_AndKeepsTheRugsStepsAndTotal()
    {
        var tenant = await fixture.NewTenantAsync();
        Guid typeId;
        await using (var scope = fixture.Scope(tenant))
        {
            var created = await scope.ServiceProvider.GetRequiredService<IStepTypeService>().CreateAsync(tenant,
                new SaveProcessStepTypeRequest("رفوی بازبینی", null, "workflow", 0, StepPricingModel.Fixed, 0m, null, null, true));
            typeId = created.Id;
        }

        var rug = LedgerFixture.NewRug(tenant, 2m, 3m);
        rug.WorkflowSteps.Add(new RugWorkflowStep
        {
            RugId = rug.Id, ProcessStepTypeId = typeId, Status = WorkflowStepStatus.Completed, CalculatedCost = 1_000_000m
        });
        await fixture.SaveNewAsync(tenant, rug);
        Assert.Equal(1_000_000m, (await fixture.TotalAsync(rug.Id))!.Total);

        await using (var scope = fixture.Scope(tenant))
            await scope.ServiceProvider.GetRequiredService<IRugManagementService>().SoftDeleteAsync(tenant, rug.Id);

        await using (var scope = fixture.Scope(tenant))
            await scope.ServiceProvider.GetRequiredService<IStepTypeService>().DeleteAsync(tenant, typeId);

        await using var db = fixture.CreateContext();
        var type = await db.ProcessStepTypes.AsNoTracking().SingleOrDefaultAsync(t => t.Id == typeId);
        Assert.NotNull(type);          // فقط غیرفعال شد، حذف نشد
        Assert.False(type.IsActive);
        Assert.Equal(1, await db.RugWorkflowSteps.IgnoreQueryFilters().CountAsync(s => s.RugId == rug.Id));
        Assert.Equal(1_000_000m, (await fixture.TotalAsync(rug.Id))!.Total);
        await AssertHealthyAsync(tenant);
    }

    // ── #1: «به‌روزرسانی بهای تمام‌شده» و ویرایش فروش ────────────────────

    [PostgresFact]
    public async Task RefreshCost_RacingAnUncommittedCostChange_WaitsForTheLock_AndSnapshotsTheCommittedTotal()
    {
        var (tenant, rug, line) = await SoldRugAsync(10_000_000m);

        // درخواست B: سطر خرید را بالا برده و ذخیره کرده (پاس قفل کارگاه را دارد) ولی هنوز commit نکرده
        await using var scopeB = fixture.Scope(tenant);
        var dbB = scopeB.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await dbB.Database.BeginTransactionAsync();
        var lineB = await scopeB.ServiceProvider.GetRequiredService<IRepository<RugPurchaseLine>>().GetByIdAsync(line);
        lineB!.Rate = 10_500_000m;
        await scopeB.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        // درخواست A: «به‌روزرسانی بهای تمام‌شده» — فرش را با جمع کهنه می‌خواند و پشت قفل می‌ماند
        var refresh = Task.Run(async () =>
        {
            await using var scopeA = fixture.Scope(tenant);
            return await scopeA.ServiceProvider.GetRequiredService<IRugSaleService>().RefreshCostAtSaleAsync(tenant, rug);
        });

        await WaitForAdvisoryLockWaiterAsync(refresh);
        await transaction.CommitAsync();
        var refreshed = await refresh;

        Assert.Equal(10_500_000m, refreshed.TotalInvestment);
        await using var db = fixture.CreateContext();
        Assert.Equal(10_500_000m, (await db.RugSales.AsNoTracking().SingleAsync(s => s.RugId == rug)).CostAtSale);
        Assert.Equal(10_500_000m, (await fixture.TotalAsync(rug))!.Total);
    }

    [PostgresFact]
    public async Task SaleEdit_LoadedBeforeARefresh_DoesNotRevertTheRefreshedCostAtSale()
    {
        var (tenant, rug, line) = await SoldRugAsync(10_000_000m);
        await using (var scope = fixture.Scope(tenant))
        {
            var tracked = await scope.ServiceProvider.GetRequiredService<IRepository<RugPurchaseLine>>().GetByIdAsync(line);
            tracked!.Rate = 10_500_000m;
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        // درخواست ویرایش فروش فرش را (با عکس کهنهٔ ۱۰ میلیون) پیش از «به‌روزرسانی» خوانده است
        await using var edit = fixture.Scope(tenant);
        _ = await edit.ServiceProvider.GetRequiredService<IRugRepository>().GetWithWorkflowAsync(rug, tenant);

        await using (var scope = fixture.Scope(tenant))
            await scope.ServiceProvider.GetRequiredService<IRugSaleService>().RefreshCostAtSaleAsync(tenant, rug);

        var saved = await edit.ServiceProvider.GetRequiredService<IRugSaleService>().SaveAsync(tenant, rug, new SaveRugSaleRequest(
            "خریدار", "09120000000", 12_000_000m, 0m, null, SalePaymentMethod.Cash, null, null, null));

        await using var db = fixture.CreateContext();
        var stored = await db.RugSales.AsNoTracking().SingleAsync(s => s.RugId == rug);
        Assert.Equal(10_500_000m, stored.CostAtSale);   // پیش‌تر Update() همهٔ ستون‌ها را با ۱۰ میلیون کهنه می‌نوشت
        Assert.Equal("09120000000", stored.BuyerPhone);
        Assert.Equal(12_000_000m, stored.SalePrice);
        Assert.NotNull(saved);
    }

    // ── #2: پیام ۴۰۹ بازگردانی ────────────────────────────────────────────

    [PostgresFact]
    public async Task Restore_WhenTheSerialIsNowTaken_Is409NamingTheSerial()
    {
        var tenant = await fixture.NewTenantAsync();
        var trashed = LedgerFixture.NewRug(tenant, 1m, 1m, "500");
        await fixture.SaveNewAsync(tenant, trashed);
        await using (var scope = fixture.Scope(tenant))
            await scope.ServiceProvider.GetRequiredService<IRugManagementService>().SoftDeleteAsync(tenant, trashed.Id);
        await fixture.SaveNewAsync(tenant, LedgerFixture.NewRug(tenant, 1m, 1m, "500"));

        await using (var scope = fixture.Scope(tenant))
        {
            var error = await Assert.ThrowsAsync<SerialOverlapException>(() =>
                scope.ServiceProvider.GetRequiredService<IRugManagementService>().RestoreAsync(tenant, trashed.Id));
            Assert.Equal("500", error.Serial);
            Assert.Contains("500", error.Message);
            Assert.DoesNotContain("«»", error.Message);
        }

        await using var db = fixture.CreateContext();
        Assert.NotNull((await db.Rugs.IgnoreQueryFilters().AsNoTracking().SingleAsync(r => r.Id == trashed.Id)).DeletedAt);
    }

    // ── #4: کارت انبار خانه (منبع پیش‌فرض) ───────────────────────────────

    [PostgresFact]
    public async Task HomeStock_DefaultSource_CountsLotPieces_AndTheLotArea()
    {
        var tenant = await fixture.NewTenantAsync();
        await fixture.SaveNewAsync(tenant,
            LedgerFixture.NewRug(tenant, 0.40m, 0.40m, "3911 - 3960", 50),
            LedgerFixture.NewRug(tenant, 2.08m, 2.10m, "3778"));

        await using var db = fixture.CreateContext();
        var stock = await new RugHomeStockSource(db).GetAsync(tenant);

        Assert.Equal(51, stock.Pieces);       // پیش‌تر ۲
        Assert.Equal(12.37m, stock.AreaM2);   // ۸٫۰۰ + ۴٫۳۷ (پیش‌تر ۴٫۵۳)
    }

    // ── #9: سطر قیمت خرید قدیمیِ فرش سطل زباله ───────────────────────────

    [PostgresFact]
    public async Task Seed_LinksTheLegacyLineOfATrashedRug_SoRestoreThenCellWrite_KeepsOneLine()
    {
        var tenant = await fixture.NewTenantAsync();
        var rug = LedgerFixture.NewRug(tenant, 2m, 3m, "4100");
        var legacy = LedgerFixture.Line(rug, CostBasis.Total, 5_000m, 0, RugPurchaseLine.LegacyPurchaseLabel); // همان سطر M2: بی‌نوع
        await fixture.SaveNewAsync(tenant, rug);
        await using (var scope = fixture.Scope(tenant))
            await scope.ServiceProvider.GetRequiredService<IRugManagementService>().SoftDeleteAsync(tenant, rug.Id);

        await using (var scope = fixture.Scope(tenant))
            await scope.ServiceProvider.GetRequiredService<ITenantSeedRunner>().SeedTenantAsync(tenant);

        Guid priceCategory;
        await using (var db = fixture.CreateContext())
        {
            priceCategory = (await db.Set<CostCategory>().AsNoTracking()
                .SingleAsync(c => c.TenantId == tenant && c.SeedKey == CostCategorySeedContributor.PriceKey)).Id;
            var stored = await db.Set<RugPurchaseLine>().IgnoreQueryFilters().AsNoTracking().SingleAsync(l => l.Id == legacy.Id);
            Assert.Equal(priceCategory, stored.CategoryId);
            Assert.Equal(5_000m, stored.Amount);
        }

        await using (var scope = fixture.Scope(tenant))
            await scope.ServiceProvider.GetRequiredService<IRugManagementService>().RestoreAsync(tenant, rug.Id);

        // همان مسیر خانهٔ «قیمت خرید» جدول ورود (PUT /api/rugs/{id} با values)
        await using (var scope = fixture.Scope(tenant))
        {
            await scope.ServiceProvider.GetRequiredService<IRugManagementService>().UpdateAsync(tenant, rug.Id,
                new UpdateRugRequest(null, null, null, null, null, 0m, 0m, null, null, null, null, null,
                    Version: await fixture.RugVersionAsync(rug.Id),
                    Values: new Dictionary<string, JsonElement?> { ["purchase.price"] = JsonSerializer.SerializeToElement(7_000m) }));
        }

        await using (var db = fixture.CreateContext())
        {
            var line = Assert.Single(await db.Set<RugPurchaseLine>().AsNoTracking().Where(l => l.RugId == rug.Id).ToListAsync());
            Assert.Equal((priceCategory, CostBasis.Total, 7_000m), (line.CategoryId!.Value, line.Basis, line.Amount));
        }
        Assert.Equal(7_000m, (await fixture.TotalAsync(rug.Id))!.PurchaseCost); // نه ۵٬۰۰۰ + ۴۲٬۰۰۰
        await AssertHealthyAsync(tenant);
    }

    // ── #13: مبلغ سطر خرید بیرون از numeric(18,2) ────────────────────────

    [PostgresFact]
    public async Task PurchaseRate_WhoseAmountOverflowsTheColumn_Is400_NotA500()
    {
        var tenant = await fixture.NewTenantAsync();
        await using (var scope = fixture.Scope(tenant))
            await scope.ServiceProvider.GetRequiredService<ITenantSeedRunner>().SeedTenantAsync(tenant);

        await using (var scope = fixture.Scope(tenant))
        {
            var error = await Assert.ThrowsAsync<AppValidationException>(() =>
                scope.ServiceProvider.GetRequiredService<IRugManagementService>().CreateAsync(tenant, NewRugRequest(
                    ("rug.intakeLength", 30m), ("rug.intakeWidth", 30m),
                    ("purchase.price", new { rate = RugFieldWriter.MaxRate, basis = (int)CostBasis.PerM2 }))));
            Assert.True(error.Errors.ContainsKey("purchase.price"));
        }

        var rug = LedgerFixture.NewRug(tenant, 30m, 30m);
        await fixture.SaveNewAsync(tenant, rug);
        var version = await fixture.RugVersionAsync(rug.Id);
        await using (var scope = fixture.Scope(tenant))
        {
            var error = await Assert.ThrowsAsync<AppValidationException>(() =>
                scope.ServiceProvider.GetRequiredService<IRugPurchaseService>().ReplaceAsync(tenant, rug.Id,
                    new ReplacePurchaseLinesRequest(
                        [new PurchaseLineDto(null, null, "کرایه", CostBasis.PerM2, RugFieldWriter.MaxRate, 0m, 10, null)],
                        version)));
            Assert.True(error.Errors.ContainsKey("lines[0].rate"));
        }

        Assert.Empty(await fixture.LineAmountsAsync(rug.Id));
    }

    [PostgresFact]
    public async Task AmountOverflow_FromAnyOtherPath_Is400_NotA500_AndNothingIsSaved()
    {
        // سطر متری بزرگ روی ۱×۱ درست است؛ فرم قدیمی (طول/عرض مؤثر) اندازهٔ اول را ۳۰×۳۰ می‌کند و مبلغ ۹۰۰ برابر می‌شود
        var tenant = await fixture.NewTenantAsync();
        var rug = LedgerFixture.NewRug(tenant, 1m, 1m);
        LedgerFixture.Line(rug, CostBasis.PerM2, RugFieldWriter.MaxRate);
        await fixture.SaveNewAsync(tenant, rug);

        await using (var scope = fixture.Scope(tenant))
        {
            var error = await Assert.ThrowsAsync<AppValidationException>(() =>
                scope.ServiceProvider.GetRequiredService<IRugManagementService>().UpdateAsync(tenant, rug.Id,
                    new UpdateRugRequest(null, null, null, null, null, 30m, 30m, null, null, null, null, null)));
            Assert.Equal(Messages.AmountOverflow.Text, error.Message);
        }

        await using var db = fixture.CreateContext();
        var stored = await db.Rugs.AsNoTracking().SingleAsync(r => r.Id == rug.Id);
        Assert.Equal((1m, 1m), (stored.IntakeLength, stored.IntakeWidth));
        Assert.Equal([RugFieldWriter.MaxRate], await fixture.LineAmountsAsync(rug.Id));
    }

    // ── کمک‌ها ────────────────────────────────────────────────────────────

    private static CreateRugRequest NewRugRequest(params (string Key, object Value)[] values)
        => new(null, null, null, null, null, 0m, 0m, null, null, null, null, null, null, null,
            Values: values.ToDictionary(v => v.Key, v => (JsonElement?)JsonSerializer.SerializeToElement(v.Value), StringComparer.Ordinal));

    /// <summary>فرش فروخته‌شده با یک سطر «کل»؛ عکس بهای فروش همان مبلغ سطر است.</summary>
    private async Task<(Guid Tenant, Guid Rug, Guid Line)> SoldRugAsync(decimal price)
    {
        var tenant = await fixture.NewTenantAsync();
        var rug = LedgerFixture.NewRug(tenant, 2m, 3m);
        var line = LedgerFixture.Line(rug, CostBasis.Total, price);
        await fixture.SaveNewAsync(tenant, rug);

        await using (var scope = fixture.Scope(tenant))
        {
            var sale = await scope.ServiceProvider.GetRequiredService<IRugSaleService>().SaveAsync(tenant, rug.Id,
                new SaveRugSaleRequest("خریدار", null, 12_000_000m, 0m, null, SalePaymentMethod.Cash, null, null, null));
            Assert.Equal(price, sale.TotalInvestment);
        }

        return (tenant, rug.Id, line.Id);
    }

    /// <summary>تا وقتی درخواستی پشت قفل مشورتی منتظر است (یا کار زودتر تمام شد که یعنی قفلی نگرفته — آزمون بعد می‌شکند).</summary>
    private async Task WaitForAdvisoryLockWaiterAsync(Task work)
    {
        await using var db = fixture.CreateContext();
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await connection.OpenAsync();
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline && !work.IsCompleted)
        {
            await using var command = new NpgsqlCommand(
                "SELECT count(*) FROM pg_locks WHERE locktype = 'advisory' AND NOT granted AND database = (SELECT oid FROM pg_database WHERE datname = current_database())",
                connection);
            if ((long)(await command.ExecuteScalarAsync())! > 0) return;
            await Task.Delay(50);
        }

        Assert.False(work.IsCompleted, "«به‌روزرسانی بها» پیش از commit درخواست B تمام شد: پشت قفل کارگاه نماند.");
        Assert.Fail("درخواست A هرگز پشت قفل کارگاه منتظر نماند.");
    }

    private async Task AssertHealthyAsync(Guid tenantId)
    {
        await using var scope = fixture.Scope(tenantId);
        var report = await scope.ServiceProvider.GetRequiredService<ILedgerVerifier>().VerifyAsync(tenantId);
        Assert.True(report.Mismatches.Count == 0,
            "ناهمخوانی: " + string.Join("; ", report.Mismatches.Select(m => $"{m.Check} {m.Detail} {m.Stored}/{m.Expected}")));
    }
}
