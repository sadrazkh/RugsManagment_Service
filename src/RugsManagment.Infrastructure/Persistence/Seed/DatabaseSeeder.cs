using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Infrastructure.Persistence.Seed;

/// <summary>
/// دادهٔ اولیه هنگام بالا آمدن API:
/// 1) اجرای migration
/// 2) انواع مرحله (قالیشویی، دارکشی، …)
/// 3) ادمین سیستم
/// 4) کارگاه دمو + دو قالب مسیر نمونه
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

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
            db.Users.Add(admin);
            await db.SaveChangesAsync();
        }

        if (!await db.Tenants.AnyAsync())
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
        }
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
