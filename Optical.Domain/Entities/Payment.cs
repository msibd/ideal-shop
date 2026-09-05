using Optical.Domain.Common;
using Optical.Domain.Enums;

namespace Optical.Domain.Entities;

/// <summary>One payment settles one sale. Split payments are out of scope for the MVP.</summary>
public class Payment : BaseEntity
{
    public int SaleId { get; set; }

    public Sale Sale { get; set; } = null!;

    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; }
}
