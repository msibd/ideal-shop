using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Domain.Entities;
using Optical.Domain.Exceptions;

namespace Optical.Application.Features.Products;

public sealed record CreateProductCommand(
    string Sku,
    string Name,
    int CategoryId,
    int BrandId,
    decimal PurchasePrice,
    decimal SalePrice,
    int ReorderLevel,
    decimal DiscountPercent = 0m,
    DateOnly? DiscountStartsOn = null,
    DateOnly? DiscountEndsOn = null,
    string? Barcode = null,
    bool GenerateBarcode = false);

public sealed class CreateProductHandler(IApplicationDbContext db)
{
    public async Task<int> HandleAsync(CreateProductCommand command, CancellationToken cancellationToken = default)
    {
        var sku = command.Sku.Trim();
        var normalizedSku = sku.ToLower();

        if (await db.Products.AnyAsync(p => p.Sku.ToLower() == normalizedSku, cancellationToken))
        {
            throw new DomainException($"SKU \"{sku}\" is already used by another product.");
        }

        await ProductRules.EnsureCategoryAndBrandExistAsync(db, command.CategoryId, command.BrandId, cancellationToken);

        var discount = ProductRules.NormalizeDiscount(
            command.DiscountPercent,
            command.DiscountStartsOn,
            command.DiscountEndsOn);

        var barcode = ProductRules.NormalizeBarcode(command.Barcode);

        if (barcode is null && command.GenerateBarcode)
        {
            barcode = await ProductRules.NextInternalBarcodeAsync(db, cancellationToken);
        }

        await ProductRules.EnsureBarcodeIsFreeAsync(db, barcode, null, cancellationToken);

        var product = new Product
        {
            Sku = sku,
            Barcode = barcode,
            Name = command.Name.Trim(),
            CategoryId = command.CategoryId,
            BrandId = command.BrandId,
            PurchasePrice = command.PurchasePrice,
            SalePrice = command.SalePrice,
            ReorderLevel = command.ReorderLevel,
            DiscountPercent = discount.Percent,
            DiscountStartsOn = discount.StartsOn,
            DiscountEndsOn = discount.EndsOn,
            IsActive = true
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return product.Id;
    }
}
