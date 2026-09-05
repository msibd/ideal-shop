using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Optical.Application.Common;
using Optical.Application.Features.Customers;
using Optical.Domain.Exceptions;
using Optical.Web.ViewModels;

namespace Optical.Web.Controllers;

[Authorize(Policy = AppPermissions.Customers)]
public class CustomersController(
    GetCustomersHandler getCustomers,
    GetCustomerByIdHandler getCustomerById,
    CreateCustomerHandler createCustomer,
    UpdateCustomerHandler updateCustomer) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewData["Search"] = search;
        return View(await getCustomers.HandleAsync(search, page, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var customer = await getCustomerById.HandleAsync(id, cancellationToken);

        return customer is null ? NotFound() : View(customer);
    }

    [HttpGet]
    public IActionResult Create() => View(new CustomerFormViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(CustomerFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await createCustomer.HandleAsync(
                new CreateCustomerCommand(model.Name, model.Phone, model.Email, model.Address, model.Notes),
                cancellationToken);
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        TempData["Success"] = "Customer created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var customer = await getCustomerById.HandleAsync(id, cancellationToken);

        if (customer is null)
        {
            return NotFound();
        }

        return View(new CustomerFormViewModel
        {
            Id = customer.Id,
            Name = customer.Name,
            Phone = customer.Phone,
            Email = customer.Email,
            Address = customer.Address,
            Notes = customer.Notes,
            IsActive = customer.IsActive
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(CustomerFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await updateCustomer.HandleAsync(
                new UpdateCustomerCommand(
                    model.Id,
                    model.Name,
                    model.Phone,
                    model.Email,
                    model.Address,
                    model.Notes,
                    model.IsActive),
                cancellationToken);
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        TempData["Success"] = "Customer updated.";
        return RedirectToAction(nameof(Index));
    }
}
