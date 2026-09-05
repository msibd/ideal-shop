using Optical.Domain.Common;

namespace Optical.Domain.Entities;

public class PurchaseItem : BaseEntity
{
    public int PurchaseId { get; set; }

    public Purchase Purchase { get; set; } = null!;

    public int ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public int Quantity { get; set; }

    /// <summary>Unit cost at the time of purchase, kept even if the product price changes later.</summary>
    public decimal PurchasePrice { get; set; }

    public decimal LineTotal { get; set; }
}
