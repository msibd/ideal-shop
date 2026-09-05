using Optical.Domain.Common;
using Optical.Domain.Enums;

namespace Optical.Domain.Entities;

public class Sale : BaseEntity
{
    /// <summary>Human-facing invoice number, e.g. FF-20260905-00004. Unique across all sales.</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>Optional: walk-in customers are not recorded.</summary>
    public int? CustomerId { get; set; }

    public Customer? Customer { get; set; }

    /// <summary>Sum of the lines before any discount. Always calculated on the server.</summary>
    public decimal SubTotal { get; set; }

    /// <summary>
    /// Everything taken off this sale: the line discounts plus whatever was knocked off the
    /// bill as a whole. Held here so a receipt can show one figure without re-adding the lines.
    /// </summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>What the customer actually pays: SubTotal minus DiscountAmount.</summary>
    public decimal TotalAmount { get; set; }

    public SaleStatus Status { get; set; } = SaleStatus.Completed;

    public List<SaleItem> Items { get; set; } = [];

    public Payment? Payment { get; set; }
}
