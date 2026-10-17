namespace RugsManagment.Domain.Enums;

/// <summary>
/// کدام اندازه پایهٔ متراژ یک هزینه است.
///
/// شستشو و رفوگری روی اندازهٔ رسیدن فرش انجام می‌شوند، ولی کرایه و گمرک روی اندازهٔ نهایی؛ پس
/// هر نوع هزینه پایهٔ خودش را دارد و کارگاه یک پیش‌فرض (<c>TenantPreferences.DefaultAreaBasis</c>).
/// </summary>
public enum AreaBasis
{
    /// <summary>اندازهٔ اول (هنگام رسیدن فرش)</summary>
    Intake = 0,

    /// <summary>اندازهٔ مؤثر: اندازهٔ نهایی اگر ثبت شده، وگرنه اندازهٔ اول</summary>
    Effective = 1
}
