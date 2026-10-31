namespace SalsCaterSuite.Application.Features.Bookings.DTOs;

/// <summary>
/// Request DTO for updating core booking information (event date and headcount).
/// </summary>
public class UpdateBookingCoreRequestDto
{
    /// <summary>The new event date.</summary>
    public DateOnly EventDate { get; set; }

    /// <summary>The new number of guests.</summary>
    public int NumberOfHeads { get; set; }
}
