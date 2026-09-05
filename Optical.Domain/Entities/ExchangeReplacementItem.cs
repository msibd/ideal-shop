using Optical.Domain.Common;

namespace Optical.Domain.Entities;

/// <summary>
/// One line going out in place of returned goods. Priced at today's sale price, because
/// this is effectively a fresh sale of that product.
/// </summary>
public class ExchangeReplacementItem : BaseEntity
{
    public int ExchangeId { get; set; }

    public Exchange Exchange { get; set; } = null!;

    public int ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public int Quantity { get; set; }

    /// <summary>The product's sale price at the moment of the exchange.</summary>
    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }
}
