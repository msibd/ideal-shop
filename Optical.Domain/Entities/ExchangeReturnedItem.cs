using Optical.Domain.Common;

namespace Optical.Domain.Entities;

/// <summary>
/// One line coming back. It points at the sale line it came from, which is what makes
/// "you can never hand back more than was sold" enforceable across repeated exchanges.
/// </summary>
public class ExchangeReturnedItem : BaseEntity
{
    public int ExchangeId { get; set; }

    public Exchange Exchange { get; set; } = null!;

    public int SaleItemId { get; set; }

    public SaleItem SaleItem { get; set; } = null!;

    public int ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public int Quantity { get; set; }

    /// <summary>Copied from the sale line, so a later price change never alters its value.</summary>
    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }
}
