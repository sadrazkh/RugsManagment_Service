using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RugsManagment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProviderRatesAndPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServiceProviders_TenantId",
                table: "ServiceProviders");

            // EF این دو را به‌صورت RenameColumn تولید کرده بود، اما نام‌گذاری دوباره باعث می‌شد
            // محتوای JSONی ستون قدیمی به‌عنوان «یادداشت» به کاربر نمایش داده شود.
            // ستون قدیمی دیگر هیچ‌جا خوانده نمی‌شود و جای آن را جدول ServiceProviderRates گرفته است.
            migrationBuilder.DropColumn(
                name: "SupportedStepTypeCodesJson",
                table: "ServiceProviders");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "ServiceProviders",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Specialty",
                table: "ServiceProviders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "ServiceProviders",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ProviderPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderPayments_ServiceProviders_ServiceProviderId",
                        column: x => x.ServiceProviderId,
                        principalTable: "ServiceProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProviderPayments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceProviderRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessStepTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PricingModel = table.Column<int>(type: "integer", nullable: false),
                    UnitRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PricingConfigJson = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceProviderRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceProviderRates_ProcessStepTypes_ProcessStepTypeId",
                        column: x => x.ProcessStepTypeId,
                        principalTable: "ProcessStepTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceProviderRates_ServiceProviders_ServiceProviderId",
                        column: x => x.ServiceProviderId,
                        principalTable: "ServiceProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceProviders_TenantId_Name",
                table: "ServiceProviders",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderPayments_ServiceProviderId_PaidAt",
                table: "ProviderPayments",
                columns: new[] { "ServiceProviderId", "PaidAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderPayments_TenantId",
                table: "ProviderPayments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceProviderRates_ProcessStepTypeId",
                table: "ServiceProviderRates",
                column: "ProcessStepTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceProviderRates_ServiceProviderId_ProcessStepTypeId",
                table: "ServiceProviderRates",
                columns: new[] { "ServiceProviderId", "ProcessStepTypeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProviderPayments");

            migrationBuilder.DropTable(
                name: "ServiceProviderRates");

            migrationBuilder.DropIndex(
                name: "IX_ServiceProviders_TenantId_Name",
                table: "ServiceProviders");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "ServiceProviders");

            migrationBuilder.AddColumn<string>(
                name: "SupportedStepTypeCodesJson",
                table: "ServiceProviders",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Specialty",
                table: "ServiceProviders",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "ServiceProviders",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceProviders_TenantId",
                table: "ServiceProviders",
                column: "TenantId");
        }
    }
}
