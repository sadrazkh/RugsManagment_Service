using RugsManagment.Application.DTOs.Import;
namespace RugsManagment.Application.Common;
public static class ImportMessages
{
    public static string Text(string code) => code switch
    {
        "E_NOT_XLSX" => "فقط فایل اکسل با پسوند xlsx پذیرفته می‌شود.",
        "E_FILE_TOO_LARGE" => "حجم فایل باید کمتر از ۱۰ مگابایت باشد.",
        "E_ZIP_BOMB" => "فایل فشرده بیش از حد بزرگ یا پیچیده است.",
        "E_NO_HEADER" => "ستون‌ها را نشناختم؛ از تنظیم‌کننده کمک بگیرید.",
        "E_TOO_MANY_ROWS" => "فایل حداکثر ۱۰٬۰۰۰ ردیف دارد.",
        "E_SERIAL_INVALID" => "شماره را مثل ۳۷۷۸ یا ۳۹۱۱ - ۳۹۶۰ بنویسید.",
        "E_RANGE_REVERSED" => "شمارهٔ اول دسته باید کوچک‌تر باشد.",
        "E_RANGE_TOO_LONG" => "دسته حداکثر ۱۰٬۰۰۰ شماره دارد.",
        "E_DIM_INVALID" => "اندازه باید از ۰٫۰۵ تا ۳۰ متر و با حداکثر دو رقم اعشار باشد.",
        "E_DIM_MISSING" => "طول و عرض را وارد کنید.",
        "E_QTY_INVALID" => "تعداد تخته درست نیست یا از بازهٔ شماره بیشتر است.",
        "E_SERIAL_DUP_IN_FILE" => "این شماره در فایل تکرار شده است.",
        "E_SERIAL_OVERLAP_IN_FILE" => "این دسته با شمارهٔ ردیف دیگری هم‌پوشانی دارد.",
        "E_SERIAL_EXISTS" => "این شماره از قبل در انبار هست.",
        "E_SERIAL_LOCKED" => "این فرش در محمولهٔ قطعی است و تغییر نمی‌کند.",
        "W_QTY_FROM_MATERIAL" => "تعداد تخته از ستون جنس خوانده شد.",
        "W_RANGE_QTY_DIFF" => "تعداد تخته با تعداد شماره‌های دسته برابر نیست؛ تعداد نوشته‌شده حفظ شد.",
        "W_ROW_ADOPTED" => "این ردیف به نگلهٔ بعد پیوست.",
        "W_FOOTER_LABEL" => "شمارهٔ نوشته‌شده در جمع نگله درست نیست؛ شمارهٔ خود نگله حفظ شد.",
        "W_MERGED_SPLIT" => "نوشتهٔ ادغام‌شده به محل بافت و جنس جدا شد.",
        "W_SERIAL_OUT_OF_SEQUENCE" => "این شماره از شماره‌های قبلی کوچک‌تر است.",
        "W_MISSING_FIELD" => "این مشخصه در فایل خالی است.",
        "W_FOOTER_MISMATCH" => "جمع نگله با ردیف‌های آن نمی‌خواند.",
        "W_SUMMARY_MISMATCH" => "جمع خلاصه با ردیف‌های نگله نمی‌خواند.",
        "W_DUPLICATE_FILE" => "این فایل قبلاً بارگذاری شده است.",
        "W_DIM_CM" => "اندازه شبیه سانتی‌متر است؛ متر یا سانتی‌متر را مشخص کنید.",
        "W_NO_DECLARED_VALUE" => "ارزش دلاری این ردیف مشخص نیست.",
        "W_DIM_SNAPPED" => "خطای اعشاری اکسل در اندازه اصلاح شد.",
        "I_VALUE_SNAPPED" => "خطای اعشاری اکسل در جمع اصلاح شد.",
        "I_REPEATED_HEADER" => "عنوان یا سرستون تکراری کنار گذاشته شد.",
        "I_SUMMARY_DETECTED" => "جدول خلاصه برای کنترل جمع‌ها شناسایی شد.",
        "I_TITLE_DATE" => "تاریخ اظهار از عنوان فایل خوانده شد.",
        "I_ROW_SKIPPED" => "این ردیف را کنار گذاشته‌اید؛ وارد نمی‌شود و در جمع‌ها حساب نمی‌شود.",
        _ => "این ردیف را بررسی کنید."
    };
    public static ImportIssueDto Issue(string code, string? field = null)
        => new(code, code.StartsWith("E_", StringComparison.Ordinal) ? "error"
            : code.StartsWith("I_", StringComparison.Ordinal) || code == "W_DIM_SNAPPED" ? "info" : "warning", Text(code), field);
}
