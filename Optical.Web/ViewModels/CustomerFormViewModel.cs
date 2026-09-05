using System.ComponentModel.DataAnnotations;

namespace Optical.Web.ViewModels;

public class CustomerFormViewModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(150)]
    [Display(Name = "Customer name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(30)]
    [Phone]
    [Display(Name = "Phone")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(150)]
    [EmailAddress]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [StringLength(250)]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    [StringLength(500)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}
