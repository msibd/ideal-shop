using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Optical.Application.Common;
using Optical.Application.Features.Exchanges;
using Optical.Domain.Exceptions;
using Optical.Web.ViewModels;

namespace Optical.Web.Controllers;

/// <summary>
/// Goods swapped for other goods. The shop does not refund money, so this is the only way
/// a sold product comes back — and the replacement must be worth at least as much.
/// </summary>
[Authorize(Policy = AppPermissions.Exchanges)]
public class ExchangesController(
    GetExchangesHandler getExchanges,
    GetExchangeByIdHandler getExchangeById,
    GetExchangeableSaleHandler getExchangeableSale,
    CreateExchangeHandler createExchange) : Controller
{
    /// <summary>Blank replacement rows offered on the form, so a swap can widen if needed.</summary>
    private const int ReplacementRows = 3;

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        DateOnly? from,
        DateOnly? to,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        ViewData["Search"] = search;
        ViewData["From"] = from;
        ViewData["To"] = to;

        return View(await getExchanges.HandleAsync(search, from, to, page, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var exchange = await getExchangeById.HandleAsync(id, cancellationToken);

        return exchange is null ? NotFound() : View(exchange);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int saleId, CancellationToken cancellationToken)
    {
        var sale = await getExchangeableSale.HandleAsync(saleId, cancellationToken);

        if (sale is null)
        {
            return NotFound();
        }

        var model = new ExchangeFormViewModel
        {
            SaleId = sale.SaleId,
            Sale = sale,
            Returned = sale.Lines
                .Select(l => new ExchangeReturnLineViewModel { SaleItemId = l.SaleItemId })
                .ToList(),
            Replacements = Enumerable.Range(0, ReplacementRows)
                .Select(_ => new ExchangeReplacementLineViewModel())
                .ToList()
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(ExchangeFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await RejectAsync(model, cancellationToken);
        }

        try
        {
            var result = await createExchange.HandleAsync(
                new CreateExchangeCommand(
                    model.SaleId,
                    model.Reason!.Value,
                    model.Note,
                    model.PaymentMethod,
                    model.Returned
                        .Select(l => new ExchangeReturnCommand(l.SaleItemId, l.Quantity))
                        .ToList(),
                    model.Replacements
                        .Where(l => l.ProductId is not null)
                        .Select(l => new ExchangeReplacementCommand(l.ProductId!.Value, l.Quantity))
                        .ToList()),
                cancellationToken);

            TempData["Success"] = result.AmountPaid > 0
                ? $"Exchange {result.ExchangeNumber} recorded. Adjustment of {result.AmountPaid:N2} collected."
                : $"Exchange {result.ExchangeNumber} recorded as an even swap — no adjustment due.";

            return RedirectToAction(nameof(Details), new { id = result.ExchangeId });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await RejectAsync(model, cancellationToken);
        }
    }

    /// <summary>
    /// Redraws the form from the database, never from what was posted, so the exchangeable
    /// quantities and prices on screen are always the real ones.
    /// </summary>
    private async Task<IActionResult> RejectAsync(ExchangeFormViewModel model, CancellationToken cancellationToken)
    {
        model.Sale = await getExchangeableSale.HandleAsync(model.SaleId, cancellationToken);

        if (model.Sale is null)
        {
            return NotFound();
        }

        // Keep the rows the user was working in, and always leave room for one more.
        while (model.Replacements.Count < ReplacementRows)
        {
            model.Replacements.Add(new ExchangeReplacementLineViewModel());
        }

        return View(model);
    }
}
