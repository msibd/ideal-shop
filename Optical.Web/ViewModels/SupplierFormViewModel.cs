using System.ComponentModel.DataAnnotations;

namespace Optical.Web.ViewModels;

public class SupplierFormViewModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(150)]
    [Display(Name = "Supplier name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(30)]
    [Phone]
    [Display(Name = "Phone")]
    public string? Phone { get; set; }

    [StringLength(150)]
    [EmailAddress]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [StringLength(250)]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}
