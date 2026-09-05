using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Optical.Domain.Entities;

namespace Optical.Infrastructure.Persistence.Configurations;

public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        // One stock row per product.
        builder.HasIndex(i => i.ProductId)
            .IsUnique();

        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // Last line of defence: stock can never go negative, whatever the code does.
        builder.ToTable(t =>
            t.HasCheckConstraint("CK_InventoryItems_Quantity", "\"Quantity\" >= 0"));
    }
}
