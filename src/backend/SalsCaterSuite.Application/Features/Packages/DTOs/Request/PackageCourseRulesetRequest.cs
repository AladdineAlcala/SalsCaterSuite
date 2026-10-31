namespace SalsCaterSuite.Application.Features.Packages.DTOs.Request;

/// <summary>
/// The body of <c>PUT /api/packages/{id}/course-rules</c> (contract §13.2).
/// <para>
/// The <c>rules</c> member is <b>nullable</b> on purpose: an absent or explicitly null member is
/// a rejected payload (<c>400</c>), never an empty set — an empty set has the documented,
/// destructive meaning of clearing the tier's ruleset (DEC-085).
/// </para>
/// </summary>
public record PackageCourseRulesetRequest(
    IReadOnlyList<PackageCourseRuleRequest>? Rules = null);
