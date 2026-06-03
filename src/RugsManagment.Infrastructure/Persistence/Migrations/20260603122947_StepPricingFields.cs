using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RugsManagment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StepPricingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppliedPricingModel",
                table: "RugWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "AppliedUnitRate",
                table: "RugWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "PricingConfigJson",
                table: "RugWorkflowSteps");
        }
    }
}
