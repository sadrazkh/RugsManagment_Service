using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RugsManagment.Application.Common;
using RugsManagment.Application.DTOs.Import;
using RugsManagment.Application.Features.Import;
using RugsManagment.Domain.Common;
using RugsManagment.Web.Auth;
namespace RugsManagment.Web.Controllers.Api;

[Route("api/imports")]
[Authorize(Policy = Policies.Business)]
public sealed class ImportsApiController(ImportService imports) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await imports.ListAsync(User.RequireTenantId(), ct));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await imports.GetAsync(User.RequireTenantId(), id, ct));
    [HttpPost]
    [RequestSizeLimit(10_485_760)]
    [EnableRateLimiting("heavy")]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        if (file.Length > XlsxSignature.MaxFileBytes) throw new DomainValidationException(ImportMessages.Text("E_FILE_TOO_LARGE"), "file");
        await using var input = file.OpenReadStream();
        return Ok(await imports.UploadAsync(User.RequireTenantId(), file.FileName, input, ct));
    }
    [HttpPost("{id:guid}/preview")]
    [EnableRateLimiting("heavy")]
    public async Task<IActionResult> Preview(Guid id, [FromBody] PreviewImportRequest request, CancellationToken ct)
        => Ok(await imports.PreviewAsync(User.RequireTenantId(), id, request, ct));
    [HttpPost("{id:guid}/commit")]
    [EnableRateLimiting("heavy")]
    public async Task<IActionResult> Commit(Guid id, [FromBody] CommitImportRequest request, CancellationToken ct)
        => Ok(await imports.CommitAsync(User.RequireTenantId(), id, request, ct));
    [HttpPost("{id:guid}/undo")]
    public async Task<IActionResult> Undo(Guid id, CancellationToken ct) => Ok(await imports.UndoAsync(User.RequireTenantId(), id, ct));
}
