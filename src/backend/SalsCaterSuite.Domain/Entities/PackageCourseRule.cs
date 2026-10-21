using SalsCaterSuite.Domain.Common;

namespace SalsCaterSuite.Domain.Entities;

/// <summary>
/// The enforced course-category rule a buffet tier holds (REQ-PKG-02, DEC-077).
/// <para>
/// A rule is one row per course: the number of dishes the client must take from that course,
/// the standard portion, the rule the client is held to and the allowed tier-1 upgrade.
/// </para>
/// <para>
/// The rule's <b>qualified pool</b> is not stored. It is derived on every read from the live
/// dishes of the course, so a stored pool could never go stale (DEC-083, domain model §2.4.1).
/// </para>
/// <para>
/// A rule removed from a submitted ruleset is <b>soft-deleted</b>, never destroyed (DEC-085),
/// and a re-added course <b>resurrects</b> its soft-deleted row instead of inserting a second one
/// (DEC-088). The filtered unique index on (<c>p_id</c>, <c>courseId</c>) is the database-level
/// guarantee.
/// </para>
/// </summary>
public class PackageCourseRule : BaseEntity
{
    public int PackageCourseRuleId { get; set; }

    /// <summary>FK → <see cref="Package"/>. Cascade delete.</summary>
    public int PackageId { get; set; }

    /// <summary>FK → <see cref="CourseCategory"/>. The course the rule governs. Restrict delete.</summary>
    public int CourseId { get; set; }

    /// <summary>≥ 1. The number of dishes the client must take from this course.</summary>
    public int RequiredQuota { get; set; }

    /// <summary>≥ 0. The standard portion per guest, in grams. <c>null</c> = not specified.</summary>
    public int? PortionGrams { get; set; }

    /// <summary>The rule the client is held to, in plain words. ≤ 500 characters.</summary>
    public string RuleDescription { get; set; } = string.Empty;

    /// <summary>≤ 50. The operator's label for the category's inclusion status. <c>null</c> = no tag.</summary>
    public string? InclusionTag { get; set; }

    /// <summary>FK → <see cref="AddonDetail"/>. The allowed tier-1 upgrade. <c>null</c> = no upgrade.</summary>
    public int? UpgradeAddonId { get; set; }
}
