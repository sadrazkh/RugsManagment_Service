namespace RugsManagment.Domain.Enums;

/// <summary>
/// فیلد سفارشی روی کدام موجودیت می‌نشیند. امروز فقط فرش؛ عدد صریح دارد تا اگر روزی فیلدِ محموله
/// یا نگله اضافه شد، ستون موجود معنای تازه نگیرد.
/// </summary>
public enum CustomFieldScope
{
    /// <summary>فرش (مقدار در <c>Rug.MetadataJson</c>)</summary>
    Rug = 0
}
