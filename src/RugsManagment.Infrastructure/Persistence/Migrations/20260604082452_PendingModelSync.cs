using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RugsManagment.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// سه ستون قیمت‌گذاریِ RugWorkflowSteps را (که RugBatches حذف کرده بود) دوباره می‌سازد.
    ///
    /// چرا SQL خام و idempotent: همین سه ستون در زنجیره چهار بار دست خورده‌اند —
    /// StepPricingFields می‌سازد، RugBatches حذف می‌کند، این migration دوباره می‌سازد و
    /// SyncModelState (که از شاخهٔ دیگری merge شد) باز هم می‌سازد. روی دیتابیس خالی،
    /// AddColumnِ معمولی در SyncModelState با خطای 42701 (ستون تکراری) کل زنجیره را می‌شکست.
    /// از طرف دیگر دیتابیسِ موجودِ صاحب پروژه همهٔ migrationها جز همین یکی را اجرا کرده و ستون‌ها
    /// را دارد؛ پس این migration باید روی آن دیتابیس بی‌اثر باشد. «IF NOT EXISTS» هر دو حالت را
    /// درست می‌کند. فایل Designer و snapshot دست نخورده‌اند چون مدل درست بود و فقط Up/Down خراب بود.
    /// </summary>
    public partial class PendingModelSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "RugWorkflowSteps" ADD COLUMN IF NOT EXISTS "AppliedPricingModel" integer;
                ALTER TABLE "RugWorkflowSteps" ADD COLUMN IF NOT EXISTS "AppliedUnitRate" numeric;
                ALTER TABLE "RugWorkflowSteps" ADD COLUMN IF NOT EXISTS "PricingConfigJson" text;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "RugWorkflowSteps" DROP COLUMN IF EXISTS "AppliedPricingModel";
                ALTER TABLE "RugWorkflowSteps" DROP COLUMN IF EXISTS "AppliedUnitRate";
                ALTER TABLE "RugWorkflowSteps" DROP COLUMN IF EXISTS "PricingConfigJson";
                """);
        }
    }
}
