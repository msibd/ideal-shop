using System.ComponentModel.DataAnnotations;

namespace Optical.Web.ViewModels;

public class StockAdjustmentViewModel
{
    public int ProductId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    [Display(Name = "Current stock")]
    public int CurrentQuantity { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Stock cannot be negative.")]
    [Display(Name = "Counted stock")]
    public int NewQuantity { get; set; }
}
