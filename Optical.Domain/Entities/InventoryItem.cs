using Optical.Domain.Common;

namespace Optical.Domain.Entities;

/// <summary>Current stock for one product. One row per product.</summary>
public class InventoryItem : BaseEntity
{
    public int ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public int Quantity { get; set; }
}
