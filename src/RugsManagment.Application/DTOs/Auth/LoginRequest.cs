namespace RugsManagment.Application.DTOs.Auth;

/// <summary>بدنهٔ درخواست POST /api/auth/login</summary>
public record LoginRequest(string Email, string Password);

/// <summary>پاسخ موفق login — توکن + اطلاعات کاربر برای ذخیره در فرانت</summary>
public record AuthResponse(
    string Token,
    DateTimeOffset ExpiresAt,
    UserDto User);

/// <summary>کاربر بدون PasswordHash — امن برای JSON</summary>
public record UserDto(
    Guid Id,
    string Email,
    string FullName,
    string Role,
    Guid? TenantId,
    string? TenantName);
