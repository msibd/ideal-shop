using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Domain.Entities;
using Optical.Domain.Exceptions;

namespace Optical.Application.Features.Inventory;

/// <summary>Sets the counted stock for a product, e.g. after a physical stock take.</summary>
public sealed record AdjustStockCommand(int ProductId, int NewQuantity);

public sealed class AdjustStockHandler(IApplicationDbContext db)
{
    public async Task HandleAsync(AdjustStockCommand command, CancellationToken cancellationToken = default)
    {
        if (command.NewQuantity < 0)
        {
            throw new DomainException("Stock cannot be negative.");
        }

        var productExists = await db.Products.AnyAsync(p => p.Id == command.ProductId, cancellationToken);

        if (!productExists)
        {
            throw new NotFoundException($"Product {command.ProductId} was not found.");
        }

        var stock = await db.InventoryItems
            .FirstOrDefaultAsync(i => i.ProductId == command.ProductId, cancellationToken);

        if (stock is null)
        {
            db.InventoryItems.Add(new InventoryItem
            {
                ProductId = command.ProductId,
                Quantity = command.NewQuantity
            });
        }
        else
        {
            stock.Quantity = command.NewQuantity;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
