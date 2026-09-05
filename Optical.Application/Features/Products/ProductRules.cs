using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Domain.Exceptions;

namespace Optical.Application.Features.Products;

/// <summary>
/// The rules Create and Update genuinely share. Kept here rather than duplicated,
/// and deliberately not turned into a validation framework.
/// </summary>
internal static class ProductRules
{
    public static async Task EnsureCategoryAndBrandExistAsync(
        IApplicationDbContext db,
        int categoryId,
        int brandId,
        CancellationToken cancellationToken)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == categoryId, cancellationToken))
        {
            throw new DomainException("The selected category no longer exists.");
        }

        if (!await db.Brands.AnyAsync(b => b.Id == brandId, cancellationToken))
        {
            throw new DomainException("The selected brand no longer exists.");
        }
    }

    /// <summary>
    /// The characters a barcode may hold. Deliberately narrower than Code 39 allows: digits,
    /// letters and dashes are what scanners and label printers handle without argument, and
    /// staying inside the set guarantees the receipt can always print the code.
    /// </summary>
    private const string AllowedBarcodeCharacters =
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-";

    /// <summary>Internal barcodes read as P000042, so they never look like a real EAN-13.</summary>
    private const string InternalBarcodePrefix = "P";

    private const int InternalBarcodeDigits = 6;

    /// <summary>
    /// Checks a barcode and returns it as it should be stored, or null when the product has
    /// none. Blank becomes null rather than an empty string so the unique index keeps working.
    /// </summary>
    public static string? NormalizeBarcode(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return null;
        }

        var trimmed = barcode.Trim().ToUpperInvariant();

        if (trimmed.Length > 32)
        {
            throw new DomainException("A barcode cannot be longer than 32 characters.");
        }

        foreach (var character in trimmed)
        {
            if (!AllowedBarcodeCharacters.Contains(character))
            {
                throw new DomainException(
                    "A barcode can only contain letters, digits and dashes.");
            }
        }

        return trimmed;
    }

    /// <summary>
    /// Refuses a barcode already carried by another product. Scanning is only useful if one
    /// code means one product, so this is the rule the whole feature rests on.
    /// </summary>
    public static async Task EnsureBarcodeIsFreeAsync(
        IApplicationDbContext db,
        string? barcode,
        int? exceptProductId,
        CancellationToken cancellationToken)
    {
        if (barcode is null)
        {
            return;
        }

        var taken = await db.Products.AnyAsync(
            p => p.Barcode == barcode && (exceptProductId == null || p.Id != exceptProductId),
            cancellationToken);

        if (taken)
        {
            throw new DomainException($"Barcode \"{barcode}\" is already used by another product.");
        }
    }

    /// <summary>
    /// The next internal barcode in sequence. Products creation is an admin-only, one-at-a-time
    /// job, so the highest existing code plus one is enough; the unique index is the backstop
    /// if two ever collide.
    /// </summary>
    public static async Task<string> NextInternalBarcodeAsync(
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        // Fixed-width numbers sort as text, so the highest string is the highest number.
        var latest = await db.Products
            .AsNoTracking()
            .Where(p => p.Barcode != null && p.Barcode.StartsWith(InternalBarcodePrefix))
            .OrderByDescending(p => p.Barcode)
            .Select(p => p.Barcode)
            .FirstOrDefaultAsync(cancellationToken);

        var sequence = 1;

        if (latest is not null
            && int.TryParse(latest[InternalBarcodePrefix.Length..], out var previous))
        {
            sequence = previous + 1;
        }

        return InternalBarcodePrefix + sequence.ToString(new string('0', InternalBarcodeDigits));
    }

    /// <summary>
    /// Checks an offer and returns it in the shape it should be stored: no percentage means
    /// no dates either, so an abandoned discount cannot linger and start applying again.
    /// </summary>
    public static (decimal Percent, DateOnly? StartsOn, DateOnly? EndsOn) NormalizeDiscount(
        decimal percent,
        DateOnly? startsOn,
        DateOnly? endsOn)
    {
        if (percent < 0)
        {
            throw new DomainException("The discount cannot be negative.");
        }

        if (percent > 100)
        {
            throw new DomainException("The discount cannot be more than 100%.");
        }

        if (percent == 0)
        {
            return (0m, null, null);
        }

        if (startsOn is null || endsOn is null)
        {
            throw new DomainException("A discount needs both a start date and an end date.");
        }

        if (endsOn < startsOn)
        {
            throw new DomainException("The discount cannot end before it starts.");
        }

        return (percent, startsOn, endsOn);
    }
}
