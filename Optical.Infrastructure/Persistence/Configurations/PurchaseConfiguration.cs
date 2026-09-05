using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Optical.Domain.Entities;

namespace Optical.Infrastructure.Persistence.Configurations;

public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.Property(p => p.InvoiceNumber)
            .HasMaxLength(50);

        builder.Property(p => p.TotalAmount)
            .HasPrecision(18, 2);

        builder.HasIndex(p => p.PurchaseDate);

        // Restrict: a supplier with purchase history cannot be deleted.
        builder.HasOne(p => p.Supplier)
            .WithMany()
            .HasForeignKey(p => p.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        // Cascade: the items only exist as part of their purchase.
        builder.HasMany(p => p.Items)
            .WithOne(i => i.Purchase)
            .HasForeignKey(i => i.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t =>
            t.HasCheckConstraint("CK_Purchases_TotalAmount", "\"TotalAmount\" >= 0"));
    }
}
