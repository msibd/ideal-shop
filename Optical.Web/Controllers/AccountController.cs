using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Optical.Application.Abstractions.Identity;
using Optical.Application.Common;
using Optical.Application.Features.Account;
using Optical.Web.ViewModels;

namespace Optical.Web.Controllers;

public class AccountController(
    IIdentityService identityService,
    ChangePasswordHandler changePassword,
    GetProfileHandler getProfile,
    UpdateProfileHandler updateProfile) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await identityService.LoginAsync(model.UserName, model.Password, model.RememberMe);

        if (result is LoginResult.Success)
        {
            return RedirectToLocal(model.ReturnUrl);
        }

        ModelState.AddModelError(string.Empty, result switch
        {
            LoginResult.LockedOut => "This account is locked. Please try again later.",
            LoginResult.Disabled => "This account is disabled. Please contact the administrator.",
            _ => "Invalid username or password."
        });

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await identityService.LogoutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    /// <summary>
    /// Editing one's own display name, for whoever holds the Own Profile permission.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AppPermissions.Profile)]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken)
    {
        var profile = await getProfile.HandleAsync(cancellationToken);

        if (profile is null)
        {
            return NotFound();
        }

        return View(new ProfileViewModel
        {
            FullName = profile.FullName,
            UserName = profile.UserName,
            Email = profile.Email
        });
    }

    [HttpPost]
    [Authorize(Policy = AppPermissions.Profile)]
    public async Task<IActionResult> Profile(ProfileViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await RestoreReadOnlyFieldsAsync(model, cancellationToken);
            return View(model);
        }

        await updateProfile.HandleAsync(new UpdateProfileCommand(model.FullName), cancellationToken);

        TempData["Success"] = "Your name has been updated.";
        return RedirectToAction(nameof(Profile));
    }

    /// <summary>
    /// Changing one's own password. Whoever is not granted this has it reset by an Admin,
    /// which is deliberate for a shared till account.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AppPermissions.Profile)]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost]
    [Authorize(Policy = AppPermissions.Profile)]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var outcome = await changePassword.HandleAsync(
            new ChangePasswordCommand(model.CurrentPassword, model.NewPassword), cancellationToken);

        if (!outcome.Succeeded)
        {
            foreach (var error in outcome.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(model);
        }

        TempData["Success"] = "Your password has been changed.";
        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// Username and email are display-only on the form, so they are not posted back
    /// and must be reloaded before the page is redisplayed.
    /// </summary>
    private async Task RestoreReadOnlyFieldsAsync(ProfileViewModel model, CancellationToken cancellationToken)
    {
        var profile = await getProfile.HandleAsync(cancellationToken);

        model.UserName = profile?.UserName ?? string.Empty;
        model.Email = profile?.Email;
    }

    private IActionResult RedirectToLocal(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl!)
            : RedirectToAction("Index", "Home");
}
