using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Optical.Domain.Entities;
using Optical.Infrastructure.Persistence;

namespace Optical.Tests;

/// <summary>
/// A throwaway SQLite database per test, created from the real EF model so that
/// relationships and check constraints are exercised, not mocked away.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection connection;

    public TestDatabase()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        Db = new ApplicationDbContext(options);
        Db.Database.EnsureCreated();
    }

    public ApplicationDbContext Db { get; }

    public Supplier AddSupplier(bool isActive = true)
    {
        var supplier = new Supplier { Name = $"Supplier {Guid.NewGuid():N}", IsActive = isActive };
        Db.Suppliers.Add(supplier);
        Db.SaveChanges();

        return supplier;
    }

    public Product AddProduct(bool isActive = true, int reorderLevel = 5)
    {
        var category = new Category { Name = $"Category {Guid.NewGuid():N}", IsActive = true };
        var brand = new Brand { Name = $"Brand {Guid.NewGuid():N}", IsActive = true };
        Db.Categories.Add(category);
        Db.Brands.Add(brand);
        Db.SaveChanges();

        var product = new Product
        {
            Sku = $"SKU-{Guid.NewGuid():N}"[..12],
            Name = $"Product {Guid.NewGuid():N}",
            CategoryId = category.Id,
            BrandId = brand.Id,
            PurchasePrice = 100m,
            SalePrice = 150m,
            ReorderLevel = reorderLevel,
            IsActive = isActive
        };

        Db.Products.Add(product);
        Db.SaveChanges();

        return product;
    }

    public Customer AddCustomer(bool isActive = true)
    {
        var customer = new Customer
        {
            Name = $"Customer {Guid.NewGuid():N}",
            Phone = Guid.NewGuid().ToString("N")[..11],
            IsActive = isActive
        };

        Db.Customers.Add(customer);
        Db.SaveChanges();

        return customer;
    }

    public void SetBusinessName(string name)
    {
        Db.ShopSettings.Add(new ShopSettings
        {
            Id = ShopSettings.SingleRowId,
            BusinessName = name,
            Address = "Test address"
        });
        Db.SaveChanges();
    }

    public void SetStock(int productId, int quantity)
    {
        var row = Db.InventoryItems.FirstOrDefault(i => i.ProductId == productId);

        if (row is null)
        {
            Db.InventoryItems.Add(new InventoryItem { ProductId = productId, Quantity = quantity });
        }
        else
        {
            row.Quantity = quantity;
        }

        Db.SaveChanges();
    }

    public int StockOf(int productId) =>
        Db.InventoryItems.AsNoTracking().FirstOrDefault(i => i.ProductId == productId)?.Quantity ?? 0;

    public void Dispose()
    {
        Db.Dispose();
        connection.Dispose();
    }
}
