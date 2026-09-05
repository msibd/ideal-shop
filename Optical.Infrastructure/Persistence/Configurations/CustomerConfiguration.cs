using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Optical.Domain.Entities;

namespace Optical.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(c => c.Phone)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(c => c.Email)
            .HasMaxLength(150);

        builder.Property(c => c.Address)
            .HasMaxLength(250);

        builder.Property(c => c.Notes)
            .HasMaxLength(500);

        // Phone is how the shop identifies a walk-in customer, so it must be unique.
        builder.HasIndex(c => c.Phone).IsUnique();

        builder.HasIndex(c => c.Name);

        // The dashboard counts customers created inside a period.
        builder.HasIndex(c => c.CreatedAt);
    }
}
