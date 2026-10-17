namespace RugsManagment.Application.DTOs.Rugs;

/// <summary>
/// <c>PUT /api/rugs/{id}/final-size</c> (Floor) → <see cref="RugDto"/> تازه (C.4).
///
/// دقیقاً یکی از سه شکل: (۱) <see cref="Length"/> و <see cref="Width"/> هر دو پر؛ (۲) <see cref="SameAsIntake"/> = true
/// («همان اندازه»، اندازهٔ اول صریحاً به‌عنوان نهایی ثبت می‌شود)؛ (۳) <see cref="Clear"/> = true (برگشت به اندازهٔ اول).
/// هر ترکیب دیگری ۴۰۰ است. فرشِ محمولهٔ قطعی‌شده ۴۰۹ می‌گیرد (قفل). اندازهٔ نهایی هرگز سطر خرید «هر متر» را عوض
/// نمی‌کند (آن روی اندازهٔ اول است).
/// </summary>
/// <param name="Length">طول نهایی (متر، ۰٫۰۵ تا ۳۰، حداکثر دو رقم اعشار).</param>
/// <param name="Width">عرض نهایی.</param>
/// <param name="SameAsIntake">true = اندازهٔ نهایی برابر اندازهٔ اول.</param>
/// <param name="Clear">true = اندازهٔ نهایی پاک شود.</param>
/// <param name="Version">نسخهٔ فرش (<see cref="RugDto.Version"/>)؛ کهنه → ۴۰۹.</param>
public record SetFinalSizeRequest(
    decimal? Length,
    decimal? Width,
    bool? SameAsIntake,
    bool? Clear,
    uint Version);

/// <summary>
/// <c>POST /api/rugs/{id}/split</c> (Floor): برش یک دسته به دو ردیف (D.9).
///
/// پیش‌شرط‌ها: تعداد دسته دست‌کم ۲؛ ۱ ≤ <see cref="Quantity"/> &lt; تعداد؛ فروش ندارد؛ محموله‌اش قطعی نشده (وگرنه ۴۰۹).
///
/// <para><b>شماره‌ها.</b> هر دو null → خودکار، فقط وقتی طول بازه = تعداد: فرش تازه <see cref="Quantity"/> شمارهٔ آخر را
/// می‌گیرد («3911 - 3960» × ۵۰، برش ۲۰ ← «3911 - 3940» و «3941 - 3960»). در غیر این صورت (بازهٔ بلندتر از تعداد یا
/// شمارهٔ متنی) هر دو لازم‌اند و هر کدام مثل خانهٔ شماره خوانده می‌شود («3911 - 3940»، «۳۹۴۱ تا ۳۹۶۰»)، باید تعداد خودش را
/// بپوشاند و در همان پاس با بقیهٔ فرش‌ها همپوشانی‌سنجی می‌شود (۴۰۹ با <c>field: rug.serial</c>).</para>
/// </summary>
/// <param name="Quantity">تعداد تخته‌ای که به فرش تازه می‌رود.</param>
/// <param name="SerialFrom">شمارهٔ تازهٔ فرشِ مبدأ (آنچه می‌ماند)؛ null برای حالت خودکار.</param>
/// <param name="SerialTo">شمارهٔ فرشِ تازه (آنچه جدا می‌شود)؛ null برای حالت خودکار.</param>
/// <param name="Version">نسخهٔ فرش مبدأ؛ کهنه → ۴۰۹.</param>
public record SplitRugRequest(
    int Quantity,
    string? SerialFrom,
    string? SerialTo,
    uint Version);

/// <summary>پاسخ برش: فرش مبدأ با تعداد کمتر و فرش تازه، هر دو بعد از همان پاس (بها و سهم‌ها تازه).</summary>
/// <param name="Original">فرش مبدأ.</param>
/// <param name="Created">فرش تازه (SKU تازه از شمارنده).</param>
public record SplitRugResultDto(
    RugDto Original,
    RugDto Created);
