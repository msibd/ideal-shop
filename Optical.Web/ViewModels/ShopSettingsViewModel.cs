using System.ComponentModel.DataAnnotations;

namespace Optical.Web.ViewModels;

public class ShopSettingsViewModel
{
    [Required]
    [StringLength(150)]
    [Display(Name = "Business name")]
    public string BusinessName { get; set; } = string.Empty;

    [StringLength(30)]
    [Display(Name = "Phone number")]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [StringLength(300)]
    [Display(Name = "Address")]
    public string Address { get; set; } = string.Empty;
}
