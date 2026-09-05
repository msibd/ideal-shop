using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Optical.Application.Common;
using Optical.Application.Features.Brands;
using Optical.Domain.Exceptions;
using Optical.Web.ViewModels;

namespace Optical.Web.Controllers;

[Authorize(Policy = AppPermissions.Catalog)]
public class BrandsController(
    GetBrandsHandler getBrands,
    GetBrandByIdHandler getBrandById,
    CreateBrandHandler createBrand,
    UpdateBrandHandler updateBrand) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken)
    {
        ViewData["Search"] = search;
        return View(await getBrands.HandleAsync(search, cancellationToken));
    }

    [HttpGet]
    public IActionResult Create() => View(new BrandFormViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(BrandFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await createBrand.HandleAsync(
                new CreateBrandCommand(model.Name, model.Description), cancellationToken);
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(nameof(model.Name), ex.Message);
            return View(model);
        }

        TempData["Success"] = "Brand created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var brand = await getBrandById.HandleAsync(id, cancellationToken);

        if (brand is null)
        {
            return NotFound();
        }

        return View(new BrandFormViewModel
        {
            Id = brand.Id,
            Name = brand.Name,
            Description = brand.Description,
            IsActive = brand.IsActive
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(BrandFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await updateBrand.HandleAsync(
                new UpdateBrandCommand(model.Id, model.Name, model.Description, model.IsActive),
                cancellationToken);
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(nameof(model.Name), ex.Message);
            return View(model);
        }

        TempData["Success"] = "Brand updated.";
        return RedirectToAction(nameof(Index));
    }
}
