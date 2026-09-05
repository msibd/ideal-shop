using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;

namespace Optical.Application.Features.Products;

public sealed record SelectOption(int Id, string Name);

public sealed record ProductFormOptions(
    IReadOnlyList<SelectOption> Categories,
    IReadOnlyList<SelectOption> Brands);

public sealed class GetProductFormOptionsHandler(IApplicationDbContext db)
{
    /// <summary>
    /// Active categories and brands for the dropdowns. The ids currently assigned to the
    /// product being edited are always included, so an entry that was switched off later
    /// does not silently disappear from the form.
    /// </summary>
    public async Task<ProductFormOptions> HandleAsync(
        int? includeCategoryId = null,
        int? includeBrandId = null,
        CancellationToken cancellationToken = default)
    {
        var categories = await db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive || c.Id == includeCategoryId)
            .OrderBy(c => c.Name)
            .Select(c => new SelectOption(c.Id, c.IsActive ? c.Name : c.Name + " (inactive)"))
            .ToListAsync(cancellationToken);

        var brands = await db.Brands
            .AsNoTracking()
            .Where(b => b.IsActive || b.Id == includeBrandId)
            .OrderBy(b => b.Name)
            .Select(b => new SelectOption(b.Id, b.IsActive ? b.Name : b.Name + " (inactive)"))
            .ToListAsync(cancellationToken);

        return new ProductFormOptions(categories, brands);
    }
}
