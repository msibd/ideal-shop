using Optical.Domain.Common;

namespace Optical.Domain.Entities;

public class Product : BaseEntity
{
    public string Sku { get; set; } = string.Empty;

    /// <summary>
    /// What the scanner reads: the manufacturer's barcode, or one the shop generated. Null
    /// rather than empty for a product without one, so the unique index still allows many
    /// of them — a database treats two nulls as different, but two empty strings as the same.
    /// </summary>
    public string? Barcode { get; set; }

    public string Name { get; set; } = string.Empty;

    public int CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    public int BrandId { get; set; }

    public Brand Brand { get; set; } = null!;

    public decimal PurchasePrice { get; set; }

    public decimal SalePrice { get; set; }

    /// <summary>Stock level at which the product should be re-ordered.</summary>
    public int ReorderLevel { get; set; }

    /// <summary>
    /// Percentage off this product while the offer is running, e.g. 5 for 5%. Zero means the
    /// product sells at full price. The till reads this; a cashier cannot type a discount.
    /// </summary>
    public decimal DiscountPercent { get; set; }

    /// <summary>First day the discount applies. Null when there is no offer.</summary>
    public DateOnly? DiscountStartsOn { get; set; }

    /// <summary>Last day the discount applies, included. Null when there is no offer.</summary>
    public DateOnly? DiscountEndsOn { get; set; }

    public bool IsActive { get; set; } = true;

    // Stock quantity deliberately lives on InventoryItem, not here.

    /// <summary>
    /// The percentage in force on a given day: the product's own, or zero once the offer has
    /// expired or before it starts. An offer with no dates is not running at all, so a
    /// half-filled discount can never quietly take money off a sale.
    /// </summary>
    public decimal DiscountPercentOn(DateOnly date) =>
        IsDiscountRunning(DiscountPercent, DiscountStartsOn, DiscountEndsOn, date)
            ? DiscountPercent
            : 0m;

    /// <summary>
    /// The same rule, usable where only the three stored values are to hand — the product
    /// list and the till both need it without loading whole entities.
    /// </summary>
    public static bool IsDiscountRunning(
        decimal percent,
        DateOnly? startsOn,
        DateOnly? endsOn,
        DateOnly date) =>
        percent > 0
        && startsOn is not null
        && endsOn is not null
        && date >= startsOn.Value
        && date <= endsOn.Value;
}
