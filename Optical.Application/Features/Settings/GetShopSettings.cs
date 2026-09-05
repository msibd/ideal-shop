using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;

namespace Optical.Application.Features.Settings;

public sealed record ShopSettingsDetails(string BusinessName, string Phone, string Address);

public sealed class GetShopSettingsHandler(IApplicationDbContext db)
{
    /// <summary>Returns the shop's business details, or null when they have never been saved.</summary>
    public Task<ShopSettingsDetails?> HandleAsync(CancellationToken cancellationToken = default) =>
        db.ShopSettings
            .AsNoTracking()
            .Select(s => new ShopSettingsDetails(s.BusinessName, s.Phone, s.Address))
            .FirstOrDefaultAsync(cancellationToken);
}
