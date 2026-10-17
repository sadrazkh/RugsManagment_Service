using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RugsManagment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CostLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CostEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReceiptNo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ServiceProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Basis = table.Column<int>(type: "integer", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<int>(type: "integer", nullable: false),
                    FxRate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    BaseAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FrozenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AreaBasis = table.Column<int>(type: "integer", nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScopeNote = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ClientKey = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostEntries_CostCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "CostCategories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CostEntries_RugBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "RugBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CostEntries_ServiceProviders_ServiceProviderId",
                        column: x => x.ServiceProviderId,
                        principalTable: "ServiceProviders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CostEntries_Shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "Shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CostEntries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CostAllocations",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    RugId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    WeightUnits = table.Column<long>(type: "bigint", nullable: false),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostAllocations", x => new { x.EntryId, x.RugId });
                    table.ForeignKey(
                        name: "FK_CostAllocations_CostEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "CostEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CostAllocations_Rugs_RugId",
                        column: x => x.RugId,
                        principalTable: "Rugs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CostAllocations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CostEntryPins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    RugId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostEntryPins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostEntryPins_CostEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "CostEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CostEntryPins_Rugs_RugId",
                        column: x => x.RugId,
                        principalTable: "Rugs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CostEntryPins_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CostEntryResults",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Lines = table.Column<int>(type: "integer", nullable: false),
                    Pieces = table.Column<int>(type: "integer", nullable: false),
                    AreaM2 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    TotalWeightUnits = table.Column<long>(type: "bigint", nullable: false),
                    BaseAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Allocated = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Unallocated = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    WarningsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ComputedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostEntryResults", x => x.EntryId);
                    table.ForeignKey(
                        name: "FK_CostEntryResults_CostEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "CostEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CostEntryResults_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CostEntryTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    RugId = table.Column<Guid>(type: "uuid", nullable: true),
                    BaleId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostEntryTargets", x => x.Id);
                    table.CheckConstraint("CK_CostEntryTargets_OneTarget", "(\"RugId\" IS NOT NULL) <> (\"BaleId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_CostEntryTargets_Bales_BaleId",
                        column: x => x.BaleId,
                        principalTable: "Bales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CostEntryTargets_CostEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "CostEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CostEntryTargets_Rugs_RugId",
                        column: x => x.RugId,
                        principalTable: "Rugs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CostEntryTargets_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CostAllocations_RugId",
                table: "CostAllocations",
                column: "RugId");

            migrationBuilder.CreateIndex(
                name: "IX_CostAllocations_TenantId_RugId",
                table: "CostAllocations",
                columns: new[] { "TenantId", "RugId" });

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_BatchId",
                table: "CostEntries",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_CategoryId",
                table: "CostEntries",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_ServiceProviderId",
                table: "CostEntries",
                column: "ServiceProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_ShipmentId",
                table: "CostEntries",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_TenantId_ClientKey",
                table: "CostEntries",
                columns: new[] { "TenantId", "ClientKey" },
                unique: true,
                filter: "\"ClientKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_TenantId_Date",
                table: "CostEntries",
                columns: new[] { "TenantId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_TenantId_Number",
                table: "CostEntries",
                columns: new[] { "TenantId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CostEntryPins_EntryId_RugId",
                table: "CostEntryPins",
                columns: new[] { "EntryId", "RugId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CostEntryPins_RugId",
                table: "CostEntryPins",
                column: "RugId");

            migrationBuilder.CreateIndex(
                name: "IX_CostEntryPins_TenantId",
                table: "CostEntryPins",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CostEntryResults_TenantId_State",
                table: "CostEntryResults",
                columns: new[] { "TenantId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_CostEntryTargets_BaleId",
                table: "CostEntryTargets",
                column: "BaleId");

            migrationBuilder.CreateIndex(
                name: "IX_CostEntryTargets_EntryId_BaleId",
                table: "CostEntryTargets",
                columns: new[] { "EntryId", "BaleId" },
                unique: true,
                filter: "\"BaleId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CostEntryTargets_EntryId_RugId",
                table: "CostEntryTargets",
                columns: new[] { "EntryId", "RugId" },
                unique: true,
                filter: "\"RugId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CostEntryTargets_RugId",
                table: "CostEntryTargets",
                column: "RugId");

            migrationBuilder.CreateIndex(
                name: "IX_CostEntryTargets_TenantId",
                table: "CostEntryTargets",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CostAllocations");

            migrationBuilder.DropTable(
                name: "CostEntryPins");

            migrationBuilder.DropTable(
                name: "CostEntryResults");

            migrationBuilder.DropTable(
                name: "CostEntryTargets");

            migrationBuilder.DropTable(
                name: "CostEntries");
        }
    }
}
