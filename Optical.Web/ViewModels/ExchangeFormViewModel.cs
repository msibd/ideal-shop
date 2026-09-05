using System.ComponentModel.DataAnnotations;
using Optical.Application.Features.Exchanges;
using Optical.Domain.Enums;

namespace Optical.Web.ViewModels;

public class ExchangeReturnLineViewModel
{
    public int SaleItemId { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Quantity cannot be negative.")]
    public int Quantity { get; set; }
}

public class ExchangeReplacementLineViewModel
{
    public int? ProductId { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Quantity cannot be negative.")]
    public int Quantity { get; set; }
}

public class ExchangeFormViewModel
{
    public int SaleId { get; set; }

    /// <summary>Only needed when the replacement costs more than what came back.</summary>
    [Display(Name = "Difference paid by")]
    public PaymentMethod? PaymentMethod { get; set; }

    [Required(ErrorMessage = "Select why the goods are being exchanged.")]
    [Display(Name = "Reason")]
    public ExchangeReason? Reason { get; set; }

    [StringLength(300, ErrorMessage = "Keep the note under 300 characters.")]
    [Display(Name = "Note")]
    public string? Note { get; set; }

    public List<ExchangeReturnLineViewModel> Returned { get; set; } = [];

    public List<ExchangeReplacementLineViewModel> Replacements { get; set; } = [];

    /// <summary>
    /// Display only: the sale, what is still exchangeable on each line, and the products
    /// available to swap into. Reloaded from the database whenever the form is drawn, so a
    /// stale post can never widen the limits.
    /// </summary>
    public ExchangeableSale? Sale { get; set; }
}
