using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RugsManagment.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// M1: جدول‌های سکو (ترجیحات کارگاه، چیدمان جدول‌ها)، ترجیحات کاربر، جزئیات تاریخچه و ستون‌های تازهٔ
    /// فیلد سفارشی.
    ///
    /// ویرایش‌های دستی (فهرست کاملش در <c>Migrations/HandEdits/PlatformPreferences.md</c>، برای بازسازی طبق
    /// قاعدهٔ «regenerate»):
    /// <list type="number">
    /// <item>ستون xmin از CreateTable هر دو جدول حذف شده: ستون سیستمی پستگرس است و ساختنش خطا می‌دهد؛ مدل
    /// (snapshot) همچنان آن را توکن هم‌زمانی می‌شناسد (همان الگوی ConcurrencyAndStepIndex).</item>
    /// <item>کاربران موجود پیش‌فرض نقش خود را می‌گیرند، دقیقاً مثل <c>User.ApplyRoleDefaultPreferences</c>:
    /// بازرگان «ساده + بزرگ»، انباردار «ساده + معمولی»، بقیه «کامل + معمولی». ستون‌ها اول با ۰ ساخته می‌شوند
    /// و بعد با UPDATE پر، چون پیش‌فرض یکسان برای همه (ساده) تنظیم‌کنندهٔ فعلی را به حالت ساده می‌برد.</item>
    /// <item>برای هر کارگاه موجود یک ردیف ترجیحات با پیش‌فرض‌های <c>TenantPreferences.CreateDefault</c>
    /// ساخته می‌شود (ON CONFLICT DO NOTHING، پس اجرای دوباره بی‌اثر است). کارگاه‌های بعدی را TenantSeedRunner
    /// می‌سازد.</item>
    /// <item>Exportable برای فیلدهای موجود true است (پیش‌فرض ستون) — همان پیش‌فرض مدل.</item>
    /// </list>
    /// Down داده‌ای از دست نمی‌دهد که پیش از این migration وجود داشته است.
    /// </summary>
    public partial class PlatformPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoachDismissedJson",
                table: "Users",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GridEnter",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TextScale",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UiMode",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // ویرایش دستی ۲ — پیش‌فرض نقش (UserRole: TenantAdmin = 1، Operator = 2، Merchant = 3؛
            // UiMode: Simple = 0، Full = 1؛ TextScale: Normal = 0، Large = 1)
            migrationBuilder.Sql("""
                UPDATE "Users" SET
                    "UiMode" = CASE WHEN "Role" IN (2, 3) THEN 0 ELSE 1 END,
                    "TextScale" = CASE WHEN "Role" = 3 THEN 1 ELSE 0 END;
                """);

            migrationBuilder.AddColumn<bool>(
                name: "Exportable",
                table: "CustomFieldDefinitions",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "GridOrder",
                table: "CustomFieldDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Scope",
                table: "CustomFieldDefinitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ShowInGrid",
                table: "CustomFieldDefinitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DetailsJson",
                table: "AuditEntries",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TenantGridLayouts",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    GridId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ColumnsJson = table.Column<string>(type: "jsonb", nullable: false)
                    // ویرایش دستی ۱: ستون xmin حذف شد (ستون سیستمی پستگرس)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantGridLayouts", x => new { x.TenantId, x.GridId });
                    table.ForeignKey(
                        name: "FK_TenantGridLayouts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantPreferences",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkuPrefix = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    SerialMode = table.Column<int>(type: "integer", nullable: false),
                    SerialStart = table.Column<int>(type: "integer", nullable: true),
                    MoneyScale = table.Column<short>(type: "smallint", nullable: false),
                    MaxPlausibleSideM = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    DefaultUsdFx = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    DefaultMethod = table.Column<int>(type: "integer", nullable: false),
                    DefaultAreaBasis = table.Column<int>(type: "integer", nullable: false),
                    DeclaredValueKeyField = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    DeclaredValueFallbackUsd = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    DeclaredValuesStamp = table.Column<int>(type: "integer", nullable: false),
                    DefaultBaleTargetAreaM2 = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    BaleTargetTolerancePercent = table.Column<short>(type: "smallint", nullable: false),
                    RequireFinalSizeToFinalize = table.Column<bool>(type: "boolean", nullable: false),
                    BaleTerm = table.Column<int>(type: "integer", nullable: false),
                    DefaultDeclarationTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExporterName = table.Column<string>(type: "text", nullable: true),
                    ExporterCode = table.Column<string>(type: "text", nullable: true),
                    ExporterAddress = table.Column<string>(type: "text", nullable: true),
                    ExporterPhone = table.Column<string>(type: "text", nullable: true),
                    CustomsOffice = table.Column<string>(type: "text", nullable: true)
                    // ویرایش دستی ۱: ستون xmin حذف شد (ستون سیستمی پستگرس)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantPreferences", x => x.TenantId);
                    table.ForeignKey(
                        name: "FK_TenantPreferences_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ویرایش دستی ۳ — یک ردیف ترجیحات برای هر کارگاه موجود، برابر TenantPreferences.CreateDefault:
            // SkuPrefix «RUG»، SerialMode = SuggestNext (1)، MoneyScale 0، MaxPlausibleSideM 12،
            // DefaultMethod = ByArea (0)، DefaultAreaBasis = Effective (1)، کلید ارزش گمرکی «rug.origin»،
            // DeclaredValuesStamp 0، تلورانس ۱۰٪، اندازهٔ نهایی پیش از قطعی = بله، BaleTerm = Negleh (0)؛
            // بقیه null. مقدارها عمداً ثابت نوشته شده‌اند تا این migration با تغییر بعدی کد عوض نشود.
            migrationBuilder.Sql("""
                INSERT INTO "TenantPreferences" (
                    "TenantId", "SkuPrefix", "SerialMode", "SerialStart", "MoneyScale", "MaxPlausibleSideM",
                    "DefaultUsdFx", "DefaultMethod", "DefaultAreaBasis", "DeclaredValueKeyField",
                    "DeclaredValueFallbackUsd", "DeclaredValuesStamp", "DefaultBaleTargetAreaM2",
                    "BaleTargetTolerancePercent", "RequireFinalSizeToFinalize", "BaleTerm",
                    "DefaultDeclarationTemplateId")
                SELECT "Id", 'RUG', 1, NULL, 0, 12.00,
                       NULL, 0, 1, 'rug.origin',
                       NULL, 0, NULL,
                       10, TRUE, 0,
                       NULL
                FROM "Tenants"
                ON CONFLICT ("TenantId") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantGridLayouts");

            migrationBuilder.DropTable(
                name: "TenantPreferences");

            migrationBuilder.DropColumn(
                name: "CoachDismissedJson",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GridEnter",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TextScale",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UiMode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Exportable",
                table: "CustomFieldDefinitions");

            migrationBuilder.DropColumn(
                name: "GridOrder",
                table: "CustomFieldDefinitions");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "CustomFieldDefinitions");

            migrationBuilder.DropColumn(
                name: "ShowInGrid",
                table: "CustomFieldDefinitions");

            migrationBuilder.DropColumn(
                name: "DetailsJson",
                table: "AuditEntries");
        }
    }
}
