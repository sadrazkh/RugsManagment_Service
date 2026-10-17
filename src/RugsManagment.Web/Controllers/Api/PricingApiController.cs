using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.DTOs.Pricing;
using RugsManagment.Web.Auth;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>
/// پیش‌نمایش هزینه — محاسبهٔ بک‌اند بدون ذخیره، برای نمایش قبل از ثبت.
///
/// Business: خروجی‌اش مبلغ تومانی است و فقط ویرایشگر نرخ مرحله صدایش می‌زند، که برای انباردار پنهان است
/// (او مرحله را بدون مبلغ تکمیل می‌کند و نرخ طرف خدمات یا نرخ پیش‌فرض خودکار می‌نشیند).
/// </summary>
[Route("api/pricing")]
[Authorize(Policy = Policies.Business)]
public class PricingApiController(ICostCalculationService pricing) : ApiControllerBase
{
    [HttpPost("preview")]
    public IActionResult Preview([FromBody] PricingPreviewRequest request)
        => Ok(pricing.Preview(request));
}
