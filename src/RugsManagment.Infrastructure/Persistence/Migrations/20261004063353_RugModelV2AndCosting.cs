using System;
using Microsoft.EntityFrameworkCore.Migrations;
using RugsManagment.Infrastructure.Persistence.Seed;

#nullable disable

namespace RugsManagment.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// M2: فرش نسخهٔ ۲ و هزینه‌ها (spec B.4–B.8) — شماره و تعداد، اندازهٔ اول و نهایی، مساحت‌های تولیدشده، شمارهٔ ورودی و
    /// فروشنده، عکس بهای فروش، عکس اندازهٔ قیمت‌گذاری مرحله، و جدول‌های سطر خرید، نوع هزینه و جمع هزینه.
    ///
    /// <c>[RequiresBackup("Rugs")]</c>: ابعاد قدیمی (numeric بی‌دقت) به دو رقم گرد می‌شوند؛ اصلشان در
    /// <c>_m2_rug_dims_backup</c> می‌ماند، ولی روی دیتابیس پُر استارت تا تأیید پشتیبان متوقف می‌شود.
    ///
    /// ویرایش‌های دستی (فهرست کامل و دلیل هر کدام در <c>Migrations/HandEdits/RugModelV2AndCosting.md</c>، برای بازسازی طبق
    /// قاعدهٔ «regenerate»):
    /// <list type="number">
    /// <item>ترتیب ابعاد طبق B.4: پشتیبان ← گرد کردن ← ALTER به numeric(9,2) ← اندازهٔ اول از جفت گردشده ← ستون‌های تولیدشده.</item>
    /// <item>شمارهٔ ورودی‌های قدیمی به ترتیب ساخت + شمارندهٔ «batch»، پیش از ایندکس یکتا.</item>
    /// <item>قیمت خرید قدیمی ← یک سطر خرید «کل» با برچسب «قیمت خرید» برای هر فرش.</item>
    /// <item>بررسی خودکار (self-verify) در همان تراکنش، و پر کردن عکس بهای فروش‌های موجود.</item>
    /// <item>ستون سیستمی xmin از AddColumn/CreateTable/DropColumn حذف شد.</item>
    /// <item>Down: قیمت خرید از جمع سطرها برمی‌گردد، ستون‌های تولیدشده پیش از ALTER حذف می‌شوند و ابعادِ دست‌نخورده از
    /// پشتیبان برمی‌گردند؛ جدول پشتیبان فقط با M7 حذف می‌شود.</item>
    /// </list>
    /// ردیف‌های RugCostTotal را این migration نمی‌سازد؛ بررسی استارت دفتر (LedgerStartupCheck) آن‌ها را از نو می‌سازد.
    /// </summary>
    [RequiresBackup("Rugs")]
    public partial class RugModelV2AndCosting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ═════════════════════════════════════════════════════════════════
            // ۱. ابعاد فرش، دقیقاً به ترتیب B.4 «M2 dimension order»
            // ═════════════════════════════════════════════════════════════════

            // ۱-۱) اصل ابعاد، پیش از هر تغییر. IF NOT EXISTS و ON CONFLICT: بعد از Down جدول می‌ماند و اجرای دوباره
            // باید همان ابعادِ پیش از این اجرا را نگه دارد. کلید خارجی ندارد تا حذف سخت فرش (برگشت ورود) را نبندد.
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "_m2_rug_dims_backup" (
                    "RugId" uuid NOT NULL PRIMARY KEY,
                    "Length" numeric NOT NULL,
                    "Width" numeric NOT NULL
                );
                """);

            migrationBuilder.Sql("""
                INSERT INTO "_m2_rug_dims_backup" ("RugId", "Length", "Width")
                SELECT "Id", "LengthMeters", "WidthMeters" FROM "Rugs"
                ON CONFLICT ("RugId") DO UPDATE SET "Length" = EXCLUDED."Length", "Width" = EXCLUDED."Width";
                """);

            // تعداد فرش‌هایی که با گرد کردن عوض می‌شوند: در NOTICE و در توضیح جدول پشتیبان (پایدار، قابل خواندن بعدی)
            migrationBuilder.Sql("""
                DO $$
                DECLARE changed integer;
                BEGIN
                    SELECT count(*) INTO changed FROM "Rugs"
                    WHERE "LengthMeters" <> round("LengthMeters", 2) OR "WidthMeters" <> round("WidthMeters", 2);
                    RAISE NOTICE 'M2: % rug(s) change when their dimensions are rounded to 2 decimals (originals kept in _m2_rug_dims_backup)', changed;
                    EXECUTE format('COMMENT ON TABLE "_m2_rug_dims_backup" IS %L',
                        format('M2 RugModelV2AndCosting: original Rugs.LengthMeters/WidthMeters; %s rug(s) changed on rounding to 2 decimals (%s)', changed, now()));
                END $$;
                """);

            // ۱-۲) گرد کردن (round(numeric) در پستگرس نصفه را دور از صفر می‌برد، همان Num.Round)
            migrationBuilder.Sql("""
                UPDATE "Rugs" SET "LengthMeters" = round("LengthMeters", 2), "WidthMeters" = round("WidthMeters", 2)
                WHERE "LengthMeters" <> round("LengthMeters", 2) OR "WidthMeters" <> round("WidthMeters", 2);
                """);

            // ۱-۳) numeric(9,2)
            migrationBuilder.AlterColumn<decimal>(
                name: "WidthMeters",
                table: "Rugs",
                type: "numeric(9,2)",
                precision: 9,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "LengthMeters",
                table: "Rugs",
                type: "numeric(9,2)",
                precision: 9,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            // ۱-۴) اندازهٔ اول = جفت گردشده (هیچ فرش قدیمی اندازهٔ نهایی ندارد)
            migrationBuilder.AddColumn<decimal>(
                name: "IntakeLength",
                table: "Rugs",
                type: "numeric(9,2)",
                precision: 9,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "IntakeWidth",
                table: "Rugs",
                type: "numeric(9,2)",
                precision: 9,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("""
                UPDATE "Rugs" SET "IntakeLength" = "LengthMeters", "IntakeWidth" = "WidthMeters";
                """);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalLength",
                table: "Rugs",
                type: "numeric(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalWidth",
                table: "Rugs",
                type: "numeric(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FinalMeasuredAt",
                table: "Rugs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinalMeasuredBy",
                table: "Rugs",
                type: "uuid",
                nullable: true);

            // تعداد پیش از ستون تولیدشدهٔ TotalAreaM2 که به آن وابسته است
            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "Rugs",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // ۱-۵) ستون‌های تولیدشده، بعد از ALTER نوع (پستگرس نوعِ ستونی را که ستون تولیدشده به آن وابسته است عوض نمی‌کند)
            migrationBuilder.AddColumn<decimal>(
                name: "AreaM2",
                table: "Rugs",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                computedColumnSql: "round(\"LengthMeters\" * \"WidthMeters\", 2)",
                stored: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IntakeAreaM2",
                table: "Rugs",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                computedColumnSql: "round(\"IntakeLength\" * \"IntakeWidth\", 2)",
                stored: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAreaM2",
                table: "Rugs",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                computedColumnSql: "round(\"LengthMeters\" * \"WidthMeters\", 2) * \"Quantity\"",
                stored: true);

            // ═════════════════════════════════════════════════════════════════
            // ۲. شماره، شناسهٔ ردیف جدول ورود، قیدها و ایندکس‌های فرش
            // ═════════════════════════════════════════════════════════════════

            migrationBuilder.AddColumn<string>(
                name: "SerialNo",
                table: "Rugs",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SerialFrom",
                table: "Rugs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SerialTo",
                table: "Rugs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SerialSort",
                table: "Rugs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientRowId",
                table: "Rugs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rugs_TenantId_ClientRowId",
                table: "Rugs",
                columns: new[] { "TenantId", "ClientRowId" },
                unique: true,
                filter: "\"ClientRowId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Rugs_TenantId_SerialFrom_SerialTo",
                table: "Rugs",
                columns: new[] { "TenantId", "SerialFrom", "SerialTo" },
                filter: "\"DeletedAt\" IS NULL AND \"SerialFrom\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Rugs_TenantId_SerialNo",
                table: "Rugs",
                columns: new[] { "TenantId", "SerialNo" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL AND \"SerialNo\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rugs_FinalSize",
                table: "Rugs",
                sql: "(\"FinalLength\" IS NULL) = (\"FinalWidth\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rugs_Quantity",
                table: "Rugs",
                sql: "\"Quantity\" >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rugs_SerialRange",
                table: "Rugs",
                sql: "(\"SerialFrom\" IS NULL AND \"SerialTo\" IS NULL) OR (\"SerialFrom\" IS NOT NULL AND \"SerialTo\" IS NOT NULL AND \"SerialTo\"::bigint - \"SerialFrom\" + 1 >= \"Quantity\")");

            // ═════════════════════════════════════════════════════════════════
            // ۳. ورودی (RugBatch): شماره، فروشنده، روز — شماره‌ها پیش از ایندکس یکتا پر می‌شوند
            // ═════════════════════════════════════════════════════════════════

            migrationBuilder.DropIndex(
                name: "IX_RugBatches_TenantId",
                table: "RugBatches");

            migrationBuilder.AddColumn<int>(
                name: "Number",
                table: "RugBatches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReceivedOn",
                table: "RugBatches",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellerKey",
                table: "RugBatches",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SellerName",
                table: "RugBatches",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            // ویرایش دستی: ورودی‌های موجود به ترتیب ساخت در هر کارگاه ۱، ۲، ۳، … می‌گیرند؛ فروشنده ندارند (کلید خالی)
            // و روزشان خالی می‌ماند تا هیچ‌کدام در کلید «یک ورودی برای هر فروشنده در هر روز» با دیگری برخورد نکند.
            migrationBuilder.Sql("""
                UPDATE "RugBatches" b SET "Number" = n.rn, "SellerKey" = ''
                FROM (
                    SELECT "Id", row_number() OVER (PARTITION BY "TenantId" ORDER BY "CreatedAt", "Id") AS rn
                    FROM "RugBatches"
                ) n
                WHERE n."Id" = b."Id";
                """);

            // شمارندهٔ «batch» از بزرگ‌ترین شماره ادامه می‌دهد (اگر از پیش جلوتر است، عقب نمی‌رود)
            migrationBuilder.Sql("""
                INSERT INTO "TenantCounters" ("TenantId", "Key", "Value")
                SELECT "TenantId", 'batch', max("Number") FROM "RugBatches" GROUP BY "TenantId"
                ON CONFLICT ("TenantId", "Key") DO UPDATE
                    SET "Value" = GREATEST("TenantCounters"."Value", EXCLUDED."Value");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_RugBatches_TenantId_Number",
                table: "RugBatches",
                columns: new[] { "TenantId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RugBatches_TenantId_ReceivedOn_SellerKey",
                table: "RugBatches",
                columns: new[] { "TenantId", "ReceivedOn", "SellerKey" },
                unique: true,
                filter: "\"ReceivedOn\" IS NOT NULL")
                .Annotation("Npgsql:NullsDistinct", false);

            // ═════════════════════════════════════════════════════════════════
            // ۴. فروش و مرحله
            // ═════════════════════════════════════════════════════════════════

            migrationBuilder.AddColumn<decimal>(
                name: "CostAtSale",
                table: "RugSales",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CostAtSaleAt",
                table: "RugSales",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PricedLength",
                table: "RugWorkflowSteps",
                type: "numeric(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PricedWidth",
                table: "RugWorkflowSteps",
                type: "numeric(9,2)",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PricedQuantity",
                table: "RugWorkflowSteps",
                type: "integer",
                nullable: true);

            // ═════════════════════════════════════════════════════════════════
            // ۵. جدول‌های تازه
            // ═════════════════════════════════════════════════════════════════

            migrationBuilder.CreateTable(
                name: "CostCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeedKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Group = table.Column<int>(type: "integer", nullable: false),
                    Icon = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultBasis = table.Column<int>(type: "integer", nullable: false),
                    DefaultRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    DefaultCurrency = table.Column<int>(type: "integer", nullable: true),
                    DefaultMethod = table.Column<int>(type: "integer", nullable: false),
                    DefaultAreaBasis = table.Column<int>(type: "integer", nullable: false),
                    DefaultScope = table.Column<int>(type: "integer", nullable: true),
                    ShowInIntake = table.Column<bool>(type: "boolean", nullable: false),
                    StepTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    // ویرایش دستی: ستون xmin حذف شد (ستون سیستمی پستگرس)
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostCategories_ProcessStepTypes_StepTypeId",
                        column: x => x.StepTypeId,
                        principalTable: "ProcessStepTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CostCategories_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RugCostTotals",
                columns: table => new
                {
                    RugId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    StepCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpenseCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CostPerM2 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CostPerPiece = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExportRevenueShare = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ComputedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LedgerVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RugCostTotals", x => x.RugId);
                    table.ForeignKey(
                        name: "FK_RugCostTotals_Rugs_RugId",
                        column: x => x.RugId,
                        principalTable: "Rugs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RugCostTotals_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RugPurchaseLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RugId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Basis = table.Column<int>(type: "integer", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RugPurchaseLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RugPurchaseLines_CostCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "CostCategories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RugPurchaseLines_Rugs_RugId",
                        column: x => x.RugId,
                        principalTable: "Rugs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RugPurchaseLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CostCategories_StepTypeId",
                table: "CostCategories",
                column: "StepTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CostCategories_TenantId_Group_SortOrder",
                table: "CostCategories",
                columns: new[] { "TenantId", "Group", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CostCategories_TenantId_SeedKey",
                table: "CostCategories",
                columns: new[] { "TenantId", "SeedKey" },
                unique: true,
                filter: "\"SeedKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RugCostTotals_TenantId",
                table: "RugCostTotals",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RugPurchaseLines_CategoryId",
                table: "RugPurchaseLines",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RugPurchaseLines_RugId_SortOrder",
                table: "RugPurchaseLines",
                columns: new[] { "RugId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_RugPurchaseLines_TenantId_CategoryId",
                table: "RugPurchaseLines",
                columns: new[] { "TenantId", "CategoryId" });

            // ═════════════════════════════════════════════════════════════════
            // ۶. قیمت خرید قدیمی ← یک سطر خرید «کل» برای هر فرش (حتی فرش سطل‌زباله، که با برگشتنش قیمتش هم برگردد)
            // ═════════════════════════════════════════════════════════════════

            // Basis 0 = Total. نرخ همان مبلغ قدیمی (numeric(18,4))؛ مبلغ = ROUND(نرخ، MoneyScale کارگاه)، همان قاعدهٔ
            // «سطر خرید کل» که PurchaseLineStage بعداً به کار می‌برد. بی نوع هزینه: نوع‌های seed بعد از migration ساخته می‌شوند.
            migrationBuilder.Sql("""
                INSERT INTO "RugPurchaseLines"
                    ("Id", "TenantId", "RugId", "CategoryId", "Label", "Basis", "Rate", "Amount", "SortOrder", "Notes", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), r."TenantId", r."Id", NULL, 'قیمت خرید', 0,
                       round(r."PurchaseCost", 4),
                       round(round(r."PurchaseCost", 4), COALESCE(p."MoneyScale", 0)::integer),
                       0, NULL, now(), NULL
                FROM "Rugs" r
                LEFT JOIN "TenantPreferences" p ON p."TenantId" = r."TenantId"
                WHERE r."PurchaseCost" IS NOT NULL;
                """);

            // ═════════════════════════════════════════════════════════════════
            // ۷. بررسی خودکار (self-verify) در همان تراکنش، و عکس بهای فروش‌های موجود (D.6)
            // ═════════════════════════════════════════════════════════════════

            // جمع تازه = سطرهای خرید + هزینهٔ مؤثر مراحل انجام‌شده/در حال انجام (Status 1، 2)، هر مرحله کف صفر.
            // جمع قدیم = همان فرمول TotalInvestment پیش از M2: قیمت خرید + همان مراحل. فرمول مرحله این‌جا عمداً
            // تکرار شده: migration تاریخی است و نباید با تغییر بعدیِ CostSql عوض شود.
            migrationBuilder.Sql("""
                CREATE TEMP TABLE "_m2_totals" ON COMMIT DROP AS
                SELECT r."Id" AS "RugId", r."Sku",
                       COALESCE(r."PurchaseCost", 0) + COALESCE(s.step_cost, 0) AS old_total,
                       COALESCE(l.lines_cost, 0) + COALESCE(s.step_cost, 0) AS new_total
                FROM "Rugs" r
                LEFT JOIN (
                    SELECT "RugId",
                           sum(GREATEST(0, COALESCE("ManualCostOverride", "CalculatedCost", 0) + COALESCE("Adjustment", 0))) AS step_cost
                    FROM "RugWorkflowSteps"
                    WHERE "Status" IN (1, 2)
                    GROUP BY "RugId"
                ) s ON s."RugId" = r."Id"
                LEFT JOIN (
                    SELECT "RugId", sum("Amount") AS lines_cost
                    FROM "RugPurchaseLines"
                    GROUP BY "RugId"
                ) l ON l."RugId" = r."Id";
                """);

            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    bad_dims integer;
                    bad_totals integer;
                    sample text;
                BEGIN
                    SELECT count(*) INTO bad_dims FROM "Rugs"
                    WHERE "LengthMeters" <> COALESCE("FinalLength", "IntakeLength")
                       OR "WidthMeters" <> COALESCE("FinalWidth", "IntakeWidth");
                    IF bad_dims > 0 THEN
                        RAISE EXCEPTION 'M2 self-verify failed: % rug(s) have an effective size that differs from COALESCE(final, intake)', bad_dims;
                    END IF;

                    SELECT count(*),
                           string_agg(format('%s (old %s, new %s)', "Sku", old_total, new_total), '; ' ORDER BY "Sku")
                               FILTER (WHERE rn <= 5)
                    INTO bad_totals, sample
                    FROM (
                        SELECT "Sku", old_total, new_total, row_number() OVER (ORDER BY "Sku") AS rn
                        FROM "_m2_totals"
                        WHERE old_total <> new_total
                    ) mismatches;
                    IF bad_totals > 0 THEN
                        RAISE EXCEPTION 'M2 self-verify failed: the new total differs from the old TotalInvestment for % rug(s): %', bad_totals, sample
                            USING HINT = 'Usually a purchase cost with more decimals than the tenant MoneyScale allows. Fix those rugs on the old version, then migrate again.';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                UPDATE "RugSales" s SET "CostAtSale" = t.new_total, "CostAtSaleAt" = s."SoldAt"
                FROM "_m2_totals" t
                WHERE t."RugId" = s."RugId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ویرایش دستی: پیش از حذف سطرهای خرید، قیمت خرید قدیمی از جمع آن‌ها برمی‌گردد (ویرایش‌های بعد از M2 گم نشود)،
            // برای همهٔ فرش‌ها. فرش بی‌سطر NULL می‌گیرد: Up هر قیمت غیر NULL را سطر کرد و بعد از M2 هیچ کدی ستون را نمی‌نویسد،
            // پس فرش بی‌سطر یا پیش از M2 هم NULL بود یا کاربر سطرهایش را پاک کرده — نگه داشتن ستون قیمت پاک‌شده را برمی‌گرداند.
            migrationBuilder.Sql("""
                UPDATE "Rugs" r SET "PurchaseCost" =
                    (SELECT sum(l."Amount") FROM "RugPurchaseLines" l WHERE l."RugId" = r."Id");
                """);

            migrationBuilder.DropTable(
                name: "RugCostTotals");

            migrationBuilder.DropTable(
                name: "RugPurchaseLines");

            migrationBuilder.DropTable(
                name: "CostCategories");

            migrationBuilder.DropIndex(
                name: "IX_Rugs_TenantId_ClientRowId",
                table: "Rugs");

            migrationBuilder.DropIndex(
                name: "IX_Rugs_TenantId_SerialFrom_SerialTo",
                table: "Rugs");

            migrationBuilder.DropIndex(
                name: "IX_Rugs_TenantId_SerialNo",
                table: "Rugs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rugs_FinalSize",
                table: "Rugs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rugs_Quantity",
                table: "Rugs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rugs_SerialRange",
                table: "Rugs");

            migrationBuilder.DropIndex(
                name: "IX_RugBatches_TenantId_Number",
                table: "RugBatches");

            migrationBuilder.DropIndex(
                name: "IX_RugBatches_TenantId_ReceivedOn_SellerKey",
                table: "RugBatches");

            // ویرایش دستی: شمارهٔ ورودی‌ها با ستونشان می‌رود، پس شمارنده‌اش هم
            migrationBuilder.Sql("""
                DELETE FROM "TenantCounters" WHERE "Key" = 'batch';
                """);

            // ستون‌های تولیدشده پیش از ALTER نوعِ ابعاد حذف می‌شوند (پستگرس نوعِ ستونِ وابسته را عوض نمی‌کند)
            migrationBuilder.DropColumn(
                name: "AreaM2",
                table: "Rugs");

            migrationBuilder.DropColumn(
                name: "IntakeAreaM2",
                table: "Rugs");

            migrationBuilder.DropColumn(
                name: "TotalAreaM2",
                table: "Rugs");

            migrationBuilder.DropColumn(
                name: "PricedLength",
                table: "RugWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "PricedQuantity",
                table: "RugWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "PricedWidth",
                table: "RugWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "CostAtSale",
                table: "RugSales");

            migrationBuilder.DropColumn(
                name: "CostAtSaleAt",
                table: "RugSales");

            migrationBuilder.DropColumn(
                name: "ClientRowId",
                table: "Rugs");

            migrationBuilder.DropColumn(
                name: "FinalLength",
                table: "Rugs");

            migrationBuilder.DropColumn(
                name: "FinalMeasuredAt",
                table: "Rugs");

            migrationBuilder.DropColumn(
                name: "FinalMeasuredBy",
                table: "Rugs");

            migrationBuilder.DropColumn(
                name: "FinalWidth",
                table: "Rugs");

            migrationBuilder.DropColumn(
                name: "IntakeLength",
                table: "Rugs");

            migrationBuilder.DropColumn(
                name: "IntakeWidth",
                table: "Rugs");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "Rugs");

            migrationBuilder.DropColumn(
                name: "SerialFrom",
                table: "Rugs");

            migrationBuilder.DropColumn(
                name: "SerialNo",
                table: "Rugs");

            migrationBuilder.DropColumn(
                name: "SerialSort",
                table: "Rugs");

            migrationBuilder.DropColumn(
                name: "SerialTo",
                table: "Rugs");

            migrationBuilder.DropColumn(
                name: "Number",
                table: "RugBatches");

            migrationBuilder.DropColumn(
                name: "ReceivedOn",
                table: "RugBatches");

            migrationBuilder.DropColumn(
                name: "SellerKey",
                table: "RugBatches");

            migrationBuilder.DropColumn(
                name: "SellerName",
                table: "RugBatches");

            // ویرایش دستی: DropColumn ستون سیستمی xmin حذف شد

            migrationBuilder.AlterColumn<decimal>(
                name: "WidthMeters",
                table: "Rugs",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(9,2)",
                oldPrecision: 9,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "LengthMeters",
                table: "Rugs",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(9,2)",
                oldPrecision: 9,
                oldScale: 2);

            // ویرایش دستی: ابعاد اصلی از پشتیبان، فقط برای فرشی که بعد از M2 اندازه‌اش عوض نشده (جفت فعلی هنوز همان
            // گردشدهٔ اصل است)؛ اندازه‌ای که کاربر بعد از M2 نوشته (از جمله اندازهٔ نهایی) نگه داشته می‌شود.
            migrationBuilder.Sql("""
                UPDATE "Rugs" r SET "LengthMeters" = b."Length", "WidthMeters" = b."Width"
                FROM "_m2_rug_dims_backup" b
                WHERE b."RugId" = r."Id"
                  AND r."LengthMeters" = round(b."Length", 2)
                  AND r."WidthMeters" = round(b."Width", 2);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_RugBatches_TenantId",
                table: "RugBatches",
                column: "TenantId");
        }
    }
}
