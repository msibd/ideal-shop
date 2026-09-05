using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Optical.Domain.Entities;

namespace Optical.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.Property(p => p.Amount)
            .HasPrecision(18, 2);

        builder.Property(p => p.Method)
            .HasConversion<int>();

        // One payment per sale.
        builder.HasIndex(p => p.SaleId)
            .IsUnique();

        builder.ToTable(t =>
            t.HasCheckConstraint("CK_Payments_Amount", "\"Amount\" >= 0"));
    }
}
