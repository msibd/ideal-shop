using Optical.Domain.Common;

namespace Optical.Domain.Entities;

public class Purchase : BaseEntity
{
    public int SupplierId { get; set; }

    public Supplier Supplier { get; set; } = null!;

    public DateOnly PurchaseDate { get; set; }

    public string? InvoiceNumber { get; set; }

    /// <summary>Sum of the line totals. Always calculated on the server.</summary>
    public decimal TotalAmount { get; set; }

    public List<PurchaseItem> Items { get; set; } = [];
}
