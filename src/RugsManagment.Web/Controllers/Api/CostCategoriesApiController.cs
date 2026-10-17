using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.DTOs.Costs;
using RugsManagment.Application.Features.Costs;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>
/// API نوع‌های هزینه (spec I.2، B.7):
/// <list type="bullet">
/// <item><c>GET</c> برای هر کاربر کارگاه (Floor): انباردار فقط نوع‌های گروه خرید را می‌گیرد (سرستون‌های جدول ورود)، بازرگان و
/// تنظیم‌کننده همه را. <c>?includeInactive=true</c> نوع‌های غیرفعال را هم می‌دهد (پنل ثبت، برای ثبتی که نوعش بعداً غیرفعال
/// شد؛ صفحهٔ تنظیم) — فقط برای Business.</item>
/// <item><c>POST /quick</c> «+ نوع تازه» از پنل ثبت هزینه (Business) با پیش‌فرض‌های گروه.</item>
/// <item>ساختن، ویرایش و حذف کامل فقط تنظیم‌کننده (Configure): گروهِ نوعِ به کار رفته عوض نمی‌شود (۴۰۹) و حذفِ نوعِ به کار رفته
/// آن را غیرفعال می‌کند (<c>{deleted, deactivated}</c>).</item>
/// </list>
/// نسخهٔ کهنه ۴۰۹؛ شناسهٔ کارگاه دیگر ۴۰۴.
/// </summary>
[Route("api/cost-categories")]
public sealed class CostCategoriesApiController(ICostCategoryService categories) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeInactive, CancellationToken ct)
    {
        var business = Policies.IsBusiness(User);
        return Ok(await categories.ListAsync(User.RequireTenantId(), purchaseOnly: !business, includeInactive && business, ct));
    }

    /// <summary>«+ نوع تازه» (H.2): <c>{name, group}</c>؛ نام تکراریِ همان گروه همان نوع را برمی‌گرداند.</summary>
    [HttpPost("quick")]
    [Authorize(Policy = Policies.Business)]
    public async Task<IActionResult> Quick([FromBody] QuickCostCategoryRequest request, CancellationToken ct)
        => Ok(await categories.QuickCreateAsync(User.RequireTenantId(), request, ct));

    [HttpPost]
    [Authorize(Policy = Policies.Configure)]
    public async Task<IActionResult> Create([FromBody] SaveCostCategoryRequest request, CancellationToken ct)
        => Ok(await categories.CreateAsync(User.RequireTenantId(), request, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Configure)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveCostCategoryRequest request, CancellationToken ct)
        => Ok(await categories.UpdateAsync(User.RequireTenantId(), id, request, ct));

    /// <summary>حذف؛ نوعِ به کار رفته فقط غیرفعال می‌شود (B.13). <c>?version=</c> اختیاری.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.Configure)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] uint? version, CancellationToken ct)
        => Ok(await categories.DeleteAsync(User.RequireTenantId(), id, version, ct));
}
