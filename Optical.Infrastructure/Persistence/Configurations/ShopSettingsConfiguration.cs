using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Optical.Domain.Entities;

namespace Optical.Infrastructure.Persistence.Configurations;

public class ShopSettingsConfiguration : IEntityTypeConfiguration<ShopSettings>
{
    public void Configure(EntityTypeBuilder<ShopSettings> builder)
    {
        // The key is assigned by the application, never by the database,
        // so the single-row rule can be enforced.
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.BusinessName)
            .IsRequired()
            .HasMaxLength(150);

        // Optional: a shop that has not filled it in simply prints no phone line.
        builder.Property(s => s.Phone)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(s => s.Address)
            .IsRequired()
            .HasMaxLength(300);

        builder.ToTable(t => t.HasCheckConstraint("CK_ShopSettings_SingleRow", "\"Id\" = 1"));
    }
}
