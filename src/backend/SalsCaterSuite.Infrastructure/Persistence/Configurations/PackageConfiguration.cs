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
        builder.Property(e => e.CreatedDate).HasColumnName("date_Created");
        builder.Property(e => e.UpdatedDate).HasColumnName("date_Updated");

        builder.Ignore(e => e.CreatedByUser);
        builder.Ignore(e => e.IsDeleted);
    }
}

