using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Infrastructure.Persistence.Configurations;

/// <summary>
/// The mapping of the course-coverage ruleset table (REQ-PKG-02, DEC-077, DEC-088).
/// <para>
/// The column names are the data dictionary's: <c>pcr_id</c>, <c>p_id</c>, <c>courseId</c>,
/// <c>requiredQuota</c>, <c>portionGrams</c> and <c>ruleDescription</c>.
/// </para>
/// <para>
/// The retired <c>inclusionTag</c> and <c>addonId</c> columns (and the add-on foreign key and
/// index that hung off <c>addonId</c>) are gone as of 2026-10-27 (DEC-172). The rule no longer
/// references the add-on master.
/// </para>
/// <para>
/// The <b>filtered</b> unique index on (<c>p_id</c>, <c>courseId</c>) is the database-level
/// guarantee that a live ruleset holds at most one rule per course. The filter
/// (<c>is_deleted = 0</c>) is what makes the re-add path possible: a plain unique index would
/// refuse the resurrect (DEC-088).
/// </para>
/// </summary>
public class PackageCourseRuleConfiguration : IEntityTypeConfiguration<PackageCourseRule>
{
    public void Configure(EntityTypeBuilder<PackageCourseRule> builder)
    {
        builder.ToTable("PackageCourseRule");

        builder.HasKey(e => e.PackageCourseRuleId);
        builder.Property(e => e.PackageCourseRuleId).HasColumnName("pcr_id");

        builder.Property(e => e.PackageId).HasColumnName("p_id");
        builder.Property(e => e.CourseId).HasColumnName("courseId");
        builder.Property(e => e.RequiredQuota).HasColumnName("requiredQuota");
        builder.Property(e => e.PortionGrams).HasColumnName("portionGrams");
        builder.Property(e => e.RuleDescription).HasColumnName("ruleDescription").HasMaxLength(500);

        // DEC-038: BaseEntity audit columns (the documented generic pattern).
        builder.Property(e => e.CreatedDate).HasColumnName("createdDate");
        builder.Property(e => e.CreatedByUser).HasColumnName("createdByUser").HasMaxLength(100);
        builder.Property(e => e.UpdatedDate).HasColumnName("updatedDate");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasOne<Package>()
            .WithMany(p => p.CourseRules)
            .HasForeignKey(e => e.PackageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<CourseCategory>()
            .WithMany()
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        // DEC-088: one LIVE rule per (package, course). The filtered predicate keeps the
        // resurrect path open.
        builder.HasIndex(e => new { e.PackageId, e.CourseId })
            .IsUnique()
            .HasFilter("[is_deleted] = 0")
            .HasDatabaseName("UX_PackageCourseRule_Package_Course");
    }
}
