using System.ComponentModel.DataAnnotations;
using Optical.Application.Features.POS;
using Optical.Application.Features.Products;
using Optical.Domain.Enums;

namespace Optical.Web.ViewModels;

/// <summary>
/// What the POS screen posts. Only the product and the quantity travel; the price, the
/// discount and the total are read from the database at checkout, so nothing here can
/// influence what is charged.
/// </summary>
public class PosCheckoutViewModel
{
    [Display(Name = "Customer")]
    public int? CustomerId { get; set; }

    [Required(ErrorMessage = "Select a payment method.")]
    [Display(Name = "Payment method")]
    public PaymentMethod? PaymentMethod { get; set; }

    public List<PosCartLineViewModel> Items { get; set; } = [];

    /// <summary>The chosen customer, re-read from the database for display only.</summary>
    public PosCustomer? SelectedCustomer { get; set; }

    /// <summary>Server-side product data used to redraw the cart after a rejected checkout.</summary>
    public IReadOnlyList<PosProduct> CartProducts { get; set; } = [];

    /// <summary>The sale that rang up just before this one, for display only.</summary>
    public LastSale? LastSale { get; set; }

    /// <summary>Active categories for the product filter.</summary>
    public IReadOnlyList<SelectOption> Categories { get; set; } = [];
}

public class PosCartLineViewModel
{
    [Required]
    public int ProductId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
    public int Quantity { get; set; } = 1;
}
