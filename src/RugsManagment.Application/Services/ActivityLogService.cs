using RugsManagment.Application.Abstractions.Persistence;
using RugsManagment.Application.DTOs.Activity;
using RugsManagment.Domain.Entities;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.Services;

/// <summary>خواندن تاریخچهٔ فعالیت کارگاه. فقط خواندنی — لاگ ویرایش نمی‌شود.</summary>
public interface IActivityLogService
{
    Task<ActivityPageDto> ListAsync(Guid tenantId, ActivityQuery query, CancellationToken ct = default);

    /// <summary>رویدادهای یک موجودیت مشخص — مثلاً تاریخچهٔ یک فرش.</summary>
    Task<IReadOnlyList<ActivityEntryDto>> ForEntityAsync(
        Guid tenantId, string entityType, Guid entityId, CancellationToken ct = default);
}

public sealed class ActivityLogService(IRepository<AuditEntry> entries) : IActivityLogService
{
    public async Task<ActivityPageDto> ListAsync(
        Guid tenantId, ActivityQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 10, 100);

        var all = await entries.ListAsync(
            e => e.TenantId == tenantId
                 && (query.Action == null || e.Action == query.Action)
                 && (query.From == null || e.CreatedAt >= query.From)
                 && (query.To == null || e.CreatedAt <= query.To),
            ct);

        var ordered = all.OrderByDescending(e => e.CreatedAt).ToList();

        var items = ordered
            .Skip((page - 1) * size)
            .Take(size)
            .Select(ToDto)
            .ToList();

        return new ActivityPageDto(items, ordered.Count, page, size);
    }

    public async Task<IReadOnlyList<ActivityEntryDto>> ForEntityAsync(
        Guid tenantId, string entityType, Guid entityId, CancellationToken ct = default)
    {
        var all = await entries.ListAsync(
            e => e.TenantId == tenantId && e.EntityType == entityType && e.EntityId == entityId, ct);

        return all.OrderByDescending(e => e.CreatedAt).Select(ToDto).ToList();
    }

    private static ActivityEntryDto ToDto(AuditEntry e) => new(
        e.Id,
        e.UserName,
        e.Action,
        ActionLabel(e.Action),
        e.EntityType,
        e.EntityId,
        e.EntityLabel,
        e.Summary,
        e.CreatedAt);

    /// <summary>برچسب فارسی هر نوع رویداد — برای چیپ فیلتر و ستون «کنش».</summary>
    public static string ActionLabel(AuditAction action) => action switch
    {
        AuditAction.Created => "ایجاد",
        AuditAction.Updated => "ویرایش",
        AuditAction.Deleted => "حذف",
        AuditAction.Restored => "بازگردانی",
        AuditAction.StepAdvanced => "پیشبرد مرحله",
        AuditAction.StepReverted => "بازگشت مرحله",
        AuditAction.StepSkipped => "رد کردن مرحله",
        AuditAction.WorkflowChanged => "تغییر مسیر",
        AuditAction.SaleRecorded => "ثبت فروش",
        AuditAction.SaleCancelled => "لغو فروش",
        AuditAction.ProviderPaid => "پرداخت به طرف خدمات",
        AuditAction.UserInvited => "افزودن کاربر",
        AuditAction.UserDisabled => "غیرفعال کردن کاربر",
        AuditAction.PasswordChanged => "تغییر رمز",
        AuditAction.SettingsChanged => "تغییر تنظیمات",
        _ => action.ToString()
    };
}
