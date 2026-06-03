using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// کاربر سیستم — یا ادمین کل (بدون Tenant) یا عضو یک کارگاه.
/// </summary>
public class User : BaseEntity
{
    /// <summary>برای SystemAdmin خالی است؛ برای بقیه شناسه کارگاه</summary>
    public Guid? TenantId { get; set; }

    public string Email { get; set; } = string.Empty;

    /// <summary>رمز هش‌شده — هرگز متن خام ذخیره نمی‌شود</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastLoginAt { get; set; }

    public Tenant? Tenant { get; set; }
}
