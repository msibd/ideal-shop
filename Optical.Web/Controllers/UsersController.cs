using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Optical.Application.Abstractions.Identity;
using Optical.Application.Common;
using Optical.Application.Features.Users;
using Optical.Domain.Exceptions;
using Optical.Web.ViewModels;

namespace Optical.Web.Controllers;

/// <summary>
/// Staff accounts. Admin only — an Owner runs the shop, an Admin runs the system.
/// </summary>
[Authorize(Policy = AppPermissions.ManageUsers)]
public class UsersController(ManageUsersHandler users, ICurrentUserService currentUser) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["CurrentUserId"] = currentUser.UserId;

        return View(await users.GetAllAsync(cancellationToken));
    }

    [HttpGet]
    public IActionResult Create() => View(new CreateUserViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var outcome = await users.CreateAsync(
                new CreateUserCommand(model.UserName, model.FullName, model.Email, model.Password, model.Role),
                cancellationToken);

            if (!outcome.Succeeded)
            {
                return Reject(model, outcome);
            }

            TempData["Success"] = $"{model.Role} account \"{model.UserName}\" created.";
            return RedirectToAction(nameof(Index));
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id, CancellationToken cancellationToken)
    {
        var user = await users.FindAsync(id);

        if (user is null)
        {
            return NotFound();
        }

        return View(new EditUserViewModel
        {
            Id = user.Id,
            UserName = user.UserName,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role,
            IsActive = user.IsActive,
            IsSelf = string.Equals(user.Id, currentUser.UserId, StringComparison.Ordinal)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(EditUserViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await RedrawAsync(model);
        }

        try
        {
            var outcome = await users.UpdateAsync(
                new UpdateUserCommand(model.Id, model.FullName, model.Email, model.Role, model.IsActive),
                cancellationToken);

            if (!outcome.Succeeded)
            {
                foreach (var error in outcome.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                return await RedrawAsync(model);
            }

            TempData["Success"] = $"Account \"{model.UserName}\" updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await RedrawAsync(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string id, CancellationToken cancellationToken)
    {
        var user = await users.FindAsync(id);

        if (user is null)
        {
            return NotFound();
        }

        return View(new ResetPasswordViewModel { Id = user.Id, UserName = user.UserName });
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var outcome = await users.ResetPasswordAsync(
                new ResetPasswordCommand(model.Id, model.NewPassword), cancellationToken);

            if (!outcome.Succeeded)
            {
                foreach (var error in outcome.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                return View(model);
            }

            TempData["Success"] = $"Password reset for \"{model.UserName}\". Share it with them directly.";
            return RedirectToAction(nameof(Index));
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    private IActionResult Reject(CreateUserViewModel model, IdentityOutcome outcome)
    {
        foreach (var error in outcome.Errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }

        return View(model);
    }

    /// <summary>The username is display-only, so it is reloaded before redrawing the form.</summary>
    private async Task<IActionResult> RedrawAsync(EditUserViewModel model)
    {
        var user = await users.FindAsync(model.Id);

        if (user is null)
        {
            return NotFound();
        }

        model.UserName = user.UserName;
        model.IsSelf = string.Equals(user.Id, currentUser.UserId, StringComparison.Ordinal);

        return View(model);
    }
}
