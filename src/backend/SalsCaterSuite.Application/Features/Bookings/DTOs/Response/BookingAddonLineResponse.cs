using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>One frozen add-on line (contract §16.3).</summary>
public class BookingAddonLineResponse
{
    public int AddonId { get; set; }

    /// <summary>The frozen description (BR-04).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>The frozen line total — what joins the gross (DEC-158).</summary>
    public decimal Amount { get; set; }

    /// <summary>The frozen unit of measure — descriptive only.</summary>
    public string? Unit { get; set; }

    /// <summary>The frozen pricing basis (DEC-158).</summary>
    public AddonPricingBasis PricingBasis { get; set; }

    public int? AddonCategoryId { get; set; }
    public string? AddonCategoryName { get; set; }

    /// <summary>The catalogue's current amount; <c>null</c> when the catalogue row is gone.</summary>
    public decimal? CatalogueAmount { get; set; }

    /// <summary>The catalogue's current description; <c>null</c> when the row is gone.</summary>
    public string? CatalogueDescription { get; set; }
}
