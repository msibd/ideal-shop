using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Optical.Application.Common;
using Optical.Application.Features.Suppliers;
using Optical.Domain.Exceptions;
using Optical.Web.ViewModels;

namespace Optical.Web.Controllers;

[Authorize(Policy = AppPermissions.Purchasing)]
public class SuppliersController(
    GetSuppliersHandler getSuppliers,
    GetSupplierByIdHandler getSupplierById,
    CreateSupplierHandler createSupplier,
    UpdateSupplierHandler updateSupplier) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewData["Search"] = search;
        return View(await getSuppliers.HandleAsync(search, page, cancellationToken));
    }

    [HttpGet]
    public IActionResult Create() => View(new SupplierFormViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(SupplierFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await createSupplier.HandleAsync(
                new CreateSupplierCommand(model.Name, model.Phone, model.Email, model.Address),
                cancellationToken);
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        TempData["Success"] = "Supplier created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var supplier = await getSupplierById.HandleAsync(id, cancellationToken);

        if (supplier is null)
        {
            return NotFound();
        }

        return View(new SupplierFormViewModel
        {
            Id = supplier.Id,
            Name = supplier.Name,
            Phone = supplier.Phone,
            Email = supplier.Email,
            Address = supplier.Address,
            IsActive = supplier.IsActive
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(SupplierFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await updateSupplier.HandleAsync(
                new UpdateSupplierCommand(
                    model.Id,
                    model.Name,
                    model.Phone,
                    model.Email,
                    model.Address,
                    model.IsActive),
                cancellationToken);
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        TempData["Success"] = "Supplier updated.";
        return RedirectToAction(nameof(Index));
    }
}
