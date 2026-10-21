using SalsCaterSuite.Domain.Common;

namespace SalsCaterSuite.Domain.Entities;

/// <summary>
/// The link between a buffet tier and a municipality (REQ-PKG-02, DEC-077).
/// <para>
/// The row carries <b>no money</b>: the per-pax out-of-town surcharge is
/// <see cref="Area.AdditionalRate"/>, the municipality's own rate, so there is never a question
/// of which of two rate columns is authoritative (DEC-078). Editing a municipality's rate
/// therefore affects every tier that covers it, and a coverage row cannot override it.
/// </para>
/// <para>
/// A coverage removed from a submitted set is <b>soft-deleted</b>, never destroyed (DEC-085),
/// and a re-added municipality <b>resurrects</b> its soft-deleted row (DEC-088). The filtered
/// unique index on (<c>p_id</c>, <c>areaId</c>) is the database-level guarantee.
/// </para>
/// </summary>
public class PackageAreaCoverage : BaseEntity
{
    public int PackageAreaCoverageId { get; set; }

    /// <summary>FK → <see cref="Package"/>. Cascade delete.</summary>
    public int PackageId { get; set; }

    /// <summary>FK → <see cref="Area"/>. The covered municipality. Cascade delete.</summary>
    public int AreaId { get; set; }

    /// <summary>Whether the coverage is currently offered. Default <c>true</c>.</summary>
    public bool IsActive { get; set; } = true;
}
