using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Optical.Domain.Entities;

namespace Optical.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.Property(p => p.Sku)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.Barcode)
            .HasMaxLength(32);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(p => p.PurchasePrice)
            .HasPrecision(18, 2);

        builder.Property(p => p.SalePrice)
            .HasPrecision(18, 2);

        builder.Property(p => p.DiscountPercent)
            .HasPrecision(5, 2);

        builder.HasIndex(p => p.Sku)
            .IsUnique();

        // Unique where present. Products without a barcode are all null, and nulls do not
        // clash with one another, so no filter is needed.
        builder.HasIndex(p => p.Barcode)
            .IsUnique();

        // Restrict: a category or brand that still has products cannot be deleted.
        builder.HasOne(p => p.Category)
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Brand)
            .WithMany()
            .HasForeignKey(p => p.BrandId)
            .OnDelete(DeleteBehavior.Restrict);

        // The server is the source of truth for money, not the browser.
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Products_PurchasePrice", "\"PurchasePrice\" >= 0");
            t.HasCheckConstraint("CK_Products_SalePrice", "\"SalePrice\" >= 0");
            t.HasCheckConstraint("CK_Products_ReorderLevel", "\"ReorderLevel\" >= 0");

            t.HasCheckConstraint(
                "CK_Products_DiscountPercent",
                "\"DiscountPercent\" >= 0 AND 100 - \"DiscountPercent\" >= 0");

            // An offer either has both ends or is not an offer. Written as a subtraction so
            // SQLite, which the tests run on, compares the dates rather than their text.
            t.HasCheckConstraint(
                "CK_Products_DiscountDates",
                "(\"DiscountStartsOn\" IS NULL AND \"DiscountEndsOn\" IS NULL) "
                + "OR (\"DiscountStartsOn\" IS NOT NULL AND \"DiscountEndsOn\" IS NOT NULL "
                + "AND \"DiscountEndsOn\" >= \"DiscountStartsOn\")");
        });
    }
}
