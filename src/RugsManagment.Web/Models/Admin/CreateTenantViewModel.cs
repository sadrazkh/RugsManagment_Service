using System.ComponentModel.DataAnnotations;

namespace RugsManagment.Web.Models.Admin;

/// <summary>فرم ساخت کارگاه (فروشنده) جدید + اولین مدیر آن.</summary>
public sealed class CreateTenantViewModel
{
    [Required(ErrorMessage = "نام کارگاه لازم است.")]
    [Display(Name = "نام کارگاه")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "شناسه (slug) لازم است.")]
    [RegularExpression("^[a-z0-9-]+$", ErrorMessage = "شناسه فقط حروف کوچک انگلیسی، عدد و خط تیره.")]
    [Display(Name = "شناسه یکتا")]
    public string Slug { get; set; } = string.Empty;

    [Display(Name = "تلفن تماس")]
    public string? ContactPhone { get; set; }

    [EmailAddress(ErrorMessage = "ایمیل تماس معتبر نیست.")]
    [Display(Name = "ایمیل تماس")]
    public string? ContactEmail { get; set; }

    [Required(ErrorMessage = "نام مدیر لازم است.")]
    [Display(Name = "نام مدیر کارگاه")]
    public string AdminFullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "ایمیل مدیر لازم است.")]
    [EmailAddress(ErrorMessage = "ایمیل معتبر نیست.")]
    [Display(Name = "ایمیل ورود مدیر")]
    public string AdminEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "رمز عبور لازم است.")]
    [MinLength(6, ErrorMessage = "رمز حداقل ۶ کاراکتر.")]
    [DataType(DataType.Password)]
    [Display(Name = "رمز عبور مدیر")]
    public string AdminPassword { get; set; } = string.Empty;

    public string? Error { get; set; }
}
