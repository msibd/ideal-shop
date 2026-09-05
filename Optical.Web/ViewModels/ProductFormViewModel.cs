using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Optical.Web.ViewModels;

public class ProductFormViewModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    [Display(Name = "SKU")]
    public string Sku { get; set; } = string.Empty;

    [StringLength(32)]
    [Display(Name = "Barcode")]
    public string? Barcode { get; set; }

    /// <summary>Ticked when the product has no manufacturer barcode and the shop needs one.</summary>
    [Display(Name = "Generate an internal barcode")]
    public bool GenerateBarcode { get; set; }

    [Required]
    [StringLength(150)]
    [Display(Name = "Product name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select a category.")]
    [Display(Name = "Category")]
    public int? CategoryId { get; set; }

    [Required(ErrorMessage = "Please select a brand.")]
    [Display(Name = "Brand")]
    public int? BrandId { get; set; }

    [Range(0, 99_999_999.99, ErrorMessage = "Purchase price cannot be negative.")]
    [Display(Name = "Purchase price")]
    public decimal PurchasePrice { get; set; }

    [Range(0, 99_999_999.99, ErrorMessage = "Sale price cannot be negative.")]
    [Display(Name = "Sale price")]
    public decimal SalePrice { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Reorder level cannot be negative.")]
    [Display(Name = "Reorder level")]
    public int ReorderLevel { get; set; }

    [Range(0, 100, ErrorMessage = "The discount must be between 0 and 100%.")]
    [Display(Name = "Discount %")]
    public decimal DiscountPercent { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Runs from")]
    public DateOnly? DiscountStartsOn { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Runs until")]
    public DateOnly? DiscountEndsOn { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public IEnumerable<SelectListItem> Categories { get; set; } = [];

    public IEnumerable<SelectListItem> Brands { get; set; } = [];
}
