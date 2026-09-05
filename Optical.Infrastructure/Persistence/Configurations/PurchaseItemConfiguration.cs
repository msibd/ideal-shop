using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Optical.Domain.Entities;

namespace Optical.Infrastructure.Persistence.Configurations;

public class PurchaseItemConfiguration : IEntityTypeConfiguration<PurchaseItem>
{
    public void Configure(EntityTypeBuilder<PurchaseItem> builder)
    {
        builder.Property(i => i.PurchasePrice)
            .HasPrecision(18, 2);

        builder.Property(i => i.LineTotal)
            .HasPrecision(18, 2);

        // Restrict: a product that has been purchased cannot be deleted.
        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.ProductId);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_PurchaseItems_Quantity", "\"Quantity\" > 0");
            t.HasCheckConstraint("CK_PurchaseItems_PurchasePrice", "\"PurchasePrice\" >= 0");
        });
    }
}
