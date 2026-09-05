using Optical.Domain.Common;

namespace Optical.Domain.Entities;

public class SaleItem : BaseEntity
{
    public int SaleId { get; set; }

    public Sale Sale { get; set; } = null!;

    public int ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public int Quantity { get; set; }

    /// <summary>Sale price at the time of sale, kept even if the product price changes later.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// The product's discount percentage at the moment of sale, kept so a receipt can still
    /// say "5% off" long after the offer has ended or been changed.
    /// </summary>
    public decimal DiscountPercent { get; set; }

    /// <summary>
    /// Taken off this line by that percentage. The line therefore carries its own true cost,
    /// which is what a later exchange refund and the profit figures are worked out from.
    /// </summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>Quantity x UnitPrice, less DiscountAmount.</summary>
    public decimal LineTotal { get; set; }
}
