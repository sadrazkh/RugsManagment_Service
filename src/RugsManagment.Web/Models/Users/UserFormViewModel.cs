using System.ComponentModel.DataAnnotations;
using RugsManagment.Domain.Enums;

namespace RugsManagment.Web.Models.Users;

/// <summary>فرم ساخت/ویرایش کاربر کارگاه.</summary>
public sealed class UserFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "نام لازم است.")]
    [Display(Name = "نام و نام خانوادگی")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "ایمیل لازم است.")]
    [EmailAddress(ErrorMessage = "ایمیل معتبر نیست.")]
    [Display(Name = "ایمیل ورود")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "نقش")]
    public UserRole Role { get; set; } = UserRole.Operator;

    [Display(Name = "فعال")]
    public bool IsActive { get; set; } = true;

    /// <summary>هنگام ساخت الزامی؛ هنگام ویرایش خالی = بدون تغییر.</summary>
    [DataType(DataType.Password)]
    [Display(Name = "رمز عبور")]
    public string? Password { get; set; }

    public bool IsEdit => Id.HasValue;
    public string? Error { get; set; }
}
