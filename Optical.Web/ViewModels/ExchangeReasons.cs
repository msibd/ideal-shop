using Microsoft.AspNetCore.Mvc.Rendering;
using Optical.Domain.Enums;

namespace Optical.Web.ViewModels;

/// <summary>
/// Readable labels for <see cref="ExchangeReason"/>. Kept in the Web layer so the Domain
/// enum stays free of presentation concerns.
/// </summary>
public static class ExchangeReasons
{
    private static readonly Dictionary<ExchangeReason, string> Names = new()
    {
        [ExchangeReason.WrongPrescription] = "Wrong prescription",
        [ExchangeReason.PoorFit] = "Does not fit",
        [ExchangeReason.DamagedOrDefective] = "Damaged or defective",
        [ExchangeReason.WrongProductGiven] = "Wrong product given",
        [ExchangeReason.CustomerChangedMind] = "Customer changed mind",
        [ExchangeReason.Other] = "Other"
    };

    public static string Name(ExchangeReason reason) =>
        Names.TryGetValue(reason, out var name) ? name : reason.ToString();

    public static IEnumerable<SelectListItem> SelectList(ExchangeReason? selected = null) =>
        Names.Select(entry => new SelectListItem(
            entry.Value,
            ((int)entry.Key).ToString(),
            selected == entry.Key));
}
