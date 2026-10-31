namespace SalsCaterSuite.Application.Features.Packages.DTOs.Request;

/// <summary>
/// The body of <c>POST /api/packages/{id}/course-rules/preview</c> (contract §13.3).
/// <para>
/// The <c>selections</c> member is <b>nullable</b> on purpose: an absent or explicitly null
/// member is a rejected payload (<c>400</c>), never an empty selection.
/// </para>
/// </summary>
public record PackageCourseRulePreviewRequest(
    IReadOnlyList<PackageCourseSelectionRequest>? Selections = null,
    decimal? StressPercentage = null,
    int? Pax = null);
