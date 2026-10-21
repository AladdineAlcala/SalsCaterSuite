using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Infrastructure.Persistence.Configurations;

/// <summary>
/// The mapping of the package-to-municipality coverage table (REQ-PKG-02, DEC-077, DEC-088).
/// <para>
/// The column names are the data dictionary's: <c>pac_id</c>, <c>p_id</c>, <c>areaId</c> and
/// <c>isActive</c>. The table carries <b>no money column</b> — the surcharge is the
/// municipality's own rate (DEC-078).
/// </para>
/// <para>
/// The <b>filtered</b> unique index on (<c>p_id</c>, <c>areaId</c>) is the database-level
/// guarantee that a live coverage set holds at most one row per municipality. The filter
/// (<c>is_deleted = 0</c>) is what makes the re-add path possible (DEC-088).
/// </para>
/// </summary>
public class PackageAreaCoverageConfiguration : IEntityTypeConfiguration<PackageAreaCoverage>
{
    public void Configure(EntityTypeBuilder<PackageAreaCoverage> builder)
    {
        builder.ToTable("PackageAreaCoverage");

        builder.HasKey(e => e.PackageAreaCoverageId);
        builder.Property(e => e.PackageAreaCoverageId).HasColumnName("pac_id");

        builder.Property(e => e.PackageId).HasColumnName("p_id");
        builder.Property(e => e.AreaId).HasColumnName("areaId");
        builder.Property(e => e.IsActive).HasColumnName("isActive").HasDefaultValue(true);

        // DEC-038: BaseEntity audit columns (the documented generic pattern).
        builder.Property(e => e.CreatedDate).HasColumnName("createdDate");
        builder.Property(e => e.CreatedByUser).HasColumnName("createdByUser").HasMaxLength(100);
        builder.Property(e => e.UpdatedDate).HasColumnName("updatedDate");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasOne<Package>()
            .WithMany(p => p.AreaCoverages)
            .HasForeignKey(e => e.PackageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Area>()
            .WithMany()
            .HasForeignKey(e => e.AreaId)
            .OnDelete(DeleteBehavior.Cascade);

        // DEC-088: one LIVE coverage row per (package, municipality). The filtered predicate
        // keeps the resurrect path open.
        builder.HasIndex(e => new { e.PackageId, e.AreaId })
            .IsUnique()
            .HasFilter("[is_deleted] = 0")
            .HasDatabaseName("UX_PackageAreaCoverage_Package_Area");
    }
}
