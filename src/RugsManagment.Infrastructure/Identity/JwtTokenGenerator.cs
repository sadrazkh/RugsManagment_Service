using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RugsManagment.Application.Services;
using RugsManagment.Domain.Entities;
using RugsManagment.Infrastructure.Persistence;

namespace RugsManagment.Infrastructure.Identity;

/// <summary>تنظیمات JWT در appsettings بخش Jwt</summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";
    public string Secret { get; set; } = "CHANGE_ME_TO_A_LONG_SECRET_KEY_FOR_PRODUCTION_USE";
    public string Issuer { get; set; } = "RugsManagment";
    public string Audience { get; set; } = "RugsManagment.Client";
    public int ExpiryMinutes { get; set; } = 480;
}

/// <summary>
/// ساخت توکن JWT بعد از login — شامل شناسه کاربر، نقش، و tenant_id برای کارگاه.
/// </summary>
public sealed class JwtTokenGenerator(IOptions<JwtSettings> options) : IJwtTokenGenerator
{
    public (string Token, DateTimeOffset ExpiresAt) Generate(User user)
    {
        var settings = options.Value;
        var expires = DateTimeOffset.UtcNow.AddMinutes(settings.ExpiryMinutes);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        if (user.TenantId.HasValue)
            claims.Add(new Claim("tenant_id", user.TenantId.Value.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            settings.Issuer,
            settings.Audience,
            claims,
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}

/// <summary>بارگذاری ProcessStepType از دیتابیس برای موتور مسیر سفارشی</summary>
public sealed class ProcessStepTypeLookup(AppDbContext db) : IProcessStepTypeLookup
{
    public async Task<ProcessStepType> GetRequiredAsync(Guid id, CancellationToken cancellationToken = default)
        => await db.ProcessStepTypes.FindAsync([id], cancellationToken)
            ?? throw new KeyNotFoundException($"نوع مرحله {id} یافت نشد.");
}
