using RugsManagment.Domain.Common;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Domain.Entities;

/// <summary>
/// کارگاه / مشتری سامانه — هر فرش‌باف (یا مجموعه) یک Tenant جدا دارد.
/// ادمین سیستم Tenant می‌سازد؛ کاربران همان Tenant فقط دادهٔ خودشان را می‌بینند.
/// </summary>
public class Tenant : BaseEntity
{
    /// <summary>نام نمایشی کارگاه</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>شناسه یکتا در URL و ورود (مثلاً demo-weaver) — تکراری مجاز نیست</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>اگر false باشد کاربران آن کارگاه نمی‌توانند وارد شوند</summary>
    public bool IsActive { get; set; } = true;

    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }

    /// <summary>پایان اشتراک (اختیاری) — برای مدل فروش SaaS</summary>
    public DateTimeOffset? SubscriptionExpiresAt { get; set; }

    /// <summary>
    /// واحد پول نمایشی. مبالغ همیشه به یک واحد ذخیره می‌شوند؛ این فقط برچسب نمایش است،
    /// چون بعضی کارگاه‌ها با ریال کار می‌کنند و بعضی با تومان.
    /// </summary>
    public CurrencyUnit Currency { get; set; } = CurrencyUnit.Toman;

    /// <summary>نام فایل لوگو (بدون مسیر) — روی برچسب و سربرگ گزارش استفاده می‌شود</summary>
    public string? LogoFileName { get; set; }

    /// <summary>قالب گردش کاری که هنگام ثبت فرش جدید از پیش انتخاب می‌شود</summary>
    public Guid? DefaultWorkflowTemplateId { get; set; }

    public ICollection<User> Users { get; set; } = [];
    public ICollection<WorkflowTemplate> WorkflowTemplates { get; set; } = [];
    public ICollection<Rug> Rugs { get; set; } = [];
}
