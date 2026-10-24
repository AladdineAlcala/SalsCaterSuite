using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Infrastructure.Persistence.Configurations;

/// <summary>
/// The mapping of the add-on master's category level (REQ-BKG-07, DEC-142).
/// <para>
/// The column names are the data dictionary's: <c>addonCategoryId</c> and
/// <c>addonCategoryName</c>. The table ships <b>empty</b> — no row is seeded (BR-13), so the
/// add-on picker is empty until the catalogue is populated (RSK-066, M12).
/// </para>
/// </summary>
public class AddonCategoryConfiguration : IEntityTypeConfiguration<AddonCategory>
{
    public void Configure(EntityTypeBuilder<AddonCategory> builder)
    {
        builder.ToTable("AddonCategory");

        builder.HasKey(e => e.AddonCategoryId);
        builder.Property(e => e.AddonCategoryId).HasColumnName("addonCategoryId");

        builder.Property(e => e.AddonCategoryName)
            .HasColumnName("addonCategoryName")
            .HasMaxLength(150)
            .IsRequired();

        // DEC-038: BaseEntity audit columns (the documented generic pattern).
        builder.Property(e => e.CreatedDate).HasColumnName("createdDate");
        builder.Property(e => e.CreatedByUser).HasColumnName("createdByUser").HasMaxLength(100);
        builder.Property(e => e.UpdatedDate).HasColumnName("updatedDate");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
    }
}
