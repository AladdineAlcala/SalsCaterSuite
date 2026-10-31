namespace SalsCaterSuite.Application.Features.Packages.DTOs.Request;

/// <summary>The body of <c>POST /api/packages/{id}/areas</c> (contract §13.6).</summary>
public record PackageAreaCoverageAttachRequest(
    int AreaId = 0,
    bool IsActive = true);
