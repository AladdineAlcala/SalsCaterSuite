namespace SalsCaterSuite.Application.Features.Packages.DTOs;

public class PackageDto
{
    public int PackageId { get; set; }
    public string PackageType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal? AmountPax { get; set; }
    public int? MinPax { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>Request body for creating or updating a package.</summary>
public record CreatePackageRequestDto(
    string PackageType,
    string Description,
    decimal? AmountPax,
    int? MinPax);

