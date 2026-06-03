using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RugsManagment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RugBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcessStepTypes_Tenants_TenantId",
                table: "ProcessStepTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_Rugs_RugPatterns_PatternId",
                table: "Rugs");

            migrationBuilder.DropTable(
                name: "LabelTemplates");

            migrationBuilder.DropTable(
                name: "RugPatterns");

            migrationBuilder.DropIndex(
                name: "IX_ProcessStepTypes_TenantId_Code",
                table: "ProcessStepTypes");

            migrationBuilder.DropColumn(
                name: "AppliedPricingModel",
                table: "RugWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "AppliedUnitRate",
                table: "RugWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "PricingConfigJson",
                table: "RugWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ProcessStepTypes");

            migrationBuilder.RenameColumn(
                name: "PatternId",
                table: "Rugs",
                newName: "BatchId");

            migrationBuilder.RenameIndex(
                name: "IX_Rugs_PatternId",
                table: "Rugs",
                newName: "IX_Rugs_BatchId");

            migrationBuilder.CreateTable(
                name: "RugBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RugBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RugBatches_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStepTypes_Code",
                table: "ProcessStepTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RugBatches_TenantId",
                table: "RugBatches",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Rugs_RugBatches_BatchId",
                table: "Rugs",
                column: "BatchId",
                principalTable: "RugBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rugs_RugBatches_BatchId",
                table: "Rugs");

            migrationBuilder.DropTable(
                name: "RugBatches");

            migrationBuilder.DropIndex(
                name: "IX_ProcessStepTypes_Code",
                table: "ProcessStepTypes");

            migrationBuilder.RenameColumn(
                name: "BatchId",
                table: "Rugs",
                newName: "PatternId");

            migrationBuilder.RenameIndex(
                name: "IX_Rugs_BatchId",
                table: "Rugs",
                newName: "IX_Rugs_PatternId");

            migrationBuilder.AddColumn<int>(
                name: "AppliedPricingModel",
                table: "RugWorkflowSteps",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AppliedUnitRate",
                table: "RugWorkflowSteps",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PricingConfigJson",
                table: "RugWorkflowSteps",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ProcessStepTypes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LabelTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    HeightMm = table.Column<decimal>(type: "numeric", nullable: false),
                    HtmlTemplate = table.Column<string>(type: "text", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    LayoutJson = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    WidthMm = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabelTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabelTemplates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RugPatterns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RugPatterns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RugPatterns_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStepTypes_TenantId_Code",
                table: "ProcessStepTypes",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabelTemplates_TenantId",
                table: "LabelTemplates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RugPatterns_TenantId_Name",
                table: "RugPatterns",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessStepTypes_Tenants_TenantId",
                table: "ProcessStepTypes",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Rugs_RugPatterns_PatternId",
                table: "Rugs",
                column: "PatternId",
                principalTable: "RugPatterns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
