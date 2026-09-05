using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Optical.Application.Common;
using Optical.Application.Features.Settings;
using Optical.Web.ViewModels;

namespace Optical.Web.Controllers;

/// <summary>
/// System settings. Restricted to the shop owner / administrator.
/// </summary>
[Authorize(Policy = AppPermissions.Settings)]
public class SettingsController(
    GetShopSettingsHandler getShopSettings,
    SaveShopSettingsHandler saveShopSettings) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var settings = await getShopSettings.HandleAsync(cancellationToken);

        ViewData["IsConfigured"] = settings is not null;

        return View(settings is null
            ? new ShopSettingsViewModel()
            : new ShopSettingsViewModel
            {
                BusinessName = settings.BusinessName,
                Phone = settings.Phone,
                Address = settings.Address
            });
    }

    [HttpPost]
    public async Task<IActionResult> Index(ShopSettingsViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            ViewData["IsConfigured"] = await getShopSettings.HandleAsync(cancellationToken) is not null;
            return View(model);
        }

        var created = await saveShopSettings.HandleAsync(
            new SaveShopSettingsCommand(model.BusinessName, model.Phone, model.Address), cancellationToken);

        TempData["Success"] = created
            ? "Business details saved."
            : "Business details updated.";

        return RedirectToAction(nameof(Index));
    }
}
