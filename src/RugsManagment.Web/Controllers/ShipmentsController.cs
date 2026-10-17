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
/// صفحه‌های محموله (Floor): فهرست (<c>/Shipments</c>) و کارگاه یک محموله (<c>/Shipments/{id}?tab=…</c>) — قاب فاز ۴a.
///
/// چرا مسیرها attribute‌اند: نشانی کارگاه شناسهٔ محموله را بی نام اکشن دارد (<c>/Shipments/{id}</c>)، همان که خانه با
/// <c>HomeRoutes.ShipmentTab</c> و «محمولهٔ تازه» (<c>data-post-redirect</c>) می‌سازد. صفحهٔ اسکن گوشی
/// (<c>/Shipments/{id}/Pack</c>) کنترلر جدای فاز ۴b است.
///
/// هر چیزِ این صفحه‌ها برای انباردار هم باز است: محموله هیچ مبلغ تومانی ندارد (دلار گمرکی پول داخلی نیست، A.5). دکمه‌های
/// Business («محمولهٔ تازه»، «قطعی کن») فقط برای Business نشان داده می‌شوند و خودِ APIها هم Business می‌خواهند. شناسهٔ
/// محمولهٔ کارگاه دیگر درست مثل شناسهٔ ناموجود ۴۰۴ است.
/// </summary>
[Authorize(Policy = Policies.Floor)]
[Route("Shipments")]
public sealed class ShipmentsController(IShipmentService shipments) : Controller
{
    /// <summary>فهرست محموله‌ها؛ <c>?status=open|finalized</c> (یا عدد) فقط همان وضعیت.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] string? status, CancellationToken ct)
    {
        var filter = ParseStatus(status);
        var list = await shipments.ListAsync(User.RequireTenantId(), filter, ct);
        var terms = await AppShell.TermsAsync(HttpContext);

        return View(new ShipmentsIndexModel(list, filter, Policies.IsBusiness(User), terms));
    }

    /// <summary>کارگاه یک محموله: سربرگ و زبانه‌ها (<c>?tab=bales|costs|summary|output</c>).</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Workspace(Guid id, [FromQuery] string? tab, CancellationToken ct)
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

        var terms = await AppShell.TermsAsync(HttpContext);
        return View(new ShipmentWorkspaceModel(shipment, tab, Policies.IsBusiness(User), terms));
    }

    /// <summary>«open» / «finalized» / عدد enum؛ هر چیز دیگر یعنی «همه».</summary>
    private static ShipmentStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return null;

        return status.Trim().ToLowerInvariant() switch
        {
            "open" or "0" => ShipmentStatus.Open,
            "finalized" or "1" => ShipmentStatus.Finalized,
            _ => null
        };
    }
}

/// <summary>مدل فهرست محموله‌ها.</summary>
/// <param name="Shipments">محموله‌ها، تازه‌ترین شماره اول</param>
/// <param name="Status">فیلتر وضعیت؛ null یعنی همه</param>
/// <param name="CanCreate">بیننده Business است و «محمولهٔ تازه» می‌بیند</param>
/// <param name="Terms">واژه‌های نگله/عدل کارگاه</param>
public sealed record ShipmentsIndexModel(
    IReadOnlyList<ShipmentListItemDto> Shipments,
    ShipmentStatus? Status,
    bool CanCreate,
    BaleTermWords Terms);

/// <summary>مدل کارگاه یک محموله.</summary>
/// <param name="Shipment">محموله با نگله‌ها، جمع‌ها و آمادگی</param>
/// <param name="Tab">زبانهٔ خواسته‌شده (<c>?tab=</c>)؛ ناشناخته یعنی اولین زبانه</param>
/// <param name="CanBusiness">بیننده Business است</param>
/// <param name="Terms">واژه‌های نگله/عدل کارگاه</param>
public sealed record ShipmentWorkspaceModel(
    ShipmentDto Shipment,
    string? Tab,
    bool CanBusiness,
    BaleTermWords Terms);
