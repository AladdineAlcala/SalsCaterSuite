namespace SalsCaterSuite.Application.Features.Packages.DTOs.Request;

/// <summary>One submitted coverage (contract §13.5).</summary>
public record PackageAreaCoverageEntryRequest(
    int AreaId = 0,
    bool IsActive = true);
