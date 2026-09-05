using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Optical.Application.Common;
using Optical.Application.Features.Purchases;
using Optical.Domain.Exceptions;
using Optical.Web.ViewModels;

namespace Optical.Web.Controllers;

[Authorize(Policy = AppPermissions.Purchasing)]
public class PurchasesController(
    GetPurchasesHandler getPurchases,
    GetPurchaseByIdHandler getPurchaseById,
    GetPurchaseFormOptionsHandler getFormOptions,
    CreatePurchaseHandler createPurchase) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewData["Search"] = search;
        return View(await getPurchases.HandleAsync(search, page, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var purchase = await getPurchaseById.HandleAsync(id, cancellationToken);

        return purchase is null ? NotFound() : View(purchase);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new PurchaseFormViewModel { Items = [new PurchaseLineViewModel()] };
        await FillOptionsAsync(model, cancellationToken);

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(PurchaseFormViewModel model, CancellationToken cancellationToken)
    {
        if (model.Items.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Add at least one product to the purchase.");
        }

        if (!ModelState.IsValid)
        {
            await FillOptionsAsync(model, cancellationToken);
            return View(model);
        }

        try
        {
            var id = await createPurchase.HandleAsync(
                new CreatePurchaseCommand(
                    model.SupplierId!.Value,
                    model.PurchaseDate,
                    model.InvoiceNumber,
                    model.Items
                        .Select(i => new CreatePurchaseItemCommand(i.ProductId!.Value, i.Quantity, i.PurchasePrice))
                        .ToList()),
                cancellationToken);

            TempData["Success"] = "Purchase recorded and stock updated.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await FillOptionsAsync(model, cancellationToken);
            return View(model);
        }
    }

    private async Task FillOptionsAsync(PurchaseFormViewModel model, CancellationToken cancellationToken)
    {
        var options = await getFormOptions.HandleAsync(cancellationToken);

        model.Suppliers = options.Suppliers.Select(s => new SelectListItem(s.Name, s.Id.ToString()));
        model.Products = options.Products;
    }
}
