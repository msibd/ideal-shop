using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Optical.Domain.Entities;

namespace Optical.Infrastructure.Persistence.Configurations;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.Property(s => s.InvoiceNumber)
            .IsRequired()
            .HasMaxLength(30);

        // The last line of defence against two tills claiming the same number.
        builder.HasIndex(s => s.InvoiceNumber)
            .IsUnique();

        builder.Property(s => s.SubTotal)
            .HasPrecision(18, 2);

        builder.Property(s => s.DiscountAmount)
            .HasPrecision(18, 2);

        builder.Property(s => s.TotalAmount)
            .HasPrecision(18, 2);

        builder.Property(s => s.Status)
            .HasConversion<int>();

        // Every sales figure filters on status first and then on a date window, so the
        // composite in that order is what the dashboard and reports actually seek on.
        builder.HasIndex(s => new { s.Status, s.CreatedAt });

        // Restrict: a customer with sale history cannot be deleted.
        builder.HasOne(s => s.Customer)
            .WithMany()
            .HasForeignKey(s => s.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Cascade: the items only exist as part of their sale.
        builder.HasMany(s => s.Items)
            .WithOne(i => i.Sale)
            .HasForeignKey(i => i.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Payment)
            .WithOne(p => p.Sale)
            .HasForeignKey<Payment>(p => p.SaleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Sales_TotalAmount", "\"TotalAmount\" >= 0");

            // A discount can never exceed what was being charged, so the total can never
            // go negative however the lines were priced. Written as a subtraction rather than
            // a comparison of the two columns: SQLite, which the tests run on, keeps decimals
            // as text and would compare them as strings.
            t.HasCheckConstraint(
                "CK_Sales_DiscountAmount",
                "\"DiscountAmount\" >= 0 AND \"SubTotal\" - \"DiscountAmount\" >= 0");
        });
    }
}
