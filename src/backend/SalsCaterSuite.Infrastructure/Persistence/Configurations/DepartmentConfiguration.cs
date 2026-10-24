using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Infrastructure.Persistence.Configurations;

/// <summary>
/// The mapping of the department master (the owner's 2026-10-24 request).
/// <para>
/// The table ships <b>empty</b> — no department is seeded (BR-13). Both <c>AddonDetail.deptId</c>
/// and the legacy <c>Menu.deptId</c> reference it, so a department is the one place a kitchen
/// section is named.
/// </para>
/// </summary>
public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Department");

        builder.HasKey(e => e.DeptId);
        builder.Property(e => e.DeptId).HasColumnName("deptId");

        builder.Property(e => e.DeptName)
            .HasColumnName("deptName")
            .HasMaxLength(150)
            .IsRequired();

        // DEC-038: BaseEntity audit columns (the documented generic pattern).
        builder.Property(e => e.CreatedDate).HasColumnName("createdDate");
        builder.Property(e => e.CreatedByUser).HasColumnName("createdByUser").HasMaxLength(100);
        builder.Property(e => e.UpdatedDate).HasColumnName("updatedDate");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
    }
}
