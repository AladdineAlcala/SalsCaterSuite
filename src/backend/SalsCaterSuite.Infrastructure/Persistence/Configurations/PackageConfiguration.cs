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

        // The owner's 2026-10-24 request: the human-readable code is generated once at create
        // and frozen. It is unique among the live tiers only, so a soft-deleted tier never
        // blocks a future code that the generator would not reuse anyway.
        builder.Property(e => e.PackageCode).HasColumnName("p_code").HasMaxLength(30).IsRequired();
        builder.HasIndex(e => e.PackageCode)
            .IsUnique()
            .HasFilter("[is_deleted] = 0")
            .HasDatabaseName("IX_Package_p_code");

        builder.Property(e => e.PackageType).HasColumnName("p_type").HasMaxLength(50);
        builder.Property(e => e.Description).HasColumnName("p_descripton").HasMaxLength(250);
        builder.Property(e => e.AmountPax).HasColumnName("p_amountPax").HasPrecision(18, 2);
        builder.Property(e => e.MinPax).HasColumnName("p_min");

        // REQ-PKG-04 / DEC-173: the required applicable-pax classification. It is a descriptive
        // classification only — it establishes no booking headcount bound and constrains no
        // package type. The lookup row is read-only, so a tier can never delete its own
        // classification out from under it: the delete behaviour is Restrict.
        builder.Property(e => e.NumPaxId).HasColumnName("numPaxId").IsRequired();
        builder.HasOne(e => e.PaxApplicable)
            .WithMany()
            .HasForeignKey(e => e.NumPaxId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.NumPaxId).HasDatabaseName("IX_Package_numPaxId");

        builder.Property(e => e.ExtendedLocationAmount)
            .HasColumnName("p_extendedLocationAmount")
            .HasPrecision(18, 2);
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

