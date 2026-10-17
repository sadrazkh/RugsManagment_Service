namespace RugsManagment.Domain.Enums;

/// <summary>
/// اینکه Enter در جدول‌های ورود اطلاعات مکان‌نما را به کدام خانه ببرد — ترجیح هر کاربر.
/// کسی که از روی کاغذ ستونی می‌نویسد «پایین» می‌خواهد و کسی که ردیف‌به‌ردیف، «کنار».
/// </summary>
public enum GridEnterDirection
{
    /// <summary>خانهٔ زیرین همان ستون</summary>
    Down = 0,

    /// <summary>خانهٔ بعدی همان ردیف (در راست‌به‌چپ یعنی سمت چپ)</summary>
    Right = 1
}
