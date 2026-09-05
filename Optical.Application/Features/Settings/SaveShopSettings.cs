using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Domain.Entities;

namespace Optical.Application.Features.Settings;

public sealed record SaveShopSettingsCommand(string BusinessName, string Phone, string Address);

public sealed class SaveShopSettingsHandler(IApplicationDbContext db)
{
    /// <summary>
    /// Creates the settings row on first save and updates it afterwards.
    /// Returns true when the row was created.
    /// </summary>
    public async Task<bool> HandleAsync(SaveShopSettingsCommand command, CancellationToken cancellationToken = default)
    {
        var settings = await db.ShopSettings.FirstOrDefaultAsync(cancellationToken);
        var created = settings is null;

        if (settings is null)
        {
            settings = new ShopSettings { Id = ShopSettings.SingleRowId };
            db.ShopSettings.Add(settings);
        }

        settings.BusinessName = command.BusinessName.Trim();
        settings.Phone = command.Phone.Trim();
        settings.Address = command.Address.Trim();

        await db.SaveChangesAsync(cancellationToken);

        return created;
    }
}
