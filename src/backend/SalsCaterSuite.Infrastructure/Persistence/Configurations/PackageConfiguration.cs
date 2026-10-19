using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Infrastructure.Persistence.Configurations;

public class PackageConfiguration : IEntityTypeConfiguration<Package>
{
    public void Configure(EntityTypeBuilder<Package> builder)
    {
        builder.ToTable("Package");

        builder.HasKey(e => e.PackageId);
        builder.Property(e => e.PackageId).HasColumnName("p_id");

        builder.Property(e => e.PackageType).HasColumnName("p_type").HasMaxLength(50);
        builder.Property(e => e.Description).HasColumnName("p_descripton").HasMaxLength(250);
        builder.Property(e => e.AmountPax).HasColumnName("p_amountPax").HasPrecision(18, 2);
        builder.Property(e => e.MinPax).HasColumnName("p_min");
        builder.Property(e => e.IsActive).HasColumnName("isActive");
        // DEC-038: all four BaseEntity audit columns are persisted. Package keeps its
        // existing date_Created/date_Updated convention; the two columns that had no
        // equivalent use the documented generic pattern.
        builder.Property(e => e.CreatedDate).HasColumnName("date_Created");
        builder.Property(e => e.CreatedByUser).HasColumnName("createdByUser").HasMaxLength(100);
        builder.Property(e => e.UpdatedDate).HasColumnName("date_Updated");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
    }
}

