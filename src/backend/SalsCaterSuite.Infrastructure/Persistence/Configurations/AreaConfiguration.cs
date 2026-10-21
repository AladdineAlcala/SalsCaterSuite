using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Infrastructure.Persistence.Configurations;

/// <summary>
/// The mapping of the municipality catalogue (REQ-PKG-02, DEC-077, DEC-081).
/// <para>
/// The three columns added by the REQ-PKG-02 migration are <c>distanceKm</c>,
/// <c>logisticsNote</c> and <c>logisticsCost</c>. Every one of them is optional: a null value
/// means "not stated", and a null logistics cost <b>omits</b> the logistics band from the
/// gross-profit split rather than publishing zero (DEC-081).
/// </para>
/// <para>
/// The mapping was lifted out of <c>SalsCaterSuiteDbContext.OnModelCreating</c> verbatim, so the
/// model — and therefore the schema — is unchanged by the move.
/// </para>
/// </summary>
public class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> builder)
    {
        builder.ToTable("Area");

        builder.HasKey(e => e.AreaId);
        builder.Property(e => e.AreaId).HasColumnName("areaId");
        builder.Property(e => e.AreaDetails).HasColumnName("areaDetails").HasMaxLength(150);
        builder.Property(e => e.AdditionalRate).HasColumnName("additionalRate").HasPrecision(18, 2);

        // REQ-PKG-02 (DEC-077, DEC-081): the three optional municipality columns.
        builder.Property(e => e.DistanceKm).HasColumnName("distanceKm");
        builder.Property(e => e.LogisticsNote).HasColumnName("logisticsNote").HasMaxLength(150);
        builder.Property(e => e.LogisticsCost).HasColumnName("logisticsCost").HasPrecision(18, 2);

        // DEC-038: BaseEntity audit columns (the documented generic pattern).
        builder.Property(e => e.CreatedDate).HasColumnName("createdDate");
        builder.Property(e => e.CreatedByUser).HasColumnName("createdByUser").HasMaxLength(100);
        builder.Property(e => e.UpdatedDate).HasColumnName("updatedDate");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
    }
}
