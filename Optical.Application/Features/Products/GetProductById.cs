using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Domain.Entities;

namespace Optical.Application.Features.Products;

public sealed record ProductDetails(
    int Id,
    string Sku,
    string? Barcode,
    string Name,
    int CategoryId,
    string CategoryName,
    int BrandId,
    string BrandName,
    decimal PurchasePrice,
    decimal SalePrice,
    int ReorderLevel,
    decimal DiscountPercent,
    DateOnly? DiscountStartsOn,
    DateOnly? DiscountEndsOn,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    /// <summary>Whether the offer is actually taking money off the till price today.</summary>
    public bool IsDiscountRunning => Product.IsDiscountRunning(
        DiscountPercent,
        DiscountStartsOn,
        DiscountEndsOn,
        DateOnly.FromDateTime(DateTime.Now));

    /// <summary>An offer that is set up but has not started yet, or has already finished.</summary>
    public bool HasDiscount => DiscountPercent > 0;

    public decimal DiscountedPrice =>
        IsDiscountRunning
            ? decimal.Round(SalePrice * (100m - DiscountPercent) / 100m, 2, MidpointRounding.AwayFromZero)
            : SalePrice;
}

public sealed class GetProductByIdHandler(IApplicationDbContext db)
{
    public Task<ProductDetails?> HandleAsync(int id, CancellationToken cancellationToken = default) =>
        db.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProductDetails(
                p.Id,
                p.Sku,
                p.Barcode,
                p.Name,
                p.CategoryId,
                p.Category.Name,
                p.BrandId,
                p.Brand.Name,
                p.PurchasePrice,
                p.SalePrice,
                p.ReorderLevel,
                p.DiscountPercent,
                p.DiscountStartsOn,
                p.DiscountEndsOn,
                p.IsActive,
                p.CreatedAt,
                p.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);
}
