using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.Infrastructure.Persistence.Configurations;

/// <summary>
/// The mapping of the read-only applicable-pax lookup (REQ-PKG-04, DEC-173).
/// <para>
/// The table carries <b>fixed reference data</b>: the five rows are seeded by the schema
/// migration with their stated identifiers and labels, and there is no administration surface.
/// The primary key is therefore <b>not</b> database-generated — the identifier is part of the
/// reference data itself, and a generated identity would break the fixed five-row contract.
/// </para>
/// <para>
/// The table follows the project's <c>BaseEntity</c>/DEC-038 audit-column convention, so the
/// seeded rows carry the standard audit columns alongside the two reference columns.
/// </para>
/// </summary>
public class PackagePaxApplicableConfiguration : IEntityTypeConfiguration<PackagePaxApplicable>
{
    public void Configure(EntityTypeBuilder<PackagePaxApplicable> builder)
    {
        builder.ToTable("PackagePaxApplicable");

        builder.HasKey(e => e.NumPaxId);

        // The five identifiers (1..5) are fixed reference data, not a generated sequence.
        builder.Property(e => e.NumPaxId)
            .HasColumnName("numPaxId")
            .ValueGeneratedNever();

        builder.Property(e => e.PackageApplicablePax)
            .HasColumnName("packageApplicablePax")
            .HasMaxLength(100)
            .IsRequired();

        // DEC-038: BaseEntity audit columns (the documented generic pattern).
        builder.Property(e => e.CreatedDate).HasColumnName("createdDate");
        builder.Property(e => e.CreatedByUser).HasColumnName("createdByUser").HasMaxLength(100);
        builder.Property(e => e.UpdatedDate).HasColumnName("updatedDate");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        // The five fixed classification rows (DEC-173). They are EF Core's *model managed data*:
        // the identifiers and the labels are fixed reference data, so the migration scaffolding
        // turns them into InsertData/DeleteData operations and the model snapshot stays in step
        // with them.
        //
        // The audit values are truthful and deterministic:
        //   createdDate   - the fixed UTC moment this reference data was authored. It is a
        //                   literal rather than a clock function, because model managed data
        //                   must be deterministic: a non-deterministic value would make the
        //                   snapshot unstable and report perpetual pending model changes.
        //   createdByUser - NULL. The rows were inserted by a schema migration and have no human
        //                   author; no creator identity is fabricated (BR-21).
        //   updatedDate   - NULL. The rows have never been updated.
        //   is_deleted    - false. The reference rows are live.
        builder.HasData(
            new PackagePaxApplicable
            {
                NumPaxId = 1,
                PackageApplicablePax = "15 to 19 Pax",
                CreatedDate = new DateTime(2026, 10, 28, 12, 13, 57, 440, DateTimeKind.Utc),
                CreatedByUser = null,
                UpdatedDate = null,
                IsDeleted = false
            },
            new PackagePaxApplicable
            {
                NumPaxId = 2,
                PackageApplicablePax = "20 to 29 Pax",
                CreatedDate = new DateTime(2026, 10, 28, 12, 13, 57, 440, DateTimeKind.Utc),
                CreatedByUser = null,
                UpdatedDate = null,
                IsDeleted = false
            },
            new PackagePaxApplicable
            {
                NumPaxId = 3,
                PackageApplicablePax = "30 to 49 Pax",
                CreatedDate = new DateTime(2026, 10, 28, 12, 13, 57, 440, DateTimeKind.Utc),
                CreatedByUser = null,
                UpdatedDate = null,
                IsDeleted = false
            },
            new PackagePaxApplicable
            {
                NumPaxId = 4,
                PackageApplicablePax = "80 to 100 Above",
                CreatedDate = new DateTime(2026, 10, 28, 12, 13, 57, 440, DateTimeKind.Utc),
                CreatedByUser = null,
                UpdatedDate = null,
                IsDeleted = false
            },
            new PackagePaxApplicable
            {
                NumPaxId = 5,
                PackageApplicablePax = "Not Applicable to PackMeals",
                CreatedDate = new DateTime(2026, 10, 28, 12, 13, 57, 440, DateTimeKind.Utc),
                CreatedByUser = null,
                UpdatedDate = null,
                IsDeleted = false
            });
    }
}
