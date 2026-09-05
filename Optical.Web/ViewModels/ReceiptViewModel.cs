using Optical.Application.Features.Sales;
using Optical.Web.Printing;

namespace Optical.Web.ViewModels;

/// <summary>The sale, plus the shop identity printed at the top of the paper receipt.</summary>
public sealed record ReceiptViewModel(
    SaleDetails Sale,
    string BusinessName,
    string Phone,
    string Address,
    ReceiptFormat Format)
{
    public bool IsA4 => Format == ReceiptFormat.A4;
}
