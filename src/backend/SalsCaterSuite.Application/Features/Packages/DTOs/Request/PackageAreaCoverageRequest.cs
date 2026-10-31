namespace SalsCaterSuite.Application.Features.Packages.DTOs.Request;

/// <summary>
/// The body of <c>PUT /api/packages/{id}/areas</c> (contract §13.5).
/// <para>
/// The <c>coverages</c> member is <b>nullable</b> on purpose: an absent or explicitly null member
/// is a rejected payload (<c>400</c>), never an empty set (DEC-085).
/// </para>
/// </summary>
public record PackageAreaCoverageRequest(
    IReadOnlyList<PackageAreaCoverageEntryRequest>? Coverages = null);
