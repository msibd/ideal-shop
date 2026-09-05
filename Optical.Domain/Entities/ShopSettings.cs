using Optical.Domain.Common;

namespace Optical.Domain.Entities;

/// <summary>
/// Business details of the shop. A single shop keeps exactly one row,
/// enforced by a check constraint on the primary key.
/// </summary>
public class ShopSettings : BaseEntity
{
    public const int SingleRowId = 1;

    public string BusinessName { get; set; } = string.Empty;

    /// <summary>Printed on receipts so a customer can ring the shop back.</summary>
    public string Phone { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;
}
