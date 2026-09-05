using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Optical.Application.Common;
using Optical.Application.Features.Dashboard;
using Optical.Application.Features.Permissions;
using Optical.Web.Models;

namespace Optical.Web.Controllers;

[Authorize]
public class HomeController(
    GetDashboardSummaryHandler getDashboardSummary,
    RolePermissionStore permissions) : Controller
{
    /// <summary>
    /// The landing page. Whoever is not granted the dashboard is sent to the till instead
    /// of being refused, so signing in never lands on an error. The decision reads the same
    /// permission the tab and the policy do.
    /// </summary>
    public async Task<IActionResult> Index(
        DashboardRangeKind range = DashboardRangeKind.Today,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToList();

        if (!await permissions.IsGrantedAsync(roles, AppPermissions.Dashboard, cancellationToken))
        {
            return RedirectToAction("Index", "Pos");
        }

        var period = DashboardRange.Create(range, from, to, DateOnly.FromDateTime(DateTime.Today));

        return View(await getDashboardSummary.HandleAsync(period, cancellationToken));
    }

    /// <summary>
    /// Just the stat cards for a period. The panels below them describe stock as it stands,
    /// so changing the period never needs to redraw the whole page.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AppPermissions.Dashboard)]
    public async Task<IActionResult> Stats(
        DashboardRangeKind range = DashboardRangeKind.Today,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var period = DashboardRange.Create(range, from, to, DateOnly.FromDateTime(DateTime.Today));

        return PartialView("_DashboardStats", await getDashboardSummary.HandleAsync(period, cancellationToken));
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    /// <summary>
    /// Friendly page for 404s and other status codes, so a bad URL or a blocked page
    /// lands inside the application instead of on an empty browser error.
    /// </summary>
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult HttpError(int code = 404)
    {
        // The code arrives in the URL, so it is clamped rather than echoed straight back.
        ViewData["StatusCode"] = code is >= 400 and <= 599 ? code : 404;

        return View();
    }
}
