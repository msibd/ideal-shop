using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Domain.Exceptions;

namespace Optical.Application.Features.Products;

public sealed record UpdateProductCommand(
    int Id,
    string Sku,
    string Name,
    int CategoryId,
    int BrandId,
    decimal PurchasePrice,
    decimal SalePrice,
    int ReorderLevel,
    bool IsActive,
    decimal DiscountPercent = 0m,
    DateOnly? DiscountStartsOn = null,
    DateOnly? DiscountEndsOn = null,
    string? Barcode = null,
    bool GenerateBarcode = false);

public sealed class UpdateProductHandler(IApplicationDbContext db)
{
    public async Task HandleAsync(UpdateProductCommand command, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException($"Product {command.Id} was not found.");

        var sku = command.Sku.Trim();
        var normalizedSku = sku.ToLower();

        if (await db.Products.AnyAsync(
                p => p.Id != command.Id && p.Sku.ToLower() == normalizedSku, cancellationToken))
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

        await ProductRules.EnsureBarcodeIsFreeAsync(db, barcode, command.Id, cancellationToken);

        product.Sku = sku;
        product.Barcode = barcode;
        product.Name = command.Name.Trim();
        product.CategoryId = command.CategoryId;
        product.BrandId = command.BrandId;
        product.PurchasePrice = command.PurchasePrice;
        product.SalePrice = command.SalePrice;
        product.ReorderLevel = command.ReorderLevel;
        product.DiscountPercent = discount.Percent;
        product.DiscountStartsOn = discount.StartsOn;
        product.DiscountEndsOn = discount.EndsOn;
        product.IsActive = command.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
