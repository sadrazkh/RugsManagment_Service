using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Shipments;
using RugsManagment.Application.Features.Shipments;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>
/// جدول ارزش گمرکی (دلار هر متر برای هر مقدار فیلد کلید، C.5) — سه در با سه سیاست (I.2):
/// <list type="bullet">
/// <item><c>GET /api/declared-values</c> (Floor): جدول و مهر. دلار گمرکی پول داخلی نیست و انباردار هم می‌بیند (A.5). عمداً
/// بیرون از <c>/api/settings/*</c> است تا «هر <c>/api/settings/*</c> فقط Configure» همچنان درست بماند.</item>
/// <item><c>POST /api/declared-values</c> (Business): بازرگان کلید تازه‌ای را که در جدول نیست خودش اضافه می‌کند (تراشهٔ
/// <c>E_NO_DECLARED_VALUE</c>، «نرخ بگذارید»)؛ کلید موجود ۴۰۹، نرخ منفی ۴۰۰. ویرایش و حذف نرخ‌ها Configure می‌ماند.</item>
/// <item><c>PUT /api/settings/declared-values</c> (Configure): جایگزینی کل جدول از صفحهٔ تنظیمات؛ مهر کهنه ۴۰۹
/// (<see cref="StaleStampException"/>).</item>
/// </list>
/// هر نوشتن خط‌های محموله‌های باز را (بی نرخ دستی) در همان درخواست دوباره ارزش‌گذاری می‌کند و تعدادشان را برمی‌گرداند.
/// </summary>
[Route("api")]
public sealed class DeclaredValuesApiController(IDeclaredValueService declaredValues) : ApiControllerBase
{
    /// <summary>جدول با مهر، فیلد کلید و نرخ پیش‌فرض.</summary>
    [HttpGet("declared-values")]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await declaredValues.GetAsync(User.RequireTenantId(), ct));

    /// <summary>افزودن کلید تازه: <c>{keyValue, usdPerM2, stamp}</c> → <c>{affectedOpenLines, stamp}</c>.</summary>
    [HttpPost("declared-values")]
    [Authorize(Policy = Policies.Business)]
    public async Task<IActionResult> Add([FromBody] AddDeclaredValueRequest request, CancellationToken ct)
        => Ok(await declaredValues.AddAsync(User.RequireTenantId(), request, ct));

    /// <summary>جایگزینی کل جدول: <c>{rates[], stamp}</c> → <c>{affectedOpenLines, stamp}</c>.</summary>
    [HttpPut("settings/declared-values")]
    [Authorize(Policy = Policies.Configure)]
    public async Task<IActionResult> Replace([FromBody] ReplaceDeclaredValuesRequest request, CancellationToken ct)
        => Ok(await declaredValues.ReplaceAsync(User.RequireTenantId(), request, ct));
}
