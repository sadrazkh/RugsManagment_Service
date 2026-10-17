namespace RugsManagment.Application.DTOs.Shipments;

/// <summary>یک ردیف جدول ارزش گمرکی؛ <see cref="Id"/> null برای ردیف تازه در جایگزینی کامل.</summary>
public record DeclaredValueRateDto(Guid? Id, string KeyValue, decimal UsdPerM2);

/// <summary>
/// جدول ارزش گمرکی (<c>GET /api/declared-values</c>، Floor) با مهر هم‌زمانی. <see cref="KeyField"/> کلید فیلدی است که جدول
/// بر اساسش نرخ می‌دهد (ترجیح کارگاه، پیش‌فرض <c>rug.origin</c>) و <see cref="FallbackUsdPerM2"/> نرخ پیش‌فرض.
/// </summary>
public record DeclaredValuesDto(
    IReadOnlyList<DeclaredValueRateDto> Rates,
    int Stamp,
    // ── افزوده بر I.3 (دنباله‌دار) ─────────────────────────────────────────
    string? KeyField = null,
    decimal? FallbackUsdPerM2 = null);

/// <summary>
/// بازرگان کلیدی را که در جدول نیست اضافه می‌کند (<c>POST /api/declared-values</c>، Business): فقط افزودن؛ کلید موجود
/// ۴۰۹، نرخ منفی ۴۰۰، مهر کهنه ۴۰۹ (C.5).
/// </summary>
public record AddDeclaredValueRequest(string KeyValue, decimal UsdPerM2, int Stamp);

/// <summary>جایگزینی کامل جدول (<c>PUT /api/settings/declared-values</c>، Configure)؛ مهر کهنه ۴۰۹.</summary>
public record ReplaceDeclaredValuesRequest(IReadOnlyList<DeclaredValueRateDto> Rates, int Stamp);

/// <summary>نتیجهٔ نوشتن جدول: چند خط محمولهٔ باز (بی نرخ دستی) با این کلیدها دوباره ارزش‌گذاری شد، و مهر تازه.</summary>
public record DeclaredValuesWriteResultDto(int AffectedOpenLines, int Stamp);
