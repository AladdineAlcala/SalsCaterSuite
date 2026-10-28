namespace SalsCaterSuite.Application.Features.Packages.DTOs;

public class PackageDto
{
    public int PackageId { get; set; }

    /// <summary>The human-readable code, <c>PKG-{TYPE}-{NN}</c> (the owner's 2026-10-24 request).</summary>
    public string PackageCode { get; set; } = string.Empty;

    public string PackageType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal? AmountPax { get; set; }
    public int? MinPax { get; set; }

    /// <summary>
    /// The tier's applicable-pax classification (REQ-PKG-04, DEC-173).
    /// <para>
    /// It is a required reference to the read-only lookup and is informational: it does not
    /// define booking headcount bounds.
    /// </para>
    /// </summary>
    public int NumPaxId { get; set; }

    /// <summary>
    /// The tier's own per-guest extended-location amount. Available only for <c>Regular</c> and
    /// <c>Premier</c>; <c>null</c> = not stated.
    /// </summary>
    public decimal? ExtendedLocationAmount { get; set; }

    public bool IsActive { get; set; }
}

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
public record CreatePackageRequestDto(
    string PackageType,
    string Description,
    decimal? AmountPax,
    int? MinPax,
    int NumPaxId,
    decimal? ExtendedLocationAmount = null);

