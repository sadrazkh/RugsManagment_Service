using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RugsManagment.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// همان سه ستون PendingModelSync را می‌سازد؛ از شاخهٔ دیگری merge شد و تکراری است.
    ///
    /// چرا SQL خام و idempotent: روی دیتابیس خالی، PendingModelSync ستون‌ها را قبلاً ساخته و
    /// AddColumnِ معمولی اینجا با خطای 42701 (ستون تکراری) کل زنجیره را می‌شکست. دیتابیسِ موجودِ
    /// صاحب پروژه برعکس، همین migration را اجرا کرده ولی PendingModelSync را نه. با «IF NOT EXISTS»
    /// هر کدام زودتر اجرا شود ستون‌ها را می‌سازد و دیگری بی‌اثر است. Designer و snapshot دست
    /// نخورده‌اند چون مدل از اول درست بود.
    /// </summary>
    public partial class SyncModelState : Migration
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

        /// <summary>
        /// عمداً خالی. این سه ستون مال PendingModelSync هستند (مدلِ Designerِ همان migration هم آن‌ها را دارد)
        /// و Downِ همان migration پاکشان می‌کند. EF هنگام برگشت migrationها را به ترتیب عکسِ شناسه برمی‌گرداند،
        /// پس هر برگشتی که از PendingModelSync هم رد شود، بلافاصله بعد از این متد Downِ آن را اجرا می‌کند.
        ///
        /// قبلاً اینجا ستون‌ها حذف می‌شدند؛ آن‌وقت برگشت فقط تا PendingModelSync (که هنوز اجراشده ثبت می‌ماند)
        /// همهٔ دادهٔ قیمت‌گذاری مراحل (روش، نرخ و پیکربندی ترکیبی) را پاک می‌کرد و دیتابیس را بدون ستون‌هایی
        /// می‌گذاشت که مدلِ همان migration انتظارشان را دارد (خطای 42703 در کدِ آن نسخه).
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
