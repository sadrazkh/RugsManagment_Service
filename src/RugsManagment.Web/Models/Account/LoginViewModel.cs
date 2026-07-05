using System.ComponentModel.DataAnnotations;

namespace RugsManagment.Web.Models.Account;

/// <summary>مدل فرم ورود.</summary>
public sealed class LoginViewModel
{
    [Required(ErrorMessage = "ایمیل را وارد کنید.")]
    [EmailAddress(ErrorMessage = "ایمیل معتبر نیست.")]
    [Display(Name = "ایمیل")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "رمز عبور را وارد کنید.")]
    [DataType(DataType.Password)]
    [Display(Name = "رمز عبور")]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; } = true;

    public string? ReturnUrl { get; set; }

    /// <summary>پیام خطای ورود (اعتبارسنجی سرور).</summary>
    public string? Error { get; set; }
}
