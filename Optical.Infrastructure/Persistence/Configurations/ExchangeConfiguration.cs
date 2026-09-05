using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Optical.Domain.Entities;

namespace Optical.Infrastructure.Persistence.Configurations;

public class ExchangeConfiguration : IEntityTypeConfiguration<Exchange>
{
    public void Configure(EntityTypeBuilder<Exchange> builder)
    {
        builder.Property(e => e.ExchangeNumber)
            .IsRequired()
            .HasMaxLength(30);

        // The last line of defence against two tills claiming the same number.
        builder.HasIndex(e => e.ExchangeNumber)
            .IsUnique();

        builder.Property(e => e.Reason)
            .HasConversion<int>();

        builder.Property(e => e.Note)
            .HasMaxLength(300);

        // Worth indexing: the shop looks at exchanges by reason to spot a recurring problem.
        builder.HasIndex(e => e.Reason);

        builder.Property(e => e.ReturnedAmount)
            .HasPrecision(18, 2);

        builder.Property(e => e.ReplacementAmount)
            .HasPrecision(18, 2);

        builder.Property(e => e.AmountPaid)
            .HasPrecision(18, 2);

        builder.Property(e => e.PaymentMethod)
            .HasConversion<int>();

        // Exchanges are always read for a date window, the same way sales are.
        builder.HasIndex(e => e.CreatedAt);

        // Restrict: a sale that has been exchanged against cannot be deleted.
        builder.HasOne(e => e.Sale)
            .WithMany()
            .HasForeignKey(e => e.SaleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Cascade: the lines only exist as part of their exchange.
        builder.HasMany(e => e.ReturnedItems)
            .WithOne(i => i.Exchange)
            .HasForeignKey(i => i.ExchangeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.ReplacementItems)
            .WithOne(i => i.Exchange)
            .HasForeignKey(i => i.ExchangeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Exchanges_ReturnedAmount", "\"ReturnedAmount\" >= 0");
            t.HasCheckConstraint("CK_Exchanges_ReplacementAmount", "\"ReplacementAmount\" >= 0");

            // The shop never pays out on an exchange, whatever the code does.
            t.HasCheckConstraint("CK_Exchanges_AmountPaid", "\"AmountPaid\" >= 0");
            t.HasCheckConstraint(
                "CK_Exchanges_NoRefund",
                "\"ReplacementAmount\" >= \"ReturnedAmount\"");
        });
    }
}
