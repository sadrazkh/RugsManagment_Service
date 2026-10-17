namespace RugsManagment.Domain.Enums;

/// <summary>
/// وضعیت کلی یک فرش در چرخه عمر (از ثبت تا فروش).
/// </summary>
public enum RugStatus
{
    /// <summary>تازه ثبت شده؛ هنوز مسیر فرایند شروع نشده</summary>
    Draft = 0,

    /// <summary>حداقل یک مرحله فعال یا در صف است</summary>
    InProgress = 1,

    /// <summary>همه مراحل تمام شده؛ آمادهٔ قیمت‌گذاری و فروش</summary>
    ReadyForSale = 2,

    /// <summary>فروخته شده</summary>
    Sold = 3,

    /// <summary>خارج از گردش فعال (بایگانی)</summary>
    Archived = 4,

    /// <summary>
    /// «صادر شده»: فرش در محموله‌ای است که قطعی شده. فقط قطعی‌کردن محموله آن را می‌گذارد و باز
    /// کردن دوبارهٔ محموله برش می‌دارد؛ گردش کار کارگاه هرگز رویش نمی‌نویسد (Sold مقدم است).
    /// </summary>
    Exported = 5
}
