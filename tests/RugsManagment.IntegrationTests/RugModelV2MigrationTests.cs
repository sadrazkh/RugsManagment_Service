using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using RugsManagment.Domain.Common;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.Infrastructure.Persistence;
using RugsManagment.IntegrationTests.Support;

namespace RugsManagment.IntegrationTests;

/// <summary>
/// M2 (<c>RugModelV2AndCosting</c>، spec B.4–B.8، L.2) روی دیتابیس دورریختنی <c>rugs_p2_dom_xxxxxxxx</c> (هرگز دیتابیس
/// واقعی): ابعاد قدیمی سه‌رقمی گرد و پشتیبان‌گیری می‌شوند و جفت مؤثر با اندازهٔ اول یکی است؛ قیمت خرید قدیمی سطر «کل» می‌شود؛
/// ورودی‌ها شماره می‌گیرند؛ عکس بهای فروش پر می‌شود؛ ناسازگاری ساختگی کل migration را برمی‌گرداند؛ Down/Up رفت‌وبرگشت
/// دارد؛ ستون‌های تولیدشده با <see cref="AreaMath"/> روی بردارهای مشترک یکی‌اند؛ و قیدها و ایندکس‌های تازه کار می‌کنند.
/// دادهٔ قدیمی با SQL خام و فقط ASCII ساخته می‌شود (مدل فعلی EF ستون‌هایی دارد که پیش از M2 نیستند).
/// </summary>
public sealed class RugModelV2MigrationTests
{
    private const string M1 = "20261003234036_PlatformPreferences";
    private const string M2Suffix = "_RugModelV2AndCosting";

    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid RugOdd = Guid.Parse("a0000000-0000-0000-0000-000000000001");   // 2.675 × 1.005، قیمت ۱۰۰۰، مراحل، فروش
    private static readonly Guid RugTrashed = Guid.Parse("a0000000-0000-0000-0000-000000000002"); // بی‌قیمت، سطل زباله
    private static readonly Guid RugScale2 = Guid.Parse("a0000000-0000-0000-0000-000000000003");  // کارگاه با MoneyScale 2، فروش
    private static readonly Guid RugZero = Guid.Parse("a0000000-0000-0000-0000-000000000004");    // قیمت ۰، ابعاد چهاررقمی

    [PostgresFact]
    public async Task M2_OnLegacyRows_RoundsSizes_MovesPurchaseCost_NumbersBatches_AndSnapshotsSales()
    {
        await WithDatabaseAsync(async db =>
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(M1);
            Assert.Contains(await db.Database.GetPendingMigrationsAsync(), id => id.EndsWith(M2Suffix, StringComparison.Ordinal));
            await SeedLegacyRowsAsync(db);

            await migrator.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges());

            var rugs = await db.Rugs.IgnoreQueryFilters().AsNoTracking().OrderBy(r => r.Sku).ToListAsync();
            Assert.Equal(4, rugs.Count);
            foreach (var rug in rugs)
            {
                // جفت مؤثر = نهایی ?? اول؛ بدون اندازهٔ نهایی مساحت مؤثر همان مساحت اول است
                Assert.Equal(rug.FinalLength ?? rug.IntakeLength, rug.LengthMeters);
                Assert.Equal(rug.FinalWidth ?? rug.IntakeWidth, rug.WidthMeters);
                Assert.Null(rug.FinalLength);
                Assert.Equal(1, rug.Quantity);
                Assert.Null(rug.SerialNo);
            }

            var stored = await StoredAreasAsync(db);
            Assert.All(stored, s => Assert.Equal(s.Intake, s.Area));

            var odd = rugs.Single(r => r.Id == RugOdd);
            Assert.Equal((2.68m, 1.01m), (odd.LengthMeters, odd.WidthMeters)); // 2.675 / 1.005 نصفه دور از صفر
            Assert.Equal(2.71m, stored.Single(s => s.Id == RugOdd).Area);
            Assert.Equal((3.34m, 2.53m), (rugs.Single(r => r.Id == RugZero).LengthMeters, rugs.Single(r => r.Id == RugZero).WidthMeters));

            // اصل ابعاد در پشتیبان
            var backup = await db.Database.SqlQuery<decimal>(
                $"""SELECT "Length" AS "Value" FROM "_m2_rug_dims_backup" WHERE "RugId" = {RugOdd}""").SingleAsync();
            Assert.Equal(2.675m, backup);
            var comment = await db.Database.SqlQuery<string>(
                $"""SELECT obj_description('"_m2_rug_dims_backup"'::regclass) AS "Value" """).SingleAsync();
            Assert.Contains("2 rug(s) changed", comment, StringComparison.Ordinal);

            // قیمت خرید قدیمی ← یک سطر «کل» با برچسب «قیمت خرید»، بی نوع هزینه؛ مبلغ در MoneyScale کارگاه
            var lines = await db.Set<RugPurchaseLine>().IgnoreQueryFilters().AsNoTracking().ToListAsync();
            Assert.Equal(3, lines.Count);
            Assert.All(lines, l =>
            {
                Assert.Equal(RugPurchaseLine.LegacyPurchaseLabel, l.Label);
                Assert.Equal(CostBasis.Total, l.Basis);
                Assert.Null(l.CategoryId);
                Assert.Equal(l.Rate, l.Amount);
            });
            Assert.Equal(1000m, lines.Single(l => l.RugId == RugOdd).Amount);
            Assert.Equal(1234.56m, lines.Single(l => l.RugId == RugScale2).Amount);
            Assert.Equal(0m, lines.Single(l => l.RugId == RugZero).Amount);
            Assert.DoesNotContain(lines, l => l.RugId == RugTrashed);
            Assert.All(lines, l => Assert.Equal(rugs.Single(r => r.Id == l.RugId).TenantId, l.TenantId));

            // عکس بهای فروش = جمع قدیم: ۱۰۰۰ + (۵۰۰٫۲۵ − ۱۰۰٫۲۵) + ۳۰۰ (دستی) + ۰ (کف صفر)؛ مراحل در صف/ردشده حساب نیستند
            var sales = await db.RugSales.IgnoreQueryFilters().AsNoTracking().ToListAsync();
            var oddSale = sales.Single(s => s.RugId == RugOdd);
            Assert.Equal(1700m, oddSale.CostAtSale);
            Assert.Equal(oddSale.SoldAt, oddSale.CostAtSaleAt);
            Assert.Equal(1234.56m, sales.Single(s => s.RugId == RugScale2).CostAtSale);

            // ورودی‌ها به ترتیب ساخت در هر کارگاه؛ شمارنده از بزرگ‌ترین (یا مقدار جلوترِ موجود) ادامه می‌دهد
            var batches = await db.RugBatches.AsNoTracking().OrderBy(b => b.TenantId).ThenBy(b => b.Number).ToListAsync();
            Assert.Equal(new[] { ("early", 1), ("late", 2), ("other", 1) }, batches.Select(b => (b.Name, b.Number)));
            Assert.All(batches, b =>
            {
                Assert.Null(b.ReceivedOn);
                Assert.Equal(string.Empty, b.SellerKey);
            });
            Assert.Equal(10, await CounterAsync(db, TenantA));
            Assert.Equal(1, await CounterAsync(db, TenantB));

            // M2 ردیف RugCostTotal نمی‌سازد (بررسی استارت دفتر می‌سازد)
            Assert.Equal(0, await db.Set<RugCostTotal>().IgnoreQueryFilters().CountAsync());
        });
    }

    [PostgresFact]
    public async Task M2_SelfVerify_RaisesOnACraftedInconsistency_AndRollsEverythingBack()
    {
        await WithDatabaseAsync(async db =>
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(M1);
            await SeedLegacyRowsAsync(db);
            // MoneyScale کارگاه ۰ است؛ قیمت اعشاری با گرد شدن سطر «کل» جمع را عوض می‌کند
            await db.Database.ExecuteSqlAsync($"""UPDATE "Rugs" SET "PurchaseCost" = 1000.5 WHERE "Id" = {RugOdd}""");

            var thrown = await Assert.ThrowsAnyAsync<Exception>(() => migrator.MigrateAsync());
            var error = thrown as PostgresException ?? thrown.InnerException as PostgresException;
            Assert.NotNull(error);
            Assert.Equal("P0001", error.SqlState);
            Assert.Contains("self-verify", error.MessageText, StringComparison.Ordinal);
            Assert.Contains("RUG-1 (old 1700.50, new 1701.00)", error.MessageText, StringComparison.Ordinal);

            Assert.Equal(M1, (await db.Database.GetAppliedMigrationsAsync()).Last());
            Assert.False(await ColumnExistsAsync(db, "Rugs", "IntakeLength"));
            Assert.False(await TableExistsAsync(db, "_m2_rug_dims_backup"));
            var length = await db.Database.SqlQuery<decimal>(
                $"""SELECT "LengthMeters" AS "Value" FROM "Rugs" WHERE "Id" = {RugOdd}""").SingleAsync();
            Assert.Equal(2.675m, length); // گرد کردن هم برگشت
        });
    }

    [PostgresFact]
    public async Task M2_DownThenUp_RestoresUntouchedSizes_KeepsLaterEdits_AndWritesPurchaseCostBack()
    {
        await WithDatabaseAsync(async db =>
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(M1);
            await SeedLegacyRowsAsync(db);
            await migrator.MigrateAsync();

            // بعد از M2: اندازهٔ نهایی برای یک فرش و یک سطر خرید تازه برای دیگری
            await db.Database.ExecuteSqlAsync($"""
                UPDATE "Rugs" SET "FinalLength" = 3.30, "FinalWidth" = 2.50, "LengthMeters" = 3.30, "WidthMeters" = 2.50
                WHERE "Id" = {RugZero}
                """);
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO "RugPurchaseLines" ("Id", "TenantId", "RugId", "Label", "Basis", "Rate", "Amount", "SortOrder", "CreatedAt")
                VALUES ({Guid.NewGuid()}, {TenantA}, {RugOdd}, 'broker', 3, 2, 20, 1, now())
                """);
            // و تنها سطر خرید فرشی دیگر پاک شد (خرید حالا صفر): Down نباید قیمت پیش از M2 را برگرداند (بازبینی فاز ۲)
            await db.Database.ExecuteSqlAsync($"""DELETE FROM "RugPurchaseLines" WHERE "RugId" = {RugScale2}""");

            await migrator.MigrateAsync(M1);
            Assert.False(await TableExistsAsync(db, "RugPurchaseLines"));
            Assert.False(await ColumnExistsAsync(db, "Rugs", "AreaM2"));
            Assert.False(await ColumnExistsAsync(db, "RugBatches", "Number"));
            Assert.True(await TableExistsAsync(db, "_m2_rug_dims_backup")); // فقط M7 حذفش می‌کند
            Assert.Equal(0, await db.Database.SqlQuery<int>(
                $"""SELECT count(*)::int AS "Value" FROM "TenantCounters" WHERE "Key" = 'batch'""").SingleAsync());

            var after = await db.Database.SqlQuery<LegacyRugRow>($"""
                SELECT "Id", "LengthMeters", "WidthMeters", "PurchaseCost" FROM "Rugs"
                """).ToListAsync();
            Assert.Equal((2.675m, 1.005m, 1020m), Shape(after, RugOdd));          // اصل ابعاد؛ قیمت = جمع دو سطر
            Assert.Equal((3.30m, 2.50m, 0m), Shape(after, RugZero));              // اندازهٔ بعد از M2 می‌ماند
            Assert.Null(after.Single(r => r.Id == RugTrashed).PurchaseCost);
            Assert.Null(after.Single(r => r.Id == RugScale2).PurchaseCost);       // سطرهایش پاک شد → NULL، نه ۱۲۳۴٫۵۶ پیش از M2

            await migrator.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            var odd = await db.Rugs.IgnoreQueryFilters().AsNoTracking().SingleAsync(r => r.Id == RugOdd);
            Assert.Equal((2.68m, 1.01m), (odd.IntakeLength, odd.IntakeWidth));
            var oddLine = await db.Set<RugPurchaseLine>().AsNoTracking().SingleAsync(l => l.RugId == RugOdd);
            Assert.Equal(1020m, oddLine.Amount);
        });
    }

    /// <summary>L.2 «C#/SQL parity»: ستون‌های تولیدشده همان <see cref="AreaMath"/> روی بردارهای <c>pieceArea</c> مشترک.</summary>
    [PostgresFact]
    public async Task GeneratedAreas_EqualAreaMath_OverTheSharedVectors()
    {
        await WithDatabaseAsync(async db =>
        {
            await db.Database.MigrateAsync();
            var tenant = await AddTenantAsync(db);

            var vectors = PieceAreaVectors();
            Assert.NotEmpty(vectors);
            var rugs = new List<(Rug Rug, decimal Piece, decimal Line)>();
            var i = 0;
            foreach (var (length, width, qty, piece, line) in vectors)
            {
                var rug = new Rug { TenantId = tenant, Sku = $"AREA-{++i}" };
                rug.SetIntakeSize(length, width);
                rug.SetQuantity(qty);
                db.Rugs.Add(rug);
                rugs.Add((rug, piece, line));

                // پیش از ذخیره، getter همان فرمول را می‌دهد
                Assert.Equal(piece, rug.AreaM2);
                Assert.Equal(line, rug.TotalAreaM2);
            }

            await db.SaveChangesAsync();

            var stored = await StoredAreasAsync(db);
            foreach (var (rug, piece, line) in rugs)
            {
                var row = stored.Single(s => s.Id == rug.Id);
                Assert.Equal(piece, row.Area);
                Assert.Equal(line, row.Total);
                Assert.Equal(piece, row.Intake);
                Assert.Equal(AreaMath.PieceArea(rug.LengthMeters, rug.WidthMeters), row.Area);
            }

            // پروجکشن EF ستون ذخیره‌شده را می‌خواند
            var projected = await db.Rugs.AsNoTracking().Where(r => r.TenantId == tenant)
                .Select(r => new { r.Id, r.TotalAreaM2 }).ToListAsync();
            Assert.All(projected, p => Assert.Equal(rugs.Single(x => x.Rug.Id == p.Id).Line, p.TotalAreaM2));
        });
    }

    [PostgresFact]
    public async Task NewTablesAndConstraints_BehaveAsSpecified()
    {
        await WithDatabaseAsync(async db =>
        {
            await db.Database.MigrateAsync();
            var tenant = await AddTenantAsync(db);

            var lot = new Rug { TenantId = tenant, Sku = "LOT-1", ClientRowId = Guid.NewGuid() };
            lot.SetIntakeSize(0.40m, 0.40m);
            lot.SetSerialAndQuantity(SerialNumber.Parse("3911 - 3960"), 50);
            var category = new CostCategory
            {
                TenantId = tenant, SeedKey = "purchase.price", Name = "price", Group = CostGroup.Purchase,
                DefaultBasis = CostBasis.PerM2, DefaultRate = 9_000_000.1234m, ShowInIntake = true
            };
            db.Rugs.Add(lot);
            db.Set<CostCategory>().Add(category);
            db.Set<RugPurchaseLine>().Add(new RugPurchaseLine
            {
                TenantId = tenant, RugId = lot.Id, CategoryId = category.Id, Label = "price",
                Basis = CostBasis.PerM2, Rate = 45.55m, Amount = 350m
            });
            db.Set<RugCostTotal>().Add(new RugCostTotal
            {
                RugId = lot.Id, TenantId = tenant, PurchaseCost = 350m, Total = 350m, CostPerM2 = 44m, CostPerPiece = 7m,
                ComputedAt = DateTimeOffset.UtcNow, LedgerVersion = 1
            });
            await db.SaveChangesAsync();

            var loaded = await db.Rugs.AsNoTracking().Include(r => r.PurchaseLines).Include(r => r.CostTotal).SingleAsync(r => r.Id == lot.Id);
            Assert.Equal("3911 - 3960", loaded.SerialNo);
            Assert.Equal((3911, 3960, 3911L), (loaded.SerialFrom!.Value, loaded.SerialTo!.Value, loaded.SerialSort!.Value));
            Assert.Equal(8.00m, loaded.TotalAreaM2);
            Assert.Equal(350m, loaded.PurchaseLines.Single().Amount);
            Assert.Equal(350m, loaded.CostTotal!.Total);
            Assert.Equal(9_000_000.1234m, (await db.Set<CostCategory>().AsNoTracking().SingleAsync()).DefaultRate);

            // شمارهٔ یکسان بین فرش‌های فعال ممنوع؛ فرش سطل‌زباله شماره‌اش را آزاد می‌کند
            await AssertSqlStateAsync("23505", () => InsertRawRugAsync(db, tenant, "DUP-1", "'3911 - 3960'", "3911", "3960", 50));
            await db.Database.ExecuteSqlAsync($"""UPDATE "Rugs" SET "DeletedAt" = now() WHERE "Id" = {lot.Id}""");
            await InsertRawRugAsync(db, tenant, "DUP-2", "'3911 - 3960'", "3911", "3960", 50);

            // فیلتر حذف نرم روی سطر خرید و جمع هزینه
            Assert.False(await db.Set<RugPurchaseLine>().AnyAsync(l => l.RugId == lot.Id));
            Assert.False(await db.Set<RugCostTotal>().AnyAsync(t => t.RugId == lot.Id));
            Assert.True(await db.Set<RugCostTotal>().IgnoreQueryFilters().AnyAsync(t => t.RugId == lot.Id));

            // CHECKها: تعداد ≥ ۱، بازه نه کوتاه‌تر از تعداد، اندازهٔ نهایی هر دو یا هیچ
            await AssertSqlStateAsync("23514", () => InsertRawRugAsync(db, tenant, "CK-1", "NULL", "NULL", "NULL", 0));
            await AssertSqlStateAsync("23514", () => InsertRawRugAsync(db, tenant, "CK-2", "'1 - 10'", "1", "10", 11));
            await AssertSqlStateAsync("23514", () => InsertRawRugAsync(db, tenant, "CK-3", "'7'", "7", "NULL", 1));
            await AssertSqlStateAsync("23514", () => db.Database.ExecuteSqlAsync(
                $"""UPDATE "Rugs" SET "FinalLength" = 1 WHERE "Id" = {lot.Id}"""));

            // شناسهٔ ردیف جدول ورود یکتاست
            var retry = new Rug { TenantId = tenant, Sku = "LOT-RETRY", ClientRowId = lot.ClientRowId };
            retry.SetIntakeSize(1m, 1m);
            await using (var other = CreateContext(db))
            {
                other.Rugs.Add(retry);
                await AssertSqlStateAsync("23505", () => other.SaveChangesAsync());
            }

            // نوعِ هزینهٔ به‌کاررفته حذف نمی‌شود (NO ACTION)، ولی حذف کارگاه آبشاری همه را می‌برد
            await AssertSqlStateAsync("23503", () => db.Database.ExecuteSqlAsync(
                $"""DELETE FROM "CostCategories" WHERE "Id" = {category.Id}"""));
            await db.Database.ExecuteSqlAsync($"""DELETE FROM "Tenants" WHERE "Id" = {tenant}""");
            Assert.Equal(0, await db.Set<CostCategory>().CountAsync());
            Assert.Equal(0, await db.Set<RugPurchaseLine>().IgnoreQueryFilters().CountAsync());
        });
    }

    [PostgresFact]
    public async Task BatchKeyAndConcurrencyTokens_Work()
    {
        await WithDatabaseAsync(async db =>
        {
            await db.Database.MigrateAsync();
            var tenant = await AddTenantAsync(db);
            var day = new DateOnly(2026, 9, 6);

            db.RugBatches.AddRange(
                new RugBatch { TenantId = tenant, Number = 1, Name = "a", ReceivedOn = day, SellerKey = "seller" },
                new RugBatch { TenantId = tenant, Number = 2, Name = "b", ReceivedOn = day, SellerKey = "other" },
                new RugBatch { TenantId = tenant, Number = 3, Name = "c", ReceivedOn = null, SellerKey = null },
                new RugBatch { TenantId = tenant, Number = 4, Name = "d", ReceivedOn = null, SellerKey = null });
            await db.SaveChangesAsync();

            // یک ورودی برای هر فروشنده در هر روز (کلید خالی هم یکتاست: NULLS NOT DISTINCT)
            await AssertSqlStateAsync("23505", () => AddBatchAsync(db, tenant, 5, day, "seller"));
            await AddBatchAsync(db, tenant, 6, day, null);
            await AssertSqlStateAsync("23505", () => AddBatchAsync(db, tenant, 7, day, null));
            await AssertSqlStateAsync("23505", () => AddBatchAsync(db, tenant, 1, day.AddDays(1), "x")); // شمارهٔ تکراری

            // xmin روی ورودی و نوع هزینه
            db.Set<CostCategory>().Add(new CostCategory { TenantId = tenant, Name = "wash" });
            await db.SaveChangesAsync();
            await using var first = CreateContext(db);
            await using var second = CreateContext(db);

            var b1 = await first.RugBatches.SingleAsync(b => b.Number == 1 && b.TenantId == tenant);
            var b2 = await second.RugBatches.SingleAsync(b => b.Number == 1 && b.TenantId == tenant);
            b1.Name = "first";
            await first.SaveChangesAsync();
            b2.Name = "second";
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

            var c1 = await first.Set<CostCategory>().SingleAsync(c => c.TenantId == tenant);
            var c2 = await second.Set<CostCategory>().SingleAsync(c => c.TenantId == tenant);
            c1.Icon = "drop";
            await first.SaveChangesAsync();
            c2.Icon = "truck";
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

            // xmin ستون واقعی ساخته‌شده نیست
            var userColumns = await db.Database.SqlQuery<int>($"""
                SELECT count(*)::int AS "Value" FROM pg_attribute
                WHERE attrelid IN ('"RugBatches"'::regclass, '"CostCategories"'::regclass)
                  AND attname = 'xmin' AND attnum > 0
                """).SingleAsync();
            Assert.Equal(0, userColumns);
        });
    }

    // ── کمکی‌ها ──────────────────────────────────────────────────

    private sealed record StoredArea(Guid Id, decimal Area, decimal Total, decimal Intake);

    private sealed record LegacyRugRow(Guid Id, decimal LengthMeters, decimal WidthMeters, decimal? PurchaseCost);

    private static (decimal, decimal, decimal) Shape(IEnumerable<LegacyRugRow> rows, Guid id)
    {
        var row = rows.Single(r => r.Id == id);
        return (row.LengthMeters, row.WidthMeters, row.PurchaseCost ?? -1m);
    }

    private static Task<List<StoredArea>> StoredAreasAsync(AppDbContext db)
        => db.Database.SqlQuery<StoredArea>($"""
            SELECT "Id", "AreaM2" AS "Area", "TotalAreaM2" AS "Total", "IntakeAreaM2" AS "Intake" FROM "Rugs"
            """).ToListAsync();

    private static Task<long> CounterAsync(AppDbContext db, Guid tenant)
        => db.Database.SqlQuery<long>(
            $"""SELECT "Value" FROM "TenantCounters" WHERE "TenantId" = {tenant} AND "Key" = 'batch'""").SingleAsync();

    private static async Task<Guid> AddTenantAsync(AppDbContext db)
    {
        var tenant = new Tenant { Name = "IT p2", Slug = "it-p2-" + Guid.NewGuid().ToString("N")[..8] };
        db.Tenants.Add(tenant);
        db.Set<TenantPreferences>().Add(TenantPreferences.CreateDefault(tenant.Id));
        await db.SaveChangesAsync();
        return tenant.Id;
    }

    private static Task<int> InsertRawRugAsync(
        AppDbContext db, Guid tenant, string sku, string serialNo, string from, string to, int quantity)
    {
        // فقط ASCII؛ مقدارهای ثابتِ خودِ آزمون، نه ورودی کاربر
        var sql = $"""
            INSERT INTO "Rugs" ("Id", "TenantId", "Sku", "WidthMeters", "LengthMeters", "IntakeLength", "IntakeWidth",
                "Status", "CurrentStepIndex", "CreatedAt", "SerialNo", "SerialFrom", "SerialTo", "SerialSort", "Quantity")
            VALUES ('{Guid.NewGuid()}', '{tenant}', '{sku}', 1, 1, 1, 1, 0, 0, now(), {serialNo}, {from}, {to}, {from}, {quantity})
            """;
        return db.Database.ExecuteSqlRawAsync(sql);
    }

    private static async Task AddBatchAsync(AppDbContext db, Guid tenant, int number, DateOnly? day, string? sellerKey)
    {
        await using var context = CreateContext(db);
        context.RugBatches.Add(new RugBatch { TenantId = tenant, Number = number, Name = "n" + number, ReceivedOn = day, SellerKey = sellerKey });
        await context.SaveChangesAsync();
    }

    private static async Task AssertSqlStateAsync(string sqlState, Func<Task> action)
    {
        var error = await Assert.ThrowsAnyAsync<Exception>(action);
        var postgres = error as PostgresException ?? error.InnerException as PostgresException;
        Assert.NotNull(postgres);
        Assert.Equal(sqlState, postgres.SqlState);
    }

    private static List<(decimal Length, decimal Width, int Qty, decimal Piece, decimal Line)> PieceAreaVectors()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "math-vectors.json");
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        static decimal D(System.Text.Json.JsonElement e, string name)
            => decimal.Parse(e.GetProperty(name).GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture);

        return document.RootElement.GetProperty("vectors").EnumerateArray()
            .Where(v => v.GetProperty("op").GetString() == "pieceArea")
            .Select(v => (D(v, "length"), D(v, "width"), v.GetProperty("qty").GetInt32(), D(v, "piece"), D(v, "line")))
            .ToList();
    }

    /// <summary>ردیف‌های پیش از M2 با SQL خام (ستون‌های v2 هنوز نیستند).</summary>
    private static async Task SeedLegacyRowsAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "Tenants" ("Id", "Name", "Slug", "IsActive", "Currency", "CreatedAt")
            VALUES ({TenantA}, 'T1', {"t1-" + Guid.NewGuid().ToString("N")[..8]}, true, 0, now()),
                   ({TenantB}, 'T2', {"t2-" + Guid.NewGuid().ToString("N")[..8]}, true, 0, now())
            """);
        // کارگاه B با MoneyScale 2؛ کارگاه A ردیف ترجیحات ندارد (پیش‌فرض ۰ در migration)
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "TenantPreferences" ("TenantId", "SkuPrefix", "SerialMode", "MoneyScale", "MaxPlausibleSideM", "DefaultMethod",
                "DefaultAreaBasis", "DeclaredValueKeyField", "DeclaredValuesStamp", "BaleTargetTolerancePercent", "RequireFinalSizeToFinalize", "BaleTerm")
            VALUES ({TenantB}, 'RUG', 1, 2, 12, 0, 1, 'rug.origin', 0, 10, true, 0)
            """);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "TenantCounters" ("TenantId", "Key", "Value") VALUES ({TenantA}, 'batch', 10)
            """);

        var stepType = Guid.NewGuid();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "ProcessStepTypes" ("Id", "TenantId", "Code", "NameFa", "NameEn", "Icon", "SortOrder", "DefaultPricingModel", "DefaultUnitRate", "IsActive", "CreatedAt")
            VALUES ({stepType}, NULL, {"wash-" + stepType.ToString("N")[..6]}, 'w', 'Wash', 'circle', 0, 1, 100, true, now())
            """);

        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "RugBatches" ("Id", "TenantId", "Name", "CreatedAt")
            VALUES ({Guid.NewGuid()}, {TenantA}, 'late', now()),
                   ({Guid.NewGuid()}, {TenantA}, 'early', now() - interval '2 days'),
                   ({Guid.NewGuid()}, {TenantB}, 'other', now())
            """);

        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "Rugs" ("Id", "TenantId", "Sku", "WidthMeters", "LengthMeters", "PurchaseCost", "Status", "CurrentStepIndex", "CreatedAt", "DeletedAt")
            VALUES ({RugOdd}, {TenantA}, 'RUG-1', 1.005, 2.675, 1000, 1, 0, now(), NULL),
                   ({RugTrashed}, {TenantA}, 'RUG-2', 2.10, 2.08, NULL, 0, 0, now(), now()),
                   ({RugScale2}, {TenantB}, 'RUG-3', 0.4, 0.4, 1234.56, 3, 0, now(), NULL),
                   ({RugZero}, {TenantA}, 'RUG-4', 2.5251, 3.3449, 0, 0, 0, now(), NULL)
            """);

        // Status: Pending 0، InProgress 1، Completed 2، Skipped 3
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "RugWorkflowSteps" ("Id", "RugId", "ProcessStepTypeId", "OrderIndex", "IsOptional", "Status", "CalculatedCost", "ManualCostOverride", "Adjustment", "CreatedAt")
            VALUES ({Guid.NewGuid()}, {RugOdd}, {stepType}, 0, false, 2, 500.25, NULL, -100.25, now()),
                   ({Guid.NewGuid()}, {RugOdd}, {stepType}, 1, false, 1, 999, 300, NULL, now()),
                   ({Guid.NewGuid()}, {RugOdd}, {stepType}, 2, false, 0, 777, NULL, NULL, now()),
                   ({Guid.NewGuid()}, {RugOdd}, {stepType}, 3, true, 3, 50, NULL, NULL, now()),
                   ({Guid.NewGuid()}, {RugOdd}, {stepType}, 4, false, 2, 100, NULL, -500, now())
            """);

        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "RugSales" ("Id", "TenantId", "RugId", "BuyerName", "SalePrice", "Discount", "ReceivedAmount", "PaymentMethod", "SoldAt", "CreatedAt")
            VALUES ({Guid.NewGuid()}, {TenantA}, {RugOdd}, 'buyer', 5000, 0, 5000, 0, '2026-01-02T10:00:00Z', now()),
                   ({Guid.NewGuid()}, {TenantB}, {RugScale2}, 'buyer', 3000, 0, 3000, 0, '2026-02-03T10:00:00Z', now())
            """);
    }

    private static AppDbContext CreateContext(AppDbContext db)
        => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(db.Database.GetConnectionString()!).Options);

    private static async Task<bool> TableExistsAsync(AppDbContext db, string table)
        => await db.Database.SqlQuery<bool>($"""
            SELECT EXISTS (SELECT 1 FROM pg_catalog.pg_tables WHERE schemaname = current_schema() AND tablename = {table}) AS "Value"
            """).SingleAsync();

    private static async Task<bool> ColumnExistsAsync(AppDbContext db, string table, string column)
        => await db.Database.SqlQuery<bool>($"""
            SELECT EXISTS (SELECT 1 FROM information_schema.columns
                           WHERE table_schema = current_schema() AND table_name = {table} AND column_name = {column}) AS "Value"
            """).SingleAsync();

    private static async Task WithDatabaseAsync(Func<AppDbContext, Task> test)
    {
        var name = "rugs_p2_dom_" + Guid.NewGuid().ToString("N")[..8];
        var builder = new NpgsqlConnectionStringBuilder(PostgresTestEnvironment.RequireBaseConnectionString()) { Database = name };
        await ExecuteOnMaintenanceAsync($"CREATE DATABASE \"{name}\"");
        try
        {
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(builder.ConnectionString).Options);
            await test(db);
        }
        finally
        {
            await using (var connection = new NpgsqlConnection(builder.ConnectionString))
                NpgsqlConnection.ClearPool(connection);
            await ExecuteOnMaintenanceAsync($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
        }
    }

    private static async Task ExecuteOnMaintenanceAsync(string sql)
    {
        var maintenance = new NpgsqlConnectionStringBuilder(PostgresTestEnvironment.RequireBaseConnectionString())
        {
            Database = "postgres",
            Pooling = false
        };

        await using var connection = new NpgsqlConnection(maintenance.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
