namespace RugsManagment.Domain.Common;

/// <summary>
/// یک ضلع فرش به متر، که مرزهای سخت G.4 را از پیش گذرانده است: از ۰٫۰۵ تا ۳۰ متر و حداکثر دو رقم اعشار.
///
/// چرا نوعِ جدا و نه decimal خالی: ستون‌های اندازه <c>numeric(9,2)</c> هستند و دیتابیس رقم سوم را بی‌صدا گرد
/// می‌کند؛ اگر ۲٫۰۸۵ تا دیتابیس برسد، چیزی که کاربر نوشت با چیزی که ذخیره شد فرق دارد. با این نوع، هر نوشتنِ
/// اندازه (اندازهٔ اول، اندازهٔ نهایی، برش دسته) پیش از ذخیره رد یا پذیرفته می‌شود. پرسشِ «متر یا سانت؟» و
/// تبدیل ۲۰۸ ← ۲٫۰۸ کار تجزیه‌گر ورودی است، نه این نوع: این‌جا فقط عدد متریِ نهایی سنجیده می‌شود.
///
/// <c>default(Dimension)</c> صفر است و معتبر نیست؛ فقط از <see cref="From"/> یا <see cref="TryFrom"/> بسازید.
/// </summary>
public readonly record struct Dimension
{
    /// <summary>کوچک‌ترین ضلع (۵ سانت).</summary>
    public const decimal MinMeters = 0.05m;

    /// <summary>بزرگ‌ترین ضلع (۳۰ متر).</summary>
    public const decimal MaxMeters = 30m;

    /// <summary>بیشترین رقم اعشار (ستون numeric(9,2)).</summary>
    public const int Decimals = 2;

    /// <summary>کد پیام — همان <c>E_DIMENSION</c> در <c>Messages.fa</c>.</summary>
    public const string ErrorCode = "E_DIMENSION";

    /// <summary>متن پیام — باید با <c>Messages.Dimension</c> یکی بماند (آزمون واحد بررسی می‌کند).</summary>
    public const string ErrorMessage = "اندازه را به متر بنویسید، مثل ۲٫۰۸ (از ۵ سانت تا ۳۰ متر)";

    private Dimension(decimal meters) => Meters = meters;

    /// <summary>ضلع به متر، دقیقاً همان که ذخیره می‌شود.</summary>
    public decimal Meters { get; }

    /// <summary>آیا این عدد یک ضلع مجاز است.</summary>
    public static bool IsValid(decimal meters)
        => meters is >= MinMeters and <= MaxMeters && Num.HasAtMostDecimals(meters, Decimals);

    /// <summary>ضلع معتبر، یا <c>false</c> بدون استثنا (برای پیش‌نمایش جدول).</summary>
    public static bool TryFrom(decimal meters, out Dimension dimension)
    {
        if (IsValid(meters))
        {
            // ۲٫۰۸۰ و ۲٫۰۸ یک عددند؛ شکلِ دورقمی ذخیره می‌شود تا مقایسهٔ متنی هم یکی بدهد
            dimension = new Dimension(decimal.Round(meters, Decimals));
            return true;
        }

        dimension = default;
        return false;
    }

    /// <summary>
    /// ضلع معتبر؛ وگرنه <see cref="DomainValidationException"/> (۴۰۰) با پیام فارسی روی فیلد
    /// <paramref name="field"/> (مثلاً «rug.intakeLength»).
    /// </summary>
    public static Dimension From(decimal meters, string field = "length")
        => TryFrom(meters, out var dimension)
            ? dimension
            : throw new DomainValidationException(ErrorMessage, field);

    public static implicit operator decimal(Dimension dimension) => dimension.Meters;

    public override string ToString() => Meters.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
}
