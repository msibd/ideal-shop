using Microsoft.EntityFrameworkCore;
using Optical.Domain.Entities;

namespace Optical.Application.Abstractions.Persistence;

/// <summary>
/// The database as the Application layer sees it. Implemented by ApplicationDbContext.
/// EF Core's DbContext already gives us unit-of-work behaviour, so no extra abstraction is needed.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Category> Categories { get; }

    DbSet<Brand> Brands { get; }

    DbSet<Product> Products { get; }

    DbSet<Customer> Customers { get; }

    DbSet<Supplier> Suppliers { get; }

    DbSet<Purchase> Purchases { get; }

    DbSet<PurchaseItem> PurchaseItems { get; }

    DbSet<Sale> Sales { get; }

    DbSet<SaleItem> SaleItems { get; }

    DbSet<Payment> Payments { get; }

    DbSet<Exchange> Exchanges { get; }

    DbSet<ExchangeReturnedItem> ExchangeReturnedItems { get; }

    DbSet<ExchangeReplacementItem> ExchangeReplacementItems { get; }

    DbSet<InventoryItem> InventoryItems { get; }

    DbSet<ShopSettings> ShopSettings { get; }

    DbSet<RolePermission> RolePermissions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
