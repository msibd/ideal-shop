using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Optical.Domain.Entities;

namespace Optical.Infrastructure.Persistence.Configurations;

public class ExchangeReturnedItemConfiguration : IEntityTypeConfiguration<ExchangeReturnedItem>
{
    public void Configure(EntityTypeBuilder<ExchangeReturnedItem> builder)
    {
        builder.Property(i => i.UnitPrice)
            .HasPrecision(18, 2);

        builder.Property(i => i.LineTotal)
            .HasPrecision(18, 2);

        // Restrict: the sale line is the record of what may still be handed back.
        builder.HasOne(i => i.SaleItem)
            .WithMany()
            .HasForeignKey(i => i.SaleItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // How much of a sale line has already come back is looked up on every exchange.
        builder.HasIndex(i => i.SaleItemId);

        builder.HasIndex(i => i.ProductId);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_ExchangeReturnedItems_Quantity", "\"Quantity\" > 0");
            t.HasCheckConstraint("CK_ExchangeReturnedItems_UnitPrice", "\"UnitPrice\" >= 0");
        });
    }
}

public class ExchangeReplacementItemConfiguration : IEntityTypeConfiguration<ExchangeReplacementItem>
{
    public void Configure(EntityTypeBuilder<ExchangeReplacementItem> builder)
    {
        builder.Property(i => i.UnitPrice)
            .HasPrecision(18, 2);

        builder.Property(i => i.LineTotal)
            .HasPrecision(18, 2);

        // Restrict: a product that has gone out on an exchange cannot be deleted.
        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.ProductId);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_ExchangeReplacementItems_Quantity", "\"Quantity\" > 0");
            t.HasCheckConstraint("CK_ExchangeReplacementItems_UnitPrice", "\"UnitPrice\" >= 0");
        });
    }
}
