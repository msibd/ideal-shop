using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Optical.Application.Common;
using Optical.Application.Features.Products;
using Optical.Domain.Exceptions;
using Optical.Web.ViewModels;

namespace Optical.Web.Controllers;

[Authorize(Policy = AppPermissions.Catalog)]
public class ProductsController(
    GetProductsHandler getProducts,
    GetProductByIdHandler getProductById,
    GetProductFormOptionsHandler getFormOptions,
    CreateProductHandler createProduct,
    UpdateProductHandler updateProduct) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewData["Search"] = search;
        return View(await getProducts.HandleAsync(search, page, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var product = await getProductById.HandleAsync(id, cancellationToken);

        return product is null ? NotFound() : View(product);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new ProductFormViewModel();
        await FillOptionsAsync(model, cancellationToken);

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(ProductFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await FillOptionsAsync(model, cancellationToken);
            return View(model);
        }

        try
        {
            await createProduct.HandleAsync(
                new CreateProductCommand(
                    model.Sku,
                    model.Name,
                    model.CategoryId!.Value,
                    model.BrandId!.Value,
                    model.PurchasePrice,
                    model.SalePrice,
                    model.ReorderLevel,
                    model.DiscountPercent,
                    model.DiscountStartsOn,
                    model.DiscountEndsOn,
                    model.Barcode,
                    model.GenerateBarcode),
                cancellationToken);
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await FillOptionsAsync(model, cancellationToken);
            return View(model);
        }

        TempData["Success"] = "Product created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var product = await getProductById.HandleAsync(id, cancellationToken);

        if (product is null)
        {
            return NotFound();
        }

        var model = new ProductFormViewModel
        {
            Id = product.Id,
            Sku = product.Sku,
            Barcode = product.Barcode,
            Name = product.Name,
            CategoryId = product.CategoryId,
            BrandId = product.BrandId,
            PurchasePrice = product.PurchasePrice,
            SalePrice = product.SalePrice,
            ReorderLevel = product.ReorderLevel,
            DiscountPercent = product.DiscountPercent,
            DiscountStartsOn = product.DiscountStartsOn,
            DiscountEndsOn = product.DiscountEndsOn,
            IsActive = product.IsActive
        };

        await FillOptionsAsync(model, cancellationToken);

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(ProductFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await FillOptionsAsync(model, cancellationToken);
            return View(model);
        }

        try
        {
            await updateProduct.HandleAsync(
                new UpdateProductCommand(
                    model.Id,
                    model.Sku,
                    model.Name,
                    model.CategoryId!.Value,
                    model.BrandId!.Value,
                    model.PurchasePrice,
                    model.SalePrice,
                    model.ReorderLevel,
                    model.IsActive,
                    model.DiscountPercent,
                    model.DiscountStartsOn,
                    model.DiscountEndsOn,
                    model.Barcode,
                    model.GenerateBarcode),
                cancellationToken);
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await FillOptionsAsync(model, cancellationToken);
            return View(model);
        }

        TempData["Success"] = "Product updated.";
        return RedirectToAction(nameof(Index));
    }

    private async Task FillOptionsAsync(ProductFormViewModel model, CancellationToken cancellationToken)
    {
        var options = await getFormOptions.HandleAsync(model.CategoryId, model.BrandId, cancellationToken);

        model.Categories = options.Categories
            .Select(c => new SelectListItem(c.Name, c.Id.ToString()));

        model.Brands = options.Brands
            .Select(b => new SelectListItem(b.Name, b.Id.ToString()));
    }
}
