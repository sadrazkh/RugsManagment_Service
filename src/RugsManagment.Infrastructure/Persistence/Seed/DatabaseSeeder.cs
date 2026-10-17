using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Costing;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using RugsManagment.Infrastructure.Persistence.Repositories;

namespace RugsManagment.Infrastructure.Persistence.Seed;

/// <summary>
/// دادهٔ اولیه هنگام بالا آمدن برنامه (migration پیش از این، کار <see cref="Migrator"/> است):
/// 1) انواع مرحله (قالیشویی، دارکشی، …)
/// 2) ادمین سیستم
/// 3) کارگاه دمو + دو قالب مسیر نمونه
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!await db.ProcessStepTypes.AnyAsync())
        {
            db.ProcessStepTypes.AddRange(
                Step("intake", "پذیرش", "Intake", "inventory_2", 0, StepPricingModel.Fixed, 0),
                Step("washing", "قالیشویی", "Washing", "water_drop", 1, StepPricingModel.PerSquareMeter, 85000),
                Step("darkening", "دارکشی", "Darkening", "contrast", 2, StepPricingModel.PerSquareMeter, 45000),
                Step("repair", "رفوگری", "Repair", "construction", 3, StepPricingModel.PerSquareMeter, 120000),
                Step("fringe", "رفوگری حاشیه", "Fringe Repair", "gesture", 4, StepPricingModel.PerSquareMeter, 35000),
                Step("inspection", "بازرسی کیفیت", "Quality Inspection", "fact_check", 5, StepPricingModel.Fixed, 150000),
                Step("packaging", "بسته‌بندی", "Packaging", "package_2", 6, StepPricingModel.Fixed, 80000),
                Step("ready", "آماده فروش", "Ready for Sale", "sell", 7, StepPricingModel.Fixed, 0));
            await db.SaveChangesAsync();
        }

        if (!await db.Users.AnyAsync(u => u.Role == UserRole.SystemAdmin))
        {
            var admin = new User
            {
                Email = "admin@rugsystem.local",
                FullName = "مدیر سیستم",
                Role = UserRole.SystemAdmin,
                PasswordHash = AuthService.HashPassword("Admin@12345"),
                IsActive = true
            };
            // پیش‌فرض‌های نقش (حالت کامل)؛ پیش‌فرض خودِ موجودیت «ساده» است
            admin.ApplyRoleDefaultPreferences();
            db.Users.Add(admin);
            await db.SaveChangesAsync();
        }

        var seedDemo = !bool.TryParse(scope.ServiceProvider.GetService<IConfiguration>()?["Seed:Demo"], out var configuredDemo) || configuredDemo;
        if (seedDemo && !await db.Tenants.AnyAsync())
        {
            var demoTenant = new Tenant
            {
                Name = "قالی‌بافی نمونه",
                Slug = "demo-weaver",
                ContactPhone = "09120000000",
                IsActive = true
            };
            db.Tenants.Add(demoTenant);
            await db.SaveChangesAsync();

            var demoAdmin = new User
            {
                TenantId = demoTenant.Id,
                Email = "demo@rugsystem.local",
                FullName = "مدیر کارگاه",
                Role = UserRole.TenantAdmin,
                PasswordHash = AuthService.HashPassword("Demo@12345"),
                IsActive = true
            };
            demoAdmin.ApplyRoleDefaultPreferences();
            db.Users.Add(demoAdmin);

            var washing = await db.ProcessStepTypes.FirstAsync(s => s.Code == "washing");
            var darkening = await db.ProcessStepTypes.FirstAsync(s => s.Code == "darkening");
            var repair = await db.ProcessStepTypes.FirstAsync(s => s.Code == "repair");
            var inspection = await db.ProcessStepTypes.FirstAsync(s => s.Code == "inspection");
            var ready = await db.ProcessStepTypes.FirstAsync(s => s.Code == "ready");

            // قالب ۱: همیشه دارکشی دارد
            var fullTemplate = new WorkflowTemplate
            {
                TenantId = demoTenant.Id,
                Name = "مسیر کامل (با دارکشی)",
                IsDefault = true,
                IsActive = true,
                Steps =
                [
                    new() { ProcessStepTypeId = washing.Id, OrderIndex = 0 },
                    new() { ProcessStepTypeId = darkening.Id, OrderIndex = 1, IsOptional = false },
                    new() { ProcessStepTypeId = repair.Id, OrderIndex = 2 },
                    new() { ProcessStepTypeId = inspection.Id, OrderIndex = 3 },
                    new() { ProcessStepTypeId = ready.Id, OrderIndex = 4 }
                ]
            };

            // قالب ۲: دارکشی اختیاری — هنگام ثبت فرش می‌توان رد کرد
            var shortTemplate = new WorkflowTemplate
            {
                TenantId = demoTenant.Id,
                Name = "مسیر سریع (دارکشی اختیاری)",
                IsActive = true,
                Steps =
                [
                    new() { ProcessStepTypeId = washing.Id, OrderIndex = 0 },
                    new() { ProcessStepTypeId = repair.Id, OrderIndex = 1 },
                    new() { ProcessStepTypeId = darkening.Id, OrderIndex = 2, IsOptional = true },
                    new() { ProcessStepTypeId = inspection.Id, OrderIndex = 3 },
                    new() { ProcessStepTypeId = ready.Id, OrderIndex = 4 }
                ]
            };

            db.WorkflowTemplates.AddRange(fullTemplate, shortTemplate);
            await db.SaveChangesAsync();

            await SeedDemoContentAsync(
                db, scope.ServiceProvider.GetService<IUnitOfWork>(), demoTenant, fullTemplate,
                [washing, darkening, repair, inspection, ready]);
        }
    }

    /// <summary>
    /// دادهٔ نمایشی برای کارگاه دمو: چند فرش در مراحل مختلف، یک «ورودی»، یک فیلد سفارشی، یک فروش و یک برچسب.
    /// فقط یک بار (هنگام ساخت اولیهٔ کارگاه دمو) اجرا می‌شود.
    ///
    /// <para><b>از فاز ۲ (جای ۷ از هفت جای «سرمایهٔ کل»، D.4):</b> هیچ جمعی این‌جا حساب نمی‌شود. قیمت خرید سطر خرید «کل» است
    /// (ستون منسوخ <c>PurchaseCost</c> نوشته نمی‌شود)، قیمت مراحل با همان قاعدهٔ دسته و عکس اندازه (<see cref="RugCostMath"/>)،
    /// و ذخیره از <see cref="IUnitOfWork"/> (پاس سازگاری) می‌گذرد تا مبلغ سطرها، <c>RugCostTotal</c> و عکس بهای فروش را همان
    /// پاس بنویسد. میزبانی که UnitOfWork ثبت نکرده (آزمون استارت با DI حداقلی) مستقیم ذخیره می‌کند و بررسی استارت حساب‌ها
    /// (<c>LedgerStartupCheck</c>) ردیف‌ها را می‌سازد. فرش «فروخته‌شده» حالا فروش واقعی دارد (پیش از این بی‌فروش بود).</para>
    /// </summary>
    private static async Task SeedDemoContentAsync(
        AppDbContext db, IUnitOfWork? unitOfWork, Tenant tenant, WorkflowTemplate template, ProcessStepType[] flow)
    {
        // فیلد سفارشی نمونه
        db.CustomFieldDefinitions.Add(new CustomFieldDefinition
        {
            TenantId = tenant.Id, Key = "bg_color", Label = "رنگ زمینه",
            FieldType = CustomFieldType.Text, SortOrder = 0, IsActive = true
        });

        // شمارهٔ ورودی از شمارندهٔ کارگاه (یکتا در کارگاه؛ ورودی بعدی‌ای که کاربر می‌سازد از همین ادامه می‌دهد)
        var batchNumber = await new TenantCounterRepository(db).NextBlockAsync(tenant.Id, RugBatchService.BatchCounterKey);
        var batch = new RugBatch
        {
            TenantId = tenant.Id, Number = checked((int)batchNumber), Name = "ورودی نمونه", Description = "فرش‌های ورودی نمونه",
            ReceivedAt = DateTimeOffset.UtcNow.AddDays(-3)
        };
        db.RugBatches.Add(batch);

        var month = DateTime.UtcNow.ToString("yyyyMM", CultureInfo.InvariantCulture);
        var seq = 0;

        Rug MakeRug(string title, string origin, decimal w, decimal l, decimal purchase, decimal target, int currentIdx, RugStatus status, string? color, Guid? batchId)
        {
            var rug = new Rug
            {
                TenantId = tenant.Id, Sku = $"RUG-{month}-{++seq:D4}", Title = title, Origin = origin,
                Pattern = "لچک ترنج", Material = "پشم و ابریشم",
                TargetSalePrice = target, Status = status, CurrentStepIndex = currentIdx,
                WorkflowTemplateId = template.Id, BatchId = batchId,
                MetadataJson = color is null ? null : $"{{\"bg_color\":\"{color}\"}}"
            };
            rug.SetIntakeSize(l, w);

            for (var i = 0; i < flow.Length; i++)
            {
                var st = status is RugStatus.ReadyForSale or RugStatus.Sold ? WorkflowStepStatus.Completed
                    : i < currentIdx ? WorkflowStepStatus.Completed
                    : i == currentIdx ? WorkflowStepStatus.InProgress
                    : WorkflowStepStatus.Pending;
                var step = new RugWorkflowStep
                {
                    RugId = rug.Id, ProcessStepTypeId = flow[i].Id, OrderIndex = i, Status = st,
                    StartedAt = st != WorkflowStepStatus.Pending ? DateTimeOffset.UtcNow.AddDays(-flow.Length + i) : null,
                    CompletedAt = st == WorkflowStepStatus.Completed ? DateTimeOffset.UtcNow.AddDays(-flow.Length + i + 1) : null
                };
                if (st is WorkflowStepStatus.Completed or WorkflowStepStatus.InProgress)
                {
                    // قیمت‌گذاری روی عکس اندازه (B.5) و قاعدهٔ دسته در رقم اعشار ۰ (پیش‌فرض کارگاه تازه)
                    step.SnapshotPricingSize(l, w, rug.Quantity);
                    step.CalculatedCost = RugCostMath.StepLinePrice(
                        flow[i].DefaultPricingModel, flow[i].DefaultUnitRate, l, w, rug.Quantity, moneyScale: 0);
                }
                rug.WorkflowSteps.Add(step);
            }

            rug.PurchaseLines.Add(new RugPurchaseLine
            {
                TenantId = tenant.Id, RugId = rug.Id, Label = RugPurchaseLine.LegacyPurchaseLabel,
                Basis = CostBasis.Total, Rate = purchase, SortOrder = 0
            });
            return rug;
        }

        // ترتیب ساخت = ترتیب SKU (۰۰۰۱ تبریز … ۰۰۰۵ قم)، همان پیش از فاز ۲
        var rugs = new[]
        {
            MakeRug("تبریز لاکی", "تبریز", 2m, 3m, 45_000_000, 90_000_000, 0, RugStatus.InProgress, "لاکی", batch.Id),
            MakeRug("کاشان سرمه‌ای", "کاشان", 1.5m, 2.25m, 30_000_000, 65_000_000, 2, RugStatus.InProgress, "سرمه‌ای", batch.Id),
            MakeRug("اصفهان کرم", "اصفهان", 3m, 4m, 120_000_000, 260_000_000, 4, RugStatus.ReadyForSale, "کرم", null),
            MakeRug("نایین آبی", "نایین", 2m, 3m, 80_000_000, 170_000_000, 4, RugStatus.Sold, "آبی", null),
            MakeRug("قم ابریشم", "قم", 1m, 1.5m, 60_000_000, 140_000_000, 0, RugStatus.Draft, null, null)
        };
        var sold = rugs[3];
        db.Rugs.AddRange(rugs);
        db.RugWorkflowSteps.AddRange(rugs.SelectMany(r => r.WorkflowSteps));
        db.Set<RugPurchaseLine>().AddRange(rugs.SelectMany(r => r.PurchaseLines));

        // «فروخته‌شده» یعنی فروش ثبت‌شده (RugStatusResolver)؛ عکس بهای فروش را پاس در همین ذخیره می‌گیرد
        db.RugSales.Add(new RugSale
        {
            TenantId = tenant.Id, RugId = sold.Id, BuyerName = "خریدار نمونه", SalePrice = 170_000_000,
            ReceivedAmount = 170_000_000, PaymentMethod = SalePaymentMethod.Cash, SoldAt = DateTimeOffset.UtcNow.AddDays(-1)
        });

        // برچسب نمونه (طراحی بصری)
        db.LabelTemplates.Add(new LabelTemplate
        {
            TenantId = tenant.Id, Name = "برچسب استاندارد", WidthMm = 90, HeightMm = 50, Mode = LabelMode.Visual,
            ElementsJson = """
            {"columns":2,"elements":[
              {"id":"e1","type":"heading","colSpan":2,"text":"{{title}}","fontSize":16,"bold":true,"align":"center"},
              {"id":"e2","type":"divider","colSpan":2},
              {"id":"e3","type":"field","colSpan":1,"field":"sku","prefix":true,"fontSize":12,"align":"right"},
              {"id":"e4","type":"field","colSpan":1,"field":"dimensions","prefix":true,"fontSize":12,"align":"right"},
              {"id":"e5","type":"qr","colSpan":2,"source":"{{sku}}","align":"center"}
            ]}
            """
        });

        if (unitOfWork is not null)
            await unitOfWork.SaveChangesAsync();
        else
            await db.SaveChangesAsync();
    }

    private static ProcessStepType Step(
        string code, string fa, string en, string icon, int order,
        StepPricingModel model, decimal rate) => new()
    {
        Code = code,
        NameFa = fa,
        NameEn = en,
        Icon = icon,
        SortOrder = order,
        DefaultPricingModel = model,
        DefaultUnitRate = rate
    };
}
