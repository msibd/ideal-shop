using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Optical.Application.Common;

namespace Optical.Web.ViewModels;

public class CreateUserViewModel
{
    [Required(ErrorMessage = "A username is required.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be 3 to 50 characters.")]
    [RegularExpression("^[A-Za-z0-9._-]+$",
        ErrorMessage = "Username can use letters, digits, dot, dash and underscore only.")]
    [Display(Name = "Username")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "A full name is required.")]
    [StringLength(100, ErrorMessage = "Keep the name under 100 characters.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150)]
    public string? Email { get; set; }

    [Required(ErrorMessage = "A password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Select a role.")]
    public string Role { get; set; } = AppRoles.Cashier;

    public IEnumerable<SelectListItem> Roles => AppRoles.Assignable
        .Select(role => new SelectListItem(role, role, role == Role));
}

public class EditUserViewModel
{
    public string Id { get; set; } = string.Empty;

    /// <summary>Display only: the username is the sign-in name and never changes.</summary>
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "A full name is required.")]
    [StringLength(100, ErrorMessage = "Keep the name under 100 characters.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150)]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Select a role.")]
    public string Role { get; set; } = AppRoles.Cashier;

    [Display(Name = "Account is active")]
    public bool IsActive { get; set; } = true;

    /// <summary>True when the row being edited is the signed-in administrator's own account.</summary>
    public bool IsSelf { get; set; }

    public IEnumerable<SelectListItem> Roles => AppRoles.Assignable
        .Select(role => new SelectListItem(role, role, role == Role));
}

public class ResetPasswordViewModel
{
    public string Id { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "A password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "The two passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
