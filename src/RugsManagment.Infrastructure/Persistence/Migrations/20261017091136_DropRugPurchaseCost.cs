using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
using RugsManagment.Infrastructure.Persistence.Seed;

namespace RugsManagment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [RequiresBackup("Rugs")]
    public partial class DropRugPurchaseCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PurchaseCost",
                table: "Rugs");
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "_m2_rug_dims_backup";""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PurchaseCost",
                table: "Rugs",
                type: "numeric",
                nullable: true);
            migrationBuilder.Sql("""
                UPDATE "Rugs" r SET "PurchaseCost" =
                    (SELECT sum(l."Amount") FROM "RugPurchaseLines" l WHERE l."RugId" = r."Id");
                CREATE TABLE IF NOT EXISTS "_m2_rug_dims_backup" (
                    "RugId" uuid NOT NULL PRIMARY KEY,
                    "Length" numeric NOT NULL,
                    "Width" numeric NOT NULL
                );
                INSERT INTO "_m2_rug_dims_backup" ("RugId", "Length", "Width")
                SELECT "Id", "LengthMeters", "WidthMeters" FROM "Rugs"
                ON CONFLICT ("RugId") DO NOTHING;
                """);
        }
    }
}
