using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Optical.Application.Common;
using Optical.Application.Features.Categories;
using Optical.Domain.Exceptions;
using Optical.Web.ViewModels;

namespace Optical.Web.Controllers;

[Authorize(Policy = AppPermissions.Catalog)]
public class CategoriesController(
    GetCategoriesHandler getCategories,
    GetCategoryByIdHandler getCategoryById,
    CreateCategoryHandler createCategory,
    UpdateCategoryHandler updateCategory) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken)
    {
        ViewData["Search"] = search;
        return View(await getCategories.HandleAsync(search, cancellationToken));
    }

    [HttpGet]
    public IActionResult Create() => View(new CategoryFormViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(CategoryFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await createCategory.HandleAsync(
                new CreateCategoryCommand(model.Name, model.Description), cancellationToken);
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(nameof(model.Name), ex.Message);
            return View(model);
        }

        TempData["Success"] = "Category created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var category = await getCategoryById.HandleAsync(id, cancellationToken);

        if (category is null)
        {
            return NotFound();
        }

        return View(new CategoryFormViewModel
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            IsActive = category.IsActive
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(CategoryFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await updateCategory.HandleAsync(
                new UpdateCategoryCommand(model.Id, model.Name, model.Description, model.IsActive),
                cancellationToken);
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(nameof(model.Name), ex.Message);
            return View(model);
        }

        TempData["Success"] = "Category updated.";
        return RedirectToAction(nameof(Index));
    }
}
