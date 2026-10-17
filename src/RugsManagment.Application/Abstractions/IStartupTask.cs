namespace RugsManagment.Application.Abstractions;

/// <summary>
/// کاری که یک بار هنگام استارت، بعد از migration و seed، اجرا می‌شود (به ترتیب <see cref="Order"/>).
///
/// ماژول‌ها کار استارت خود را با ثبت یک پیاده‌سازی در DI اضافه می‌کنند، بدون اینکه Program.cs را
/// ویرایش کنند. کل زنجیره زیر یک قفل advisory پستگرس اجرا می‌شود، پس دو نمونهٔ هم‌زمانِ برنامه
/// با هم مسابقه نمی‌دهند.
/// </summary>
public interface IStartupTask
{
    /// <summary>ترتیب اجرا؛ کوچک‌تر زودتر.</summary>
    int Order { get; }

    Task RunAsync(IServiceProvider services, CancellationToken cancellationToken);
}

/// <summary>
/// خطایی که استارت را <b>فوراً</b> متوقف می‌کند و نباید با تلاش دوباره پوشانده شود
/// (مثلاً migration‌ای که پشتیبان‌گیری می‌خواهد، یا مدلی که migration‌اش ساخته نشده).
/// </summary>
public class StartupAbortException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// migration در انتظار روی جدولی دادهٔ واقعی را تغییر می‌دهد و هنوز پشتیبانِ تأییدشده ندارد.
/// </summary>
public sealed class MigrationBackupRequiredException(string migrationId, string table)
    : StartupAbortException(
        $"migration «{migrationId}» دادهٔ جدول «{table}» را تغییر می‌دهد و این جدول خالی نیست. " +
        $"اول پشتیبان بگیرید (scripts/db-backup) و بعد Database:ConfirmedBackupFor را برابر «{migrationId}» بگذارید.")
{
    public string MigrationId { get; } = migrationId;
    public string Table { get; } = table;
}
