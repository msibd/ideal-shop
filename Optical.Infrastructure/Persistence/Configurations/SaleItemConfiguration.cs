using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Optical.Domain.Entities;

namespace Optical.Infrastructure.Persistence.Configurations;

public class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> builder)
    {
        builder.Property(i => i.UnitPrice)
            .HasPrecision(18, 2);

        builder.Property(i => i.DiscountPercent)
            .HasPrecision(5, 2);

        builder.Property(i => i.DiscountAmount)
            .HasPrecision(18, 2);

        builder.Property(i => i.LineTotal)
            .HasPrecision(18, 2);

        // Restrict: a product that has been sold cannot be deleted.
        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.ProductId);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_SaleItems_Quantity", "\"Quantity\" > 0");
            t.HasCheckConstraint("CK_SaleItems_UnitPrice", "\"UnitPrice\" >= 0");

            t.HasCheckConstraint(
                "CK_SaleItems_DiscountAmount",
                "\"DiscountAmount\" >= 0 AND \"Quantity\" * \"UnitPrice\" - \"DiscountAmount\" >= 0");
        });
    }
}
