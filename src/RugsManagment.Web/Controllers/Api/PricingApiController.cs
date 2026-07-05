using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.DTOs.Pricing;

namespace RugsManagment.Web.Controllers.Api;

/// <summary>پیش‌نمایش هزینه — محاسبهٔ بک‌اند بدون ذخیره، برای نمایش قبل از ثبت.</summary>
[Route("api/pricing")]
public class PricingApiController(ICostCalculationService pricing) : ApiControllerBase
{
    [HttpPost("preview")]
    public IActionResult Preview([FromBody] PricingPreviewRequest request)
        => Ok(pricing.Preview(request));
}
