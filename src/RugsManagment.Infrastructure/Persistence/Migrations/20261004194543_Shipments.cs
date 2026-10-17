using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RugsManagment.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// M3: محموله‌ها (spec B.9، B.13) — محموله، نگله، خط (عضویت فرش در نگله)، ارزش گمرکی مشتق‌شدهٔ خط و جدول ارزش گمرکی.
    /// فقط جدول تازه می‌سازد و به دادهٔ موجود دست نمی‌زند، پس پشتیبان لازم ندارد.
    ///
    /// ویرایش‌های دستی (فهرست کامل و دلیل هر کدام در <c>Migrations/HandEdits/Shipments.md</c>، برای بازسازی طبق قاعدهٔ
    /// «regenerate»):
    /// <list type="number">
    /// <item>ستون سیستمی xmin از CreateTable جدول‌های Shipments، Bales و ShipmentLines حذف شد.</item>
    /// <item>قید یکتای «تعویق‌پذیر» <c>UX_Bales_Shipment_Number</c> روی (ShipmentId, Number) با SQL دستی، به‌جای ایندکس یکتا.</item>
    /// </list>
    /// ردیف‌های ShipmentLineValues را فقط پاس سازگاری می‌نویسد (مرحلهٔ ارزش خط، ترتیب ۳۰۰).
    /// </summary>
    public partial class Shipments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeclaredValueRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyValue = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    KeyNormalized = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    UsdPerM2 = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeclaredValueRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeclaredValueRates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Shipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Destination = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BuyerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DeclarationNo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    CustomsOffice = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FinalizeCount = table.Column<int>(type: "integer", nullable: false),
                    FinalizedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinalizedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    SnapshotJson = table.Column<string>(type: "text", nullable: true),
                    SnapshotHash = table.Column<string>(type: "character(64)", maxLength: 64, nullable: true),
                    LastSnapshotJson = table.Column<string>(type: "text", nullable: true),
                    TargetBaleAreaM2 = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    UsdRate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    RevenueAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    RevenueCurrency = table.Column<int>(type: "integer", nullable: true),
                    RevenueFx = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    // ویرایش دستی: ستون xmin حذف شد (ستون سیستمی پستگرس؛ HandEdits/Shipments.md)
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shipments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    WeightKg = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    // ویرایش دستی: ستون xmin حذف شد (ستون سیستمی پستگرس؛ HandEdits/Shipments.md)
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bales_Shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "Shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Bales_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ویرایش دستی (HandEdits/Shipments.md): شمارهٔ نگله در محموله یکتاست، ولی با قید «تعویق‌پذیر» به‌جای ایندکس
            // یکتای مدل، تا شماره‌گذاری دوباره و جابه‌جایی دو شماره در یک flush انجام شود (B.9). یکتایی در پایان تراکنش
            // سنجیده می‌شود. مدل این قید را نمی‌شناسد و DropTable در Down آن را با جدول می‌برد.
            migrationBuilder.Sql("""
                ALTER TABLE "Bales"
                    ADD CONSTRAINT "UX_Bales_Shipment_Number" UNIQUE ("ShipmentId", "Number") DEFERRABLE INITIALLY DEFERRED;
                """);

            migrationBuilder.CreateTable(
                name: "ShipmentLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    RugId = table.Column<Guid>(type: "uuid", nullable: false),
                    Seq = table.Column<int>(type: "integer", nullable: false),
                    UsdPerM2Override = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    // ویرایش دستی: ستون xmin حذف شد (ستون سیستمی پستگرس؛ HandEdits/Shipments.md)
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentLines_Bales_BaleId",
                        column: x => x.BaleId,
                        principalTable: "Bales",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ShipmentLines_Rugs_RugId",
                        column: x => x.RugId,
                        principalTable: "Rugs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ShipmentLines_Shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "Shipments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ShipmentLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentLineValues",
                columns: table => new
                {
                    LineId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsdPerM2 = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    RateSource = table.Column<int>(type: "integer", nullable: false),
                    PieceUsd = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    LineAreaM2 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    LineUsd = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    ComputedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentLineValues", x => x.LineId);
                    table.ForeignKey(
                        name: "FK_ShipmentLineValues_ShipmentLines_LineId",
                        column: x => x.LineId,
                        principalTable: "ShipmentLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShipmentLineValues_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bales_ShipmentId",
                table: "Bales",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Bales_TenantId_ShipmentId",
                table: "Bales",
                columns: new[] { "TenantId", "ShipmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_DeclaredValueRates_TenantId_KeyNormalized",
                table: "DeclaredValueRates",
                columns: new[] { "TenantId", "KeyNormalized" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentLines_BaleId_Seq",
                table: "ShipmentLines",
                columns: new[] { "BaleId", "Seq" });

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentLines_RugId",
                table: "ShipmentLines",
                column: "RugId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentLines_ShipmentId",
                table: "ShipmentLines",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentLines_TenantId_ShipmentId",
                table: "ShipmentLines",
                columns: new[] { "TenantId", "ShipmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentLineValues_TenantId_ShipmentId",
                table: "ShipmentLineValues",
                columns: new[] { "TenantId", "ShipmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_TenantId_Number",
                table: "Shipments",
                columns: new[] { "TenantId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_TenantId_Status",
                table: "Shipments",
                columns: new[] { "TenantId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeclaredValueRates");

            migrationBuilder.DropTable(
                name: "ShipmentLineValues");

            migrationBuilder.DropTable(
                name: "ShipmentLines");

            migrationBuilder.DropTable(
                name: "Bales");

            migrationBuilder.DropTable(
                name: "Shipments");
        }
    }
}
