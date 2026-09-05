using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Optical.Application.Common;
using Optical.Application.Features.Permissions;
using Optical.Domain.Exceptions;
using Optical.Web.ViewModels;

namespace Optical.Web.Controllers;

/// <summary>
/// Which role may reach which module. The list of modules is fixed in code; who holds
/// them is yours to change.
/// </summary>
[Authorize(Policy = AppPermissions.ManageUsers)]
public class RolesController(RolePermissionStore permissions) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var matrix = await permissions.GetMatrixAsync(cancellationToken);

        return View(new RolePermissionsViewModel
        {
            Rows = AppPermissions.All
                .Select(definition => new PermissionRowViewModel
                {
                    Key = definition.Key,
                    Name = definition.Name,
                    Description = definition.Description,
                    GrantedTo = AppRoles.All
                        .Where(role => matrix.TryGetValue(role, out var set) && set.Contains(definition.Key))
                        .ToHashSet(StringComparer.Ordinal)
                })
                .ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> Index(
        Dictionary<string, List<string>>? grants,
        CancellationToken cancellationToken)
    {
        try
        {
            // A cleared checkbox posts nothing, so every role is saved, not only the ones present.
            foreach (var role in AppRoles.All)
            {
                var granted = grants is not null && grants.TryGetValue(role, out var keys)
                    ? keys
                    : [];

                await permissions.SaveAsync(role, granted, cancellationToken);
            }

            TempData["Success"] = "Role permissions updated. Staff see the change on their next page.";
        }
        catch (DomainException ex)
        {
            TempData["Success"] = null;
            ModelState.AddModelError(string.Empty, ex.Message);
        }

        return RedirectToAction(nameof(Index));
    }
}
