using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.DTOs.Auth;
using RugsManagment.Application.DTOs.CustomFields;
using RugsManagment.Application.DTOs.Dashboard;
using RugsManagment.Application.DTOs.Rugs;
using RugsManagment.Application.DTOs.Tenants;
using RugsManagment.Application.DTOs.Workflows;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;
using System.Text.Json;

namespace RugsManagment.Application.Mapping;

/// <summary>
/// تبدیل موجودیت دامنه به DTO برای API — یک نقطه تا شکل JSON ثابت بماند.
/// </summary>
public static class EntityMappers
{
    public static UserDto ToDto(this User user) => new(
        user.Id,
        user.Email,
        user.FullName,
        user.Role.ToString(),
        user.TenantId,
        user.Tenant?.Name);

    public static TenantDto ToDto(this Tenant tenant) => new(
        tenant.Id,
        tenant.Name,
        tenant.Slug,
        tenant.IsActive,
        tenant.ContactPhone,
        tenant.ContactEmail,
        tenant.SubscriptionExpiresAt);

    public static ProcessStepTypeDto ToDto(this ProcessStepType t) => new(
        t.Id, t.Code, t.NameFa, t.NameEn, t.Icon, t.SortOrder,
        t.DefaultPricingModel, t.DefaultUnitRate, t.FieldSchemaJson);

    public static WorkflowTemplateDto ToDto(this WorkflowTemplate template) => new(
        template.Id,
        template.Name,
        template.Description,
        template.IsDefault,
        template.IsActive,
        template.Steps.OrderBy(s => s.OrderIndex).Select(s => s.ToDto()).ToList());

    public static WorkflowTemplateStepDto ToDto(this WorkflowTemplateStep step) => new(
        step.Id,
        step.ProcessStepTypeId,
        step.ProcessStepType?.Code ?? "",
        step.ProcessStepType?.NameFa ?? "",
        step.OrderIndex,
        step.IsOptional,
        step.DefaultServiceProviderId,
        step.OverridePricingModel,
        step.OverrideUnitRate);

    /// <summary>
    /// «مراحلی که این طرف انجام می‌دهد» از نرخ‌های تعریف‌شده مشتق می‌شود، نه از یک فهرست جدا:
    /// نرخ داشتن برای یک نوع مرحله یعنی همان کار را انجام می‌دهد.
    /// </summary>
    public static ServiceProviderDto ToDto(this ServiceProvider sp) => new(
        sp.Id, sp.Name, sp.Specialty, sp.Phone, sp.Address, sp.IsActive,
        sp.Rates.Select(r => r.ProcessStepType?.Code ?? string.Empty)
            .Where(code => code.Length > 0)
            .ToList());

    /// <summary>فرش کامل با مراحل و خلاصهٔ هزینه — برای صفحهٔ جزئیات</summary>
    public static RugDto ToDto(this Rug rug, IWorkflowEngine workflowEngine)
    {
        var costs = workflowEngine.CalculateRugCosts(rug);
        var current = rug.WorkflowSteps.FirstOrDefault(s => s.Status == Domain.Enums.WorkflowStepStatus.InProgress);
        return new RugDto(
            rug.Id,
            rug.Sku,
            rug.Title,
            rug.Origin,
            rug.Pattern,
            rug.Material,
            rug.KnotDensity,
            rug.WidthMeters,
            rug.LengthMeters,
            rug.AreaSquareMeters,
            rug.PurchaseCost,
            rug.TargetSalePrice,
            rug.Status,
            rug.ImageUrl,
            rug.Notes,
            rug.WorkflowTemplateId,
            rug.BatchId,
            rug.Batch?.Name,
            current?.ProcessStepType?.NameFa,
            rug.CurrentStepIndex,
            rug.WorkflowSteps.OrderBy(s => s.OrderIndex).Select(s => s.ToDto()).ToList(),
            costs.ToDto(rug.Sale),
            rug.MetadataJson,
            rug.Images.OrderBy(i => i.SortOrder).Select(i => i.ToDto()).ToList());
    }

    /// <summary>
    /// آدرس‌ها از مسیر کنترلر media ساخته می‌شوند، نه از مسیر فایل سیستمی:
    /// فایل‌ها بیرون از wwwroot هستند و با بررسی مالکیت کارگاه سرو می‌شوند.
    /// </summary>
    public static RugImageDto ToDto(this RugImage image)
    {
        var url = $"/media/rugs/{image.RugId}/{image.FileName}";
        var thumbnail = string.IsNullOrEmpty(image.ThumbnailFileName)
            ? url
            : $"/media/rugs/{image.RugId}/{image.ThumbnailFileName}";

        return new RugImageDto(
            image.Id, url, thumbnail, image.Width, image.Height, image.SizeBytes, image.SortOrder, image.IsPrimary);
    }

    public static CustomFieldDefinitionDto ToDto(this CustomFieldDefinition f) => new(
        f.Id, f.Key, f.Label, f.FieldType, f.OptionsJson, f.IsRequired, f.SortOrder, f.IsActive);

    public static RugWorkflowStepDto ToDto(this RugWorkflowStep step) => new(
        step.Id,
        step.ProcessStepTypeId,
        step.ProcessStepType?.Code ?? "",
        step.ProcessStepType?.NameFa ?? "",
        step.ProcessStepType?.NameEn ?? "",
        step.ProcessStepType?.Icon ?? "circle",
        step.OrderIndex,
        step.IsOptional,
        step.Status,
        step.ServiceProviderId,
        step.ServiceProvider?.Name,
        step.StartedAt,
        step.CompletedAt,
        step.EffectiveCost,
        step.CalculatedCost,
        step.AppliedPricingModel?.ToString(),
        step.AppliedUnitRate,
        step.PricingConfigJson,
        step.FieldValuesJson,
        step.Notes,
        step.Adjustment,
        step.CompletedByName);

    /// <summary>
    /// خلاصهٔ هزینه. اگر فرش فروخته شده باشد، سود واقعی هم کنار سود تخمینی می‌آید
    /// تا تفاوت «آنچه انتظار داشتیم» و «آنچه شد» دیده شود.
    /// </summary>
    public static RugCostSummaryDto ToDto(this RugCostSummary summary, RugSale? sale = null) => new(
        summary.TotalProcessCost,
        summary.PurchaseCost,
        summary.TotalInvestment,
        summary.TargetSalePrice,
        summary.EstimatedMargin,
        sale?.NetAmount,
        sale is null ? null : sale.NetAmount - summary.TotalInvestment);

    /// <summary>ساخت آمار داشبورد از لیست فرش‌ها — بدون کوئری اضافی</summary>
    public static DashboardStatsDto ToDashboard(
        IReadOnlyList<Rug> rugs,
        IWorkflowEngine workflowEngine)
    {
        var recent = rugs
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .Select(r =>
            {
                var costs = workflowEngine.CalculateRugCosts(r);
                var current = r.WorkflowSteps
                    .OrderBy(s => s.OrderIndex)
                    .FirstOrDefault(s => s.Status is WorkflowStepStatus.InProgress or WorkflowStepStatus.Pending);
                return new RecentRugDto(
                    r.Id,
                    r.Sku,
                    r.Title,
                    r.Status.ToString(),
                    current?.ProcessStepType?.NameFa,
                    costs.TotalInvestment);
            })
            .ToList();

        var distribution = rugs
            .SelectMany(r => r.WorkflowSteps.Where(s => s.Status == WorkflowStepStatus.InProgress))
            .GroupBy(s => s.ProcessStepType?.NameFa ?? "نامشخص")
            .Select(g => new StepDistributionDto(g.Key, g.Count()))
            .ToList();

        // یک بار محاسبهٔ هزینهٔ هر فرش تا از تکرار جلوگیری شود
        var costed = rugs.Select(r => (Rug: r, Costs: workflowEngine.CalculateRugCosts(r))).ToList();

        var pipeline = costed.Where(x => x.Rug.Status == RugStatus.InProgress).Sum(x => x.Costs.TotalInvestment);
        var readyValue = costed.Where(x => x.Rug.Status == RugStatus.ReadyForSale).Sum(x => x.Costs.TotalInvestment);

        // سود تخمینی فقط برای فرش‌های فروخته‌نشده معنا دارد؛ برای فروخته‌شده‌ها
        // سود واقعی را داریم و مخلوط کردن این دو عدد را بی‌معنا می‌کند.
        var sold = costed.Where(x => x.Rug.Sale is not null).ToList();
        var unsold = costed.Where(x => x.Rug.Sale is null).ToList();

        var profit = unsold
            .Where(x => x.Costs.EstimatedMargin.HasValue)
            .Sum(x => x.Costs.EstimatedMargin!.Value);

        var actualSales = sold.Sum(x => x.Rug.Sale!.NetAmount);
        var actualProfit = sold.Sum(x => x.Rug.Sale!.NetAmount - x.Costs.TotalInvestment);
        var outstanding = sold.Sum(x => x.Rug.Sale!.OutstandingAmount);
        var batchCount = rugs.Where(r => r.BatchId.HasValue).Select(r => r.BatchId).Distinct().Count();
        // فرش‌های در جریان که هزینهٔ مرحلهٔ جاری‌شان هنوز ثبت نشده
        var pendingCost = costed.Count(x => x.Rug.Status == RugStatus.InProgress
            && x.Rug.WorkflowSteps.Any(s => s.Status == WorkflowStepStatus.InProgress && s.EffectiveCost == 0));

        return new DashboardStatsDto(
            rugs.Count,
            rugs.Count(r => r.Status == RugStatus.InProgress),
            rugs.Count(r => r.Status == RugStatus.ReadyForSale),
            rugs.Count(r => r.Status == RugStatus.Sold),
            costed.Sum(x => x.Costs.TotalInvestment),
            pipeline,
            profit,
            readyValue,
            batchCount,
            pendingCost,
            actualSales,
            actualProfit,
            outstanding,
            recent,
            distribution);
    }
}
