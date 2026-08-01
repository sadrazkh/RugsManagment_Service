namespace RugsManagment.Application.DTOs.Auth;

/// <summary>بدنهٔ فرم ورود</summary>
public record LoginRequest(string Email, string Password);

/// <summary>کاربر بدون PasswordHash — امن برای JSON</summary>
public record UserDto(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    Guid? TenantId,
    string? TenantName);
