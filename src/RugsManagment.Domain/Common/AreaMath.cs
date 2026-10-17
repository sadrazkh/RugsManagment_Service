namespace RugsManagment.Domain.Common;

/// <summary>
/// «قاعدهٔ دسته»: هر چیز اول برای یک تخته گرد می‌شود و بعد در تعداد ضرب — همه‌جا یکسان (C#، SQL،
/// <c>lib/num.ts</c>، فرمول‌های اکسل خروجی).
///
/// چرا اول گرد و بعد ضرب: اکسل بازرگان مساحت هر تخته را با ROUND(L×W,2) حساب می‌کند و بعد جمع می‌زند؛
/// اگر ما اول ضرب می‌کردیم، دستهٔ ۵۰ تختهٔ ۰٫۴۰×۰٫۴۰ به‌جای ۸٫۰۰ چیز دیگری می‌شد و جمع اظهارنامه با فایل
/// نمی‌خواند. نمونهٔ دلاری: ۰٫۱۶ متر × ۴۵٫۵۵ = ۷٫۲۸۸ → ۷٫۲۹ برای هر تخته → ۳۶۴٫۵۰ برای ۵۰ تخته
/// (نه ۳۶۴٫۴۰).
///
/// همهٔ گردکردن‌ها با <see cref="Num.Round(decimal, int)"/> (نصفه دور از صفر) است.
/// </summary>
public static class AreaMath
{
    /// <summary>ضریب دقیق متر مربع به فوت مربع (نه ۱۰٫۷۶۳۹ تقریبی).</summary>
    public const decimal SquareFeetPerSquareMeter = 10.7639104m;

    /// <summary>دقت ابعاد و مساحت (متر، دو رقم اعشار).</summary>
    public const int DimensionDecimals = 2;

    /// <summary>دقت مبلغ دلاری (دو رقم اعشار).</summary>
    public const int UsdDecimals = 2;

    /// <summary>
    /// مساحت یک تخته: ROUND(L×W, 2). هر ضلع هم اول به دو رقم گرد می‌شود، چون ابعاد در دیتابیس
    /// numeric(9,2) است؛ پس عددی که از اکسل با خطای ممیز آمده (2.2999999999999998) همان ۲٫۳۰ حساب
    /// می‌شود و نتیجه با ستون ذخیره‌شده یکی است.
    /// </summary>
    public static decimal PieceArea(decimal length, decimal width)
        => Num.Round(
            Num.Round(length, DimensionDecimals) * Num.Round(width, DimensionDecimals),
            DimensionDecimals);

    /// <summary>مساحت یک ردیف: مساحت تخته × تعداد.</summary>
    public static decimal LineArea(decimal pieceArea, int quantity)
        => pieceArea * NonNegative(quantity);

    /// <summary>ارزش دلاری یک تخته: ROUND(مساحت تخته × دلار برای هر متر، 2).</summary>
    public static decimal PieceUsd(decimal pieceArea, decimal usdPerM2)
        => Num.Round(pieceArea * usdPerM2, UsdDecimals);

    /// <summary>ارزش دلاری یک ردیف: دلار تخته × تعداد.</summary>
    public static decimal LineUsd(decimal pieceUsd, int quantity)
        => pieceUsd * NonNegative(quantity);

    /// <summary>مبلغ متری یک تخته: ROUND(نرخ × مساحت تخته، MoneyScale).</summary>
    public static decimal PieceMoney(decimal ratePerM2, decimal pieceArea, int moneyScale)
        => Num.Round(ratePerM2 * pieceArea, moneyScale);

    /// <summary>مبلغ دانه‌ای یک تخته: ROUND(نرخ، MoneyScale).</summary>
    public static decimal PieceMoneyPerPiece(decimal ratePerPiece, int moneyScale)
        => Num.Round(ratePerPiece, moneyScale);

    /// <summary>مبلغ یک ردیف: مبلغ تخته × تعداد.</summary>
    public static decimal LineMoney(decimal pieceMoney, int quantity)
        => pieceMoney * NonNegative(quantity);

    /// <summary>
    /// تبدیل مبلغِ یک تخته از ارز دیگر (دلار، یا ریال↔تومان با نرخ ۱۰ / ۰٫۱) به ارز پایه، باز هم
    /// برای هر تخته: ROUND(مبلغ تخته × نرخ، MoneyScale). برای رسید متری، مبلغ تخته پیش از این با
    /// <see cref="PieceUsd"/> (دو رقم) حساب شده است. نمونه: ۷٫۲۹ × ۱۰۵٬۰۰۰٫۵ → ۷۶۵٬۴۵۴ برای هر تخته
    /// → ۳۸٬۲۷۲٬۷۰۰ برای ۵۰ تخته (نه ROUND(۳۶۴٫۵۰ × نرخ) = ۳۸٬۲۷۲٬۶۸۲).
    /// </summary>
    public static decimal ConvertPiece(decimal pieceAmount, decimal rate, int moneyScale)
        => Num.Round(pieceAmount * rate, moneyScale);

    /// <summary>مساحت به فوت مربع (برای قراردادهای قدیمی کارگاه).</summary>
    public static decimal ToSquareFeet(decimal squareMeters) => squareMeters * SquareFeetPerSquareMeter;

    private static int NonNegative(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);
        return quantity;
    }
}
