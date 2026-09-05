using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Optical.Application.Common;
using Optical.Application.Features.Sales;
using Optical.Application.Features.Settings;
using Optical.Web.Printing;
using Optical.Web.ViewModels;

namespace Optical.Web.Controllers;

/// <summary>Read-only history of what the till has rung up, plus the printable receipt.</summary>
[Authorize(Policy = AppPermissions.Sales)]
public class SalesController(
    GetSalesHandler getSales,
    GetSaleByIdHandler getSaleById,
    GetShopSettingsHandler getShopSettings) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        DateOnly? from,
        DateOnly? to,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        ViewData["Search"] = search;
        ViewData["From"] = from;
        ViewData["To"] = to;

        return View(await getSales.HandleAsync(search, from, to, page, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var sale = await getSaleById.HandleAsync(id, cancellationToken);

        return sale is null ? NotFound() : View(sale);
    }

    [HttpGet]
    /// <summary>
    /// The printable receipt. Defaults to the till roll, because that is what the counter
    /// prints all day; A4 is asked for by name when a customer wants a filed copy.
    /// </summary>
    public async Task<IActionResult> Receipt(
        int id,
        ReceiptFormat format = ReceiptFormat.Thermal,
        CancellationToken cancellationToken = default)
    {
        var sale = await getSaleById.HandleAsync(id, cancellationToken);

        if (sale is null)
        {
            return NotFound();
        }

        var shop = await getShopSettings.HandleAsync(cancellationToken);

        return View(new ReceiptViewModel(
            sale,
            shop?.BusinessName ?? "Optical Shop",
            shop?.Phone ?? string.Empty,
            shop?.Address ?? string.Empty,
            Enum.IsDefined(format) ? format : ReceiptFormat.Thermal));
    }
}
