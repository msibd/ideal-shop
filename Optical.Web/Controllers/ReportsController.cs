using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Optical.Application.Common;
using Optical.Application.Features.Reports;

namespace Optical.Web.Controllers;

/// <summary>Read-only summaries of what the shop sold and what it currently holds.</summary>
[Authorize(Policy = AppPermissions.Reports)]
public class ReportsController(
    GetSalesReportHandler getSalesReport,
    GetInventoryReportHandler getInventoryReport) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Sales(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var period = ReportPeriod.Create(from, to, DateOnly.FromDateTime(DateTime.Today));

        return View(await getSalesReport.HandleAsync(period, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Inventory(
        bool lowStockOnly = false,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        ViewData["LowStockOnly"] = lowStockOnly;

        return View(await getInventoryReport.HandleAsync(lowStockOnly, page, cancellationToken));
    }
}
