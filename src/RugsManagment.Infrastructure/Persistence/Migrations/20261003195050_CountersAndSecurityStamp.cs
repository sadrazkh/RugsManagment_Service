using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RugsManagment.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// M0: جدول شمارنده‌های اتمیک هر کارگاه + مهر امنیتی کاربر.
    ///
    /// مهر امنیتی سه مرحله دارد: اول ستون nullable ساخته می‌شود، بعد برای هر کاربرِ موجود
    /// یک مقدار تصادفیِ متفاوت پر می‌شود و آخر NOT NULL می‌شود. اگر مثل خروجی پیش‌فرض EF با
    /// defaultValue خالی ساخته می‌شد، همهٔ کاربران قدیمی مهر یکسانِ «» می‌گرفتند و عملاً مهری نداشتند.
    /// کوکی‌های قدیمی این مهر را ندارند، پس بعد از این migration همه یک بار دوباره وارد می‌شوند.
    ///
    /// xmin عمداً اینجا نیست: ستون سیستمی پستگرس است و ساختنش خطا می‌دهد (ConcurrencyAndStepIndex).
    /// </summary>
    public partial class CountersAndSecurityStamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "Users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql("""UPDATE "Users" SET "SecurityStamp" = md5(random()::text || "Id"::text);""");

            migrationBuilder.Sql("""ALTER TABLE "Users" ALTER COLUMN "SecurityStamp" SET NOT NULL;""");

            migrationBuilder.CreateTable(
                name: "TenantCounters",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantCounters", x => new { x.TenantId, x.Key });
                    table.ForeignKey(
                        name: "FK_TenantCounters_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantCounters");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "Users");
        }
    }
}
