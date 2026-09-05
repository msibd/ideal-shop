using System.ComponentModel.DataAnnotations;

namespace Optical.Web.ViewModels;

public class ProfileViewModel
{
    [Required]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Display name must be between 2 and 100 characters.")]
    [Display(Name = "Display name")]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Shown for reference only — the sign-in username cannot be changed here.</summary>
    public string UserName { get; set; } = string.Empty;

    public string? Email { get; set; }
}
