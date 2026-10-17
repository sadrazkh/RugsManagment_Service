using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Shipments;
using RugsManagment.Application.Features.Shipments;
using RugsManagment.Domain.Enums;
using RugsManagment.Web.Auth;
using RugsManagment.Web.Navigation;

namespace RugsManagment.Web.Controllers;

/// <summary>
/// «حالت اسکن (گوشی)» (<c>/Shipments/{id}/Pack</c>، Floor، spec G.5): انباردار با گوشی کنار نگله می‌ایستد، شماره را می‌زند یا
/// برچسب را اسکن می‌کند و فرش به «نگلهٔ جاری» می‌رود. همهٔ کار در جزیرهٔ <c>pack-mode</c> است؛ این کنترلر فقط محموله را
/// می‌سنجد و props را می‌دهد.
///
/// چرا کنترلر جدا و نه اکشنی در <see cref="ShipmentsController"/>: آن کنترلر قاب فاز ۴a است و فازهای بعد ویرایشش نمی‌کنند
/// (K.1). محمولهٔ کارگاه دیگر درست مثل محمولهٔ ناموجود ۴۰۴ است. <c>?baleId=</c> اختیاری نگلهٔ آغازین است؛ نگله‌ای که مال این
/// محموله نیست نادیده گرفته می‌شود (نگلهٔ جاری همان قاعدهٔ C.2 را می‌گیرد).
/// </summary>
[Authorize(Policy = Policies.Floor)]
[Route("Shipments/{id:guid}/Pack")]
public sealed class ShipmentPackController(IShipmentService shipments) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(Guid id, [FromQuery] Guid? baleId, CancellationToken ct)
    {
        ShipmentDto shipment;
        try
        {
            shipment = await shipments.GetAsync(User.RequireTenantId(), id, ct);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var bale = baleId is { } wanted && shipment.Bales.Any(b => b.Id == wanted) ? wanted : (Guid?)null;
        var terms = await AppShell.TermsAsync(HttpContext);
        return View("~/Views/Shipments/Pack.cshtml",
            new ShipmentPackModel(shipment, bale, shipment.Status == ShipmentStatus.Finalized, Policies.IsBusiness(User), terms));
    }
}

/// <summary>مدل صفحهٔ «حالت اسکن».</summary>
/// <param name="Shipment">محموله با خلاصهٔ نگله‌ها و جمع‌ها</param>
/// <param name="BaleId">نگلهٔ آغازین (از <c>?baleId=</c>)؛ null یعنی «نگلهٔ جاری» همیشگی کاربر</param>
/// <param name="Locked">محموله قطعی است: فقط دیدن (C.8)</param>
/// <param name="CanBusiness">بیننده Business است (متن قفل همان H.3 هر نقش)</param>
/// <param name="Terms">واژه‌های نگله/عدل کارگاه</param>
public sealed record ShipmentPackModel(
    ShipmentDto Shipment,
    Guid? BaleId,
    bool Locked,
    bool CanBusiness,
    BaleTermWords Terms);
