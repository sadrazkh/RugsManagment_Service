using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.DTOs.Rugs;

/// <summary>
/// <c>GET /api/rugs/next-serial</c>: شمارهٔ پیشنهادی بعدی (قاعدهٔ G.4، <c>SerialSuggester</c>):
/// <c>max(SerialStart ?? 1, MAX(COALESCE(SerialTo, SerialFrom)) + 1)</c> روی فرش‌های فعال کارگاه، صادرشده و فروخته‌شده هم.
/// بعد از «3985 - 4003» می‌شود 4004 (آخرِ بازه + ۱، نه اولِ بازه + تعداد).
/// </summary>
/// <param name="Next">شمارهٔ بعدی؛ در حالت «دستی» (<see cref="SerialMode.Manual"/>) همیشه null، چون پیشنهادی نشان داده نمی‌شود.</param>
public record NextSerialDto(int? Next);

/// <summary>
/// نوع تطبیق شماره در <c>GET /api/rugs/by-serial/{serial}</c> (C.3). رشته است نه enum، چون در JSON همین دو کلمه
/// قرارداد است (<c>'exact' | 'inRange'</c>) و سرور enum را عدد می‌فرستد.
/// </summary>
public static class SerialMatchType
{
    /// <summary>
    /// شمارهٔ نوشته‌شده خودِ شمارهٔ فرش است: <c>SerialNo</c> همان شکل نمایشی است، یا عددِ تک برابر شمارهٔ تکِ فرش
    /// است («0123» و «123»).
    /// </summary>
    public const string Exact = "exact";

    /// <summary>
    /// عددِ تک داخل بازهٔ یک دسته است («3925» در «3911 - 3960»)؛ جزیره می‌پرسد «کل دسته را بگذار» یا «جدا کن».
    /// </summary>
    public const string InRange = "inRange";
}

/// <summary>
/// پاسخ <c>GET /api/rugs/by-serial/{serial}</c> (پیدا نشد → ۴۰۴). اسکنر QR/بارکد و «شماره را بزنید» از همین استفاده
/// می‌کنند؛ تطبیق همیشه دقیق است (هرگز «شامل» یا شبیه).
/// </summary>
/// <param name="Rug">ردیف فرش (همان شکل فهرست؛ پول برای غیر Business null).</param>
/// <param name="Location">جای فرش (C.10)؛ همان <see cref="RugListItemDto.Location"/>، جدا آمده چون قرارداد I.2 همین است.</param>
/// <param name="MatchType"><see cref="SerialMatchType.Exact"/> یا <see cref="SerialMatchType.InRange"/>.</param>
public record RugBySerialDto(
    RugListItemDto Rug,
    RugLocation Location,
    string MatchType);
