using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RugsManagment.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// کنترل هم‌زمانی خوش‌بینانه روی Rugs و RugWorkflowSteps + ایندکس ترتیب مراحل.
    ///
    /// نکته: EF برای توکن هم‌زمانی، AddColumn برای «xmin» تولید می‌کند اما xmin یک
    /// ستون سیستمیِ همیشه‌موجودِ PostgreSQL است و ساختنش خطا می‌دهد؛ بنابراین آن دستورها
    /// عمداً حذف شده‌اند. مدل (snapshot) همچنان xmin را می‌شناسد و به همان ستون سیستمی نگاشت می‌شود.
    /// </summary>
    public partial class ConcurrencyAndStepIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RugWorkflowSteps_RugId",
                table: "RugWorkflowSteps");

            // ایندکس ترکیبی: مراحل هر فرش تقریباً همیشه به ترتیب OrderIndex خوانده می‌شوند
            migrationBuilder.CreateIndex(
                name: "IX_RugWorkflowSteps_RugId_OrderIndex",
                table: "RugWorkflowSteps",
                columns: new[] { "RugId", "OrderIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RugWorkflowSteps_RugId_OrderIndex",
                table: "RugWorkflowSteps");

            migrationBuilder.CreateIndex(
                name: "IX_RugWorkflowSteps_RugId",
                table: "RugWorkflowSteps",
                column: "RugId");
        }
    }
}
