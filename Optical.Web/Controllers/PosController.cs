using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Optical.Application.Common;
using Optical.Application.Features.POS;
using Optical.Application.Features.Products;
using Optical.Domain.Exceptions;
using Optical.Web.ViewModels;

namespace Optical.Web.Controllers;

[Authorize(Policy = AppPermissions.Pos)]
public class PosController(
    SearchPosProductsHandler searchProducts,
    SearchPosCustomersHandler searchCustomers,
    GetLastSaleHandler getLastSale,
    GetProductFormOptionsHandler getProductOptions,
    CheckoutHandler checkout) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new PosCheckoutViewModel();
        await FillOptionsAsync(model, cancellationToken);

        return View(model);
    }

    /// <summary>Product search for the POS screen. Read-only, so it is safe as a GET.</summary>
    [HttpGet]
    public async Task<IActionResult> Search(
        string? term,
        int? categoryId,
        PosProductSort sort = PosProductSort.NameAscending,
        CancellationToken cancellationToken = default) =>
        Json(await searchProducts.SearchAsync(term, categoryId, sort, cancellationToken));

    /// <summary>
    /// One scanned barcode. Read-only, so it is safe as a GET. Returns the product when the
    /// code matches exactly, and nothing otherwise, so the till can say "unknown barcode"
    /// rather than quietly adding whatever happened to look similar.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Scan(string? barcode, CancellationToken cancellationToken)
    {
        var product = await searchProducts.FindByBarcodeAsync(barcode, cancellationToken);

        return Json(new { found = product is not null, product });
    }

    /// <summary>Customer lookup by name or mobile number. Read-only, so it is safe as a GET.</summary>
    [HttpGet]
    public async Task<IActionResult> SearchCustomers(string? term, CancellationToken cancellationToken) =>
        Json(await searchCustomers.SearchAsync(term, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Checkout(PosCheckoutViewModel model, CancellationToken cancellationToken)
    {
        if (model.Items.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "The cart is empty.");
        }

        if (!ModelState.IsValid)
        {
            return await RejectAsync(model, cancellationToken);
        }

        try
        {
            var sale = await checkout.HandleAsync(
                new CheckoutCommand(
                    model.CustomerId,
                    model.PaymentMethod!.Value,
                    model.Items.Select(i => new CheckoutItemCommand(i.ProductId, i.Quantity)).ToList()),
                cancellationToken);

            var message = $"Invoice {sale.InvoiceNumber} completed and stock updated.";

            if (IsAjaxRequest)
            {
                return Json(new
                {
                    success = true,
                    saleId = sale.SaleId,
                    invoiceNumber = sale.InvoiceNumber,
                    discount = sale.DiscountAmount.ToString("N2"),
                    total = sale.TotalAmount.ToString("N2"),
                    at = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                    message
                });
            }

            TempData["Success"] = message;
            return RedirectToAction(nameof(Index));
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await RejectAsync(model, cancellationToken);
        }
    }

    /// <summary>
    /// The till posts over AJAX so a rejected sale never loses the cart, but the same action
    /// still answers an ordinary form post so checkout keeps working without JavaScript.
    /// </summary>
    private bool IsAjaxRequest => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

    private async Task<IActionResult> RejectAsync(
        PosCheckoutViewModel model,
        CancellationToken cancellationToken)
    {
        if (IsAjaxRequest)
        {
            var errors = ModelState.Values
                .SelectMany(state => state.Errors)
                .Select(error => error.ErrorMessage)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .ToArray();

            return Json(new { success = false, errors });
        }

        await FillOptionsAsync(model, cancellationToken);
        return View(nameof(Index), model);
    }

    private async Task FillOptionsAsync(PosCheckoutViewModel model, CancellationToken cancellationToken)
    {
        model.SelectedCustomer = await searchCustomers.GetByIdAsync(model.CustomerId, cancellationToken);

        model.LastSale = await getLastSale.HandleAsync(cancellationToken);
        model.Categories = (await getProductOptions.HandleAsync(cancellationToken: cancellationToken)).Categories;

        // The rejected cart is redrawn from database prices, never from what was posted.
        model.CartProducts = await searchProducts.GetByIdsAsync(
            model.Items.Select(i => i.ProductId).ToList(),
            cancellationToken);
    }
}
