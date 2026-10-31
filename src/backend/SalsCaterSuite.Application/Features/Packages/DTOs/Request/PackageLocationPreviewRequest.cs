namespace SalsCaterSuite.Application.Features.Packages.DTOs.Request;

/// <summary>
/// The body of <c>POST /api/packages/{id}/areas/preview</c> (contract §13.7).
/// <para>
/// The <c>selections</c> member is <b>nullable</b>: an absent or explicitly null member is a
/// rejected payload (<c>400</c>); an <b>empty</b> array is a valid "no selection" request.
/// </para>
/// </summary>
public record PackageLocationPreviewRequest(
    int Pax = 0,
    int? AreaId = null,
    IReadOnlyList<PackageCourseSelectionRequest>? Selections = null);
