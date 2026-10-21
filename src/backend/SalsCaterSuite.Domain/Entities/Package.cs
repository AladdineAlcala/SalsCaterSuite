using SalsCaterSuite.Domain.Common;

namespace SalsCaterSuite.Domain.Entities;

public class Package : BaseEntity
{
    public int PackageId { get; set; }
    public string PackageType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal? AmountPax { get; set; }
    public int? MinPax { get; set; }
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

