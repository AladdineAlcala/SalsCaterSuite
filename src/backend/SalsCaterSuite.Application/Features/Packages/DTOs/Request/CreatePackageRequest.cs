namespace SalsCaterSuite.Application.Features.Packages.DTOs.Request;

/// <summary>Request body for creating or updating a package.</summary>
/// <param name="PackageType">The tier's type. Free text (DEC-070).</param>
/// <param name="Description">The tier's display name.</param>
/// <param name="AmountPax">The per-guest rate. <c>null</c> = not priced.</param>
/// <param name="MinPax">The minimum headcount. <c>null</c> = not set.</param>
/// <param name="NumPaxId">
/// The required applicable-pax classification identifier (REQ-PKG-04, DEC-173). It must
/// reference a seeded <c>PackagePaxApplicable</c> row.
/// </param>
/// <param name="ExtendedLocationAmount">
/// The tier's own per-guest extended-location amount. <c>null</c> = not stated.
/// </param>
public record CreatePackageRequest(
    string PackageType,
    string Description,
    decimal? AmountPax,
    int? MinPax,
    int NumPaxId,
    decimal? ExtendedLocationAmount = null);
