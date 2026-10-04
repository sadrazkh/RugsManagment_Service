namespace RugsManagment.Infrastructure.Persistence.Seed;

/// <summary>
/// روی کلاس migration‌ای گذاشته می‌شود که دادهٔ موجودِ یک جدول را تغییر می‌دهد یا دور می‌ریزد
/// (تبدیل نوع ستون، ادغام ستون‌ها، حذف ستونِ پُر، …).
///
/// چرا: از این به بعد migrationها خودکار هنگام استارت اجرا می‌شوند و کسی پیش از اجرا SQL را
/// نمی‌خواند. <see cref="Migrator"/> اگر چنین migration‌ای در انتظار باشد و جدولش حداقل یک ردیف
/// داشته باشد، استارت را متوقف می‌کند تا وقتی که اپراتور پشتیبان بگیرد (scripts/db-backup) و
/// شناسهٔ همان migration را در <c>Database:ConfirmedBackupFor</c> بگذارد. روی دیتابیس خالی
/// (نصب تازه، CI) چیزی برای از دست رفتن نیست و مانعی ایجاد نمی‌شود.
///
/// اگر migration چند جدول را تغییر می‌دهد، برای هر جدول یک بار تکرار شود.
/// </summary>
/// <example><code>[RequiresBackup("Rugs")] public partial class SplitRugDimensions : Migration { … }</code></example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class RequiresBackupAttribute : Attribute
{
    public RequiresBackupAttribute(string table)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
        Table = table;
    }

    /// <summary>نام جدول در دیتابیس، دقیقاً با همان حروف (مثلاً <c>Rugs</c>).</summary>
    public string Table { get; }
}
