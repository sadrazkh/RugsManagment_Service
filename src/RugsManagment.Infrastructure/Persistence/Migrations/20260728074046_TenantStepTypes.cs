using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RugsManagment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TenantStepTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProcessStepTypes_Code",
                table: "ProcessStepTypes");

            // پستگرس برای تبدیل text به jsonb به USING نیاز دارد؛ AlterColumn پیش‌فرض EF
            // این را تولید نمی‌کند و روی داده‌های موجود شکست می‌خورد.
            migrationBuilder.Sql(
                """
                ALTER TABLE "ProcessStepTypes"
                ALTER COLUMN "FieldSchemaJson" TYPE jsonb
                USING NULLIF("FieldSchemaJson", '')::jsonb;
                """);

            migrationBuilder.AddColumn<int>(
                name: "ExpectedDurationDays",
                table: "ProcessStepTypes",
                type: "integer",
                nullable: true);

            // پیش‌فرض true — وگرنه همهٔ مرحله‌های موجود (قالیشویی، رفوگری، …) یکباره
            // غیرفعال می‌شدند و از فهرست انتخاب حذف می‌گشتند.
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ProcessStepTypes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ProcessStepTypes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStepTypes_TenantId_Code",
                table: "ProcessStepTypes",
                columns: new[] { "TenantId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProcessStepTypes_TenantId_Code",
                table: "ProcessStepTypes");

            migrationBuilder.DropColumn(
                name: "ExpectedDurationDays",
                table: "ProcessStepTypes");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ProcessStepTypes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ProcessStepTypes");

            migrationBuilder.AlterColumn<string>(
                name: "FieldSchemaJson",
                table: "ProcessStepTypes",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStepTypes_Code",
                table: "ProcessStepTypes",
                column: "Code",
                unique: true);
        }
    }
}
