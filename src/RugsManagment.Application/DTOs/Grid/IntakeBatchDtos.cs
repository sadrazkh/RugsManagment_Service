namespace RugsManagment.Application.DTOs.Grid;

/// <summary>
/// یک «ورودی» (فرش‌های یک فروشنده در یک روز) برای جدول ورود: نوار «ورودی‌های اخیر» و «ورودی امروز».
///
/// چرا رکورد جدا و نه <c>RugBatchDto</c>: همان عضوهای <c>RugBatchDto</c> را به همان نام دارد (پس JSONش برای هر خوانندهٔ
/// قدیمی هم‌شکل است) به‌اضافهٔ فروشنده، شماره، روز و جمع تخته‌ها و متراژ (I.2). <c>RugBatchDto</c> فایلی است که این بسته
/// صاحبش نیست؛ افزودن همین عضوها به آن در گزارش فاز آمده.
/// </summary>
/// <param name="RugCount">تعداد ردیف‌ها (فرش‌ها).</param>
/// <param name="CurrentStepSummary">خلاصهٔ مرحلهٔ جاری (همیشه null این‌جا؛ برای هم‌شکلی با <c>RugBatchDto</c>).</param>
/// <param name="ReceivedOn">روز ایرانِ ورودی (کلید ورودی، B.5).</param>
/// <param name="Pieces">Σ تعداد تخته‌ها.</param>
/// <param name="AreaM2">Σ متراژ کل ردیف‌ها.</param>
/// <param name="ReceivedOnJalali">همان روز به شمسی با ارقام لاتین («1405/06/14»)؛ نمایش ارقام را فارسی می‌کند.</param>
public record IntakeBatchDto(
    Guid Id,
    string Name,
    string? Description,
    DateTimeOffset? ReceivedAt,
    int RugCount,
    string? CurrentStepSummary,
    string? SellerName,
    int Number,
    DateOnly? ReceivedOn,
    int Pieces,
    decimal AreaM2,
    string? ReceivedOnJalali);

/// <summary>
/// <c>POST /api/batches/today</c>: پیدا کردن یا ساختن ورودیِ (روز، فروشنده). <see cref="ReceivedAt"/> روز ایران به‌صورت
/// <c>yyyy-MM-dd</c> یا یک لحظهٔ ISO کامل (که روز ایرانش گرفته می‌شود)؛ خالی یعنی امروز.
/// </summary>
public record TodayBatchRequest(string? SellerName, string? ReceivedAt);

/// <summary>
/// داده‌های صفحهٔ «ورود فرش» (<c>/Intake</c>) برای props جزیرهٔ <c>intake-grid</c> (I.5).
/// </summary>
/// <param name="Date">روز ایرانِ جدول، ISO <c>yyyy-MM-dd</c>.</param>
/// <param name="Title">«ورودی امروز» / «ورودی دیروز» / «ورودی ۱۴۰۵/۰۶/۱۰».</param>
/// <param name="DefaultSeller">فروشندهٔ تازه‌ترین ورودیِ همان روز (پیش‌فرض ردیف تازه)؛ null اگر نیست.</param>
/// <param name="SerialMode">حالت شمارهٔ کارگاه (عدد enum).</param>
/// <param name="MaxPlausibleSideM">ضلعِ بزرگ‌تر از این «متر یا سانت؟» می‌پرسد.</param>
public record IntakePageDto(string Date, string Title, string? DefaultSeller, int SerialMode, decimal MaxPlausibleSideM);
