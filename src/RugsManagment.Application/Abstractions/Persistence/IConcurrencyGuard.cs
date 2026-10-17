namespace RugsManagment.Application.Abstractions.Persistence;

/// <summary>
/// نسخهٔ هم‌زمانیِ ردیف‌ها (ستون سیستمی <c>xmin</c> پستگرس) برای لایهٔ Application.
///
/// چرا جدا: xmin در موجودیت دامنه نیست (shadow property در EF)، پس سرویسی که «نسخه» را در DTO برمی‌گرداند
/// (<c>RugDto.Version</c>) یا نسخهٔ دیده‌شدهٔ کاربر را برای ذخیره مسلح می‌کند (PUT فرش، اندازهٔ نهایی، سطرهای
/// خرید، برش دسته) راهی به آن ندارد مگر از همین درگاه؛ Application به EF وابسته نمی‌شود. فقط برای جدول‌هایی معنا دارد که
/// xmin دارند (B.1: Rug، RugBatch، CostCategory، TenantPreferences و جدول‌های فازهای بعد).
/// </summary>
public interface IConcurrencyGuard
{
    /// <summary>
    /// نسخهٔ فعلیِ موجودیتِ ردیابی‌شده. بعد از <c>SaveChangesAsync</c> همان xmin تازه است (EF مقدار تولیدشده را
    /// برمی‌گرداند)، پس DTOیی که بعد از ذخیره ساخته می‌شود نسخهٔ درست را دارد. موجودیتِ ردیابی‌نشده یا تازه
    /// (هنوز ذخیره‌نشده) → ۰.
    /// </summary>
    uint VersionOf(object entity);

    /// <summary>
    /// نسخه‌ای که کاربر دیده بود را مسلح می‌کند: UPDATE همین ردیف با <c>WHERE xmin = expected</c> اجرا می‌شود و اگر
    /// کس دیگری در این فاصله ذخیره کرده باشد هیچ ردیفی عوض نمی‌شود و ذخیره <see cref="ConcurrencyConflictException"/>
    /// (۴۰۹) می‌دهد. موجودیت باید ردیابی‌شده باشد.
    /// <para><b>توجه:</b> بررسی فقط وقتی انجام می‌شود که خود ردیف نوشته شود؛ اگر درخواست هیچ ستونی از آن را عوض
    /// نمی‌کند (مثلاً فقط سطرهای خرید)، <c>UpdatedAt</c> را هم بنویسید تا UPDATE و در نتیجه بررسی نسخه انجام شود
    /// (همین است که دو PUT سطرهای خرید با یک نسخه را دقیقاً یک ۴۰۹ می‌کند، I.2).</para>
    /// </summary>
    void Expect(object entity, uint expectedVersion);
}
