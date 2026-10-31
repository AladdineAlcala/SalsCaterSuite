using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>One course rule with its qualified pool (contract §15.1, section 13.1's shape).</summary>
public class BookingMenuCourseResponse
{
    public int CourseId { get; set; }
    public string? CourseName { get; set; }
    public int RequiredQuota { get; set; }
    public int? PortionGrams { get; set; }

    /// <summary><c>PortionGrams x NumberOfHeads / 1000</c> (DEC-145); <c>null</c> when no portion.</summary>
    public decimal? BatchMassKg { get; set; }

    public string? RuleDescription { get; set; }
    public List<BookingMenuCourseDishResponse> Pool { get; set; } = new();
    public List<string> ChosenMenuIds { get; set; } = new();
}
