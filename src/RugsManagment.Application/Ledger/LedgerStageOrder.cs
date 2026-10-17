namespace RugsManagment.Application.Ledger;

/// <summary>
/// ترتیب ثابت مرحله‌های پاس سازگاری (spec D.4) — از فاز ۲ با جاهای رزروشده منتشر شده تا فازهای بعد فقط مرحلهٔ خودشان را
/// اضافه کنند. ترتیب معنادار است: مبلغ سطرهای خرید پیش از جمع خرید، ارزش دلاری خط پیش از ثبت‌های «به نسبت ارزش
/// اظهاری»، و جمع هزینهٔ فرش همیشه آخر، چون همهٔ بقیه را جمع می‌زند. آزمون معماری بررسی می‌کند عددها یکتا باشند،
/// <see cref="RugCostTotal"/> بیشینه باشد و هر مرحلهٔ ثبت‌شده یکی از همین ثابت‌ها را برگرداند.
/// </summary>
public static class LedgerStageOrder
{
    /// <summary>همپوشانی شماره‌ها (فاز ۲؛ فقط بررسی، E2).</summary>
    public const int SerialOverlap = 100;

    /// <summary>مبلغ سطرهای خرید با قاعدهٔ دسته (فاز ۲).</summary>
    public const int PurchaseLine = 200;

    /// <summary>ارزش دلاری خط‌های محموله (فاز محموله؛ رزرو).</summary>
    public const int ShipmentLineValue = 300;

    /// <summary>تقسیم ثبت‌های گروه خرید (فاز هزینه‌ها؛ رزرو).</summary>
    public const int PurchaseEntry = 400;

    /// <summary>جمع خرید هر فرش و انتشار «قیمت خرید عوض شد» (فاز ۲).</summary>
    public const int PurchaseRollup = 500;

    /// <summary>تقسیم ثبت‌های گروه هزینه (فاز هزینه‌ها؛ رزرو).</summary>
    public const int ExpenseEntry = 600;

    /// <summary>سهم درآمد صادرات (فاز هزینه‌ها؛ رزرو).</summary>
    public const int RevenueShare = 700;

    /// <summary>جمع بهای تمام‌شدهٔ هر فرش و عکس بهای فروش (فاز ۲). <b>باید آخر باشد.</b></summary>
    public const int RugCostTotal = 1000;
}
