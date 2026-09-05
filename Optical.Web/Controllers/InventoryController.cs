using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Optical.Application.Common;
using Optical.Application.Features.Inventory;
using Optical.Domain.Exceptions;
using Optical.Web.ViewModels;

namespace Optical.Web.Controllers;

[Authorize(Policy = AppPermissions.Inventory)]
public class InventoryController(
    GetInventoryHandler getInventory,
    GetProductStockHandler getProductStock,
    AdjustStockHandler adjustStock) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        bool lowStockOnly = false,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        ViewData["Search"] = search;
        ViewData["LowStockOnly"] = lowStockOnly;

        return View(await getInventory.HandleAsync(search, lowStockOnly, page, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Adjust(int id, CancellationToken cancellationToken)
    {
        var stock = await getProductStock.HandleAsync(id, cancellationToken);

        if (stock is null)
        {
            return NotFound();
        }

        return View(new StockAdjustmentViewModel
        {
            ProductId = stock.ProductId,
            Sku = stock.Sku,
            ProductName = stock.Name,
            CurrentQuantity = stock.Quantity,
            NewQuantity = stock.Quantity
        });
    }

    [HttpPost]
    public async Task<IActionResult> Adjust(StockAdjustmentViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await adjustStock.HandleAsync(new AdjustStockCommand(model.ProductId, model.NewQuantity), cancellationToken);
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        TempData["Success"] = "Stock adjusted.";
        return RedirectToAction(nameof(Index));
    }
}
