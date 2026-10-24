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
    /// The tier's own per-guest extended-location amount. Available only for <c>Regular</c> and
    /// <c>Premier</c>; <c>null</c> = not stated.
    /// </summary>
    public decimal? ExtendedLocationAmount { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>Request body for creating or updating a package.</summary>
public record CreatePackageRequestDto(
    string PackageType,
    string Description,
    decimal? AmountPax,
    int? MinPax,
    decimal? ExtendedLocationAmount = null);

