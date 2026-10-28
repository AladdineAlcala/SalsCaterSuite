using SalsCaterSuite.Domain.Common;

namespace SalsCaterSuite.Domain.Entities;

public class Package : BaseEntity
{
    public int PackageId { get; set; }

    /// <summary>
    /// The human-readable business identifier, <c>PKG-{TYPE}-{NN}</c> (the owner's 2026-10-24
    /// request). It is generated once, when the tier is created, and is <b>frozen</b>: editing
    /// the type or the description never renumbers a tier. <see cref="PackageId"/> remains the
    /// internal key the three foreign keys reference.
    /// </summary>
    public string PackageCode { get; set; } = string.Empty;

    public string PackageType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal? AmountPax { get; set; }
    public int? MinPax { get; set; }

    /// <summary>
    /// The tier's applicable-pax classification (REQ-PKG-04, DEC-173).
    /// <para>
    /// A <b>required</b> reference to the read-only <see cref="PackagePaxApplicable"/> lookup.
    /// It is descriptive classification metadata only: it does not establish a booking headcount
    /// limit, does not constrain <see cref="PackageType"/>, and has no effect on pricing.
    /// </para>
    /// </summary>
    public int NumPaxId { get; set; }

    /// <summary>
    /// The classification this tier points at. See <see cref="NumPaxId"/>.
    /// </summary>
    public PackagePaxApplicable? PaxApplicable { get; set; }

    /// <summary>
    /// The tier's own per-guest extended-location amount (the owner's 2026-10-24 request).
    /// <para>
    /// It is available <b>only</b> for the <c>Regular</c> and <c>Premier</c> tier types and is
    /// added to the municipality's per-guest rate when a booking applies the extended amount.
    /// <c>null</c> = not stated, which contributes zero. The type eligibility is enforced by
    /// the write validator and by the booking pricing path.
    /// </para>
    /// </summary>
    public decimal? ExtendedLocationAmount { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    /// <summary>
    /// The tier's course-coverage ruleset (REQ-PKG-02, M12, DEC-077).
    /// <para>
    /// The rules are configured FK-first and the collection exists so the Domain entity stays
    /// consistent with the rest of the model and with <c>architecture/domain-model.md</c> §2.4.
    /// </para>
    /// </summary>
    public ICollection<PackageCourseRule> CourseRules { get; set; } = new List<PackageCourseRule>();

    /// <summary>
    /// The tier's coverage matrix — the municipalities it may be served in (REQ-PKG-02, M12,
    /// DEC-077). See <see cref="PackageCourseRule"/> for the modelling note.
    /// </summary>
    public ICollection<PackageAreaCoverage> AreaCoverages { get; set; } = new List<PackageAreaCoverage>();
}

