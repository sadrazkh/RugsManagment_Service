using RugsManagment.Domain.Enums;

namespace RugsManagment.Application.DTOs.Users;

/// <summary>ساخت کاربر جدید در یک کارگاه (توسط مدیر همان کارگاه).</summary>
public record CreateUserRequest(string FullName, string Email, string Password, UserRole Role);

/// <summary>ویرایش کاربر کارگاه — رمز اختیاری (خالی یعنی بدون تغییر).</summary>
public record UpdateUserRequest(string FullName, UserRole Role, bool IsActive, string? NewPassword);
