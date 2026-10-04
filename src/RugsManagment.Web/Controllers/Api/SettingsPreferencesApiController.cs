using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.Features.Platform;
using RugsManagment.Application.Features.Platform.DTOs;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>
/// ترجیحات کارگاه (<c>/api/settings/preferences</c>) — فقط تنظیم‌کننده (Configure)، مثل هر مسیر زیر
/// <c>/api/settings</c>. فرم صفحهٔ «ترجیحات» (<c>preferences-form</c>) همین را می‌خواند و می‌نویسد.
///
/// قواعد ذخیره در سرویس است: نسخهٔ کهنه ۴۰۹، تغییر تعداد رقم اعشار مبلغ بعد از ثبت اولین مبلغ ۴۰۹ با
/// <c>field: "moneyScale"</c>، مقدار نامعتبر ۴۰۰ با خطای هر خانه.
/// </summary>
[Route("api/settings/preferences")]
[Authorize(Policy = Policies.Configure)]
public sealed class SettingsPreferencesApiController(ITenantPreferencesService preferences) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await preferences.GetAsync(User.RequireTenantId(), ct));

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] TenantPreferencesDto request, CancellationToken ct)
        => Ok(await preferences.UpdateAsync(User.RequireTenantId(), request, ct));
}
