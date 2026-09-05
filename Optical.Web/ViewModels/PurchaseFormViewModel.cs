using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Optical.Application.Features.Purchases;

namespace Optical.Web.ViewModels;

public class PurchaseFormViewModel
{
    [Required(ErrorMessage = "Please select a supplier.")]
    [Display(Name = "Supplier")]
    public int? SupplierId { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Purchase date")]
    public DateOnly PurchaseDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [StringLength(50)]
    [Display(Name = "Invoice number")]
    public string? InvoiceNumber { get; set; }

    public List<PurchaseLineViewModel> Items { get; set; } = [];

    public IEnumerable<SelectListItem> Suppliers { get; set; } = [];

    /// <summary>Used to build the product dropdown and to prefill the default purchase price.</summary>
    public IReadOnlyList<PurchaseProductOption> Products { get; set; } = [];
}

public class PurchaseLineViewModel
{
    [Required(ErrorMessage = "Select a product.")]
    [Display(Name = "Product")]
    public int? ProductId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
    [Display(Name = "Quantity")]
    public int Quantity { get; set; } = 1;

    [Range(0, 99_999_999.99, ErrorMessage = "Purchase price cannot be negative.")]
    [Display(Name = "Unit cost")]
    public decimal PurchasePrice { get; set; }
}
