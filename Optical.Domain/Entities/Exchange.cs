using Optical.Domain.Common;
using Optical.Domain.Enums;

namespace Optical.Domain.Entities;

/// <summary>
/// Goods swapped for other goods against an earlier sale. The shop does not refund money,
/// so an exchange never pays out: the replacement must be worth at least as much as what
/// comes back, and the customer settles any shortfall.
/// </summary>
public class Exchange : BaseEntity
{
    /// <summary>Human-facing number, e.g. EXC-20260905-00001. Unique across all exchanges.</summary>
    public string ExchangeNumber { get; set; } = string.Empty;

    public int SaleId { get; set; }

    public Sale Sale { get; set; } = null!;

    /// <summary>Why the goods were swapped, from a fixed list.</summary>
    public ExchangeReason Reason { get; set; }

    /// <summary>Free-text detail. Required when the reason is Other, optional otherwise.</summary>
    public string? Note { get; set; }

    /// <summary>Value of the goods handed back, at the price originally paid for them.</summary>
    public decimal ReturnedAmount { get; set; }

    /// <summary>Value of the goods handed out, at today's sale price.</summary>
    public decimal ReplacementAmount { get; set; }

    /// <summary>
    /// What the customer paid to cover the difference. Never negative: the replacement is
    /// required to be worth at least as much as what came back.
    /// </summary>
    public decimal AmountPaid { get; set; }

    /// <summary>How the difference was settled. Null when the swap was even and nothing was paid.</summary>
    public PaymentMethod? PaymentMethod { get; set; }

    /// <summary>Lines coming back off the original sale.</summary>
    public List<ExchangeReturnedItem> ReturnedItems { get; set; } = [];

    /// <summary>Lines going out in their place.</summary>
    public List<ExchangeReplacementItem> ReplacementItems { get; set; } = [];
}
