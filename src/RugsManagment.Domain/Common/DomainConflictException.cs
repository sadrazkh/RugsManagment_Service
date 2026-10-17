namespace RugsManagment.Domain.Common;

/// <summary>
/// درخواست درست است ولی با وضع فعلی داده جور نیست → ۴۰۹ با بدنهٔ <c>{message, field}</c> (و
/// <see cref="Extensions"/> اگر باشد). نمونه‌ها: فرش در نگله است و حذف نمی‌شود، محموله قطعی است،
/// شماره داخل دستهٔ دیگری است، تعداد رقم اعشار مبلغ بعد از ثبت اولین مبلغ.
///
/// فازهای بعد از همین ارث می‌برند (<c>ShipmentLockedException</c>، <c>SerialOverlapException</c>،
/// <c>StaleStampException</c>) و فیلتر خطای API و صفحه‌ها دیگر هرگز ویرایش نمی‌شوند: هر دادهٔ اضافهٔ
/// پاسخ (مثلاً «شمارهٔ آزاد بعدی») از <see cref="Extensions"/> خوانده می‌شود.
/// </summary>
public class DomainConflictException : Exception
{
    public DomainConflictException(string message, string? field = null)
        : base(message)
    {
        Field = field;
    }

    public DomainConflictException(string message, string? field, Exception? inner)
        : base(message, inner)
    {
        Field = field;
    }

    /// <summary>فیلدی که تعارض به آن مربوط است (camelCase، مثل «rug.serial» یا «moneyScale»)؛ null یعنی کل درخواست.</summary>
    public string? Field { get; }

    /// <summary>
    /// کلیدهای اضافه‌ای که فیلتر کنار <c>message</c> و <c>field</c> در بدنهٔ ۴۰۹ می‌نویسد (کلید camelCase،
    /// مقدار قابل‌سریال‌سازی). پیش‌فرض هیچ؛ زیرکلاس‌ها override می‌کنند، مثلاً <c>{ nextFree: 4005 }</c>.
    /// </summary>
    public virtual IReadOnlyDictionary<string, object?>? Extensions => null;
}
