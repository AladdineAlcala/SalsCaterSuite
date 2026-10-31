using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>
/// The proposed selection a quote describes (contract §14.3) — the same shape
/// <c>GET /api/bookings/{id}/menus</c> uses for a stored selection.
/// </summary>
public class BookingQuoteSelectionResponse
{
    public bool IsLocked { get; set; }

    /// <summary>Always a concrete date — never <c>null</c> (DEC-128).</summary>
    public DateOnly LockDate { get; set; }

    public List<BookingQuoteSelectionCourseResponse> Courses { get; set; } = new();

    /// <summary>The sum of the chosen dishes' food costs, or <c>null</c> when unknown.</summary>
    public decimal? SelectedFoodCostPerPax { get; set; }

    /// <summary>The selection's planning margin, or <c>null</c> when either input is unknown.</summary>
    public decimal? SelectedGrossProfitPercentage { get; set; }
}
