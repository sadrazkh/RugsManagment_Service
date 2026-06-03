using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RugsManagment.Api.Extensions;
using RugsManagment.Application.Abstractions;
using RugsManagment.Application.Abstractions.Services;
using RugsManagment.Application.DTOs.Auth;

namespace RugsManagment.Api.Controllers;

/// <summary>ورود و دریافت پروفایل — بدون نیاز به توکن برای login</summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService auth, IUnitOfWork unitOfWork) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, ct);
        await unitOfWork.SaveChangesAsync(ct); // ذخیره LastLoginAt
        return Ok(result);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<AuthResponse>> Me(CancellationToken ct)
    {
        var result = await auth.RefreshAsync(User.GetUserId(), ct);
        return Ok(result);
    }
}
