using System.ComponentModel.DataAnnotations;

namespace RugsManagment.Web.Models.Account;

/// <summary>حساب کاربری خودِ کاربر: نام نمایشی و تغییر رمز.</summary>
public sealed class ProfileViewModel
{
    [Required(ErrorMessage = "نام را وارد کنید.")]
    [Display(Name = "نام و نام خانوادگی")]
    public string FullName { get; set; } = string.Empty;

    /// <summary>ایمیل ورود — قابل تغییر نیست (شناسهٔ حساب است)</summary>
    public string Email { get; set; } = string.Empty;

    public string RoleLabel { get; set; } = string.Empty;
    public string? TenantName { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "رمز فعلی")]
    public string? CurrentPassword { get; set; }

    [DataType(DataType.Password)]
    [MinLength(8, ErrorMessage = "رمز جدید باید حداقل ۸ کاراکتر باشد.")]
    [Display(Name = "رمز جدید")]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "تکرار رمز با رمز جدید یکسان نیست.")]
    [Display(Name = "تکرار رمز جدید")]
    public string? ConfirmPassword { get; set; }

    public string? Error { get; set; }
}
