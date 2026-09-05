using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Optical.Domain.Entities;

namespace Optical.Infrastructure.Persistence.Configurations;

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.Property(p => p.Role)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.Permission)
            .IsRequired()
            .HasMaxLength(50);

        // A grant is either there or not; it can never be recorded twice.
        builder.HasIndex(p => new { p.Role, p.Permission })
            .IsUnique();
    }
}
