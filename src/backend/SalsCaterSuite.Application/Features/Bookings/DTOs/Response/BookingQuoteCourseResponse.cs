using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>
/// One course rule of the tier as the quote publishes it (contract §14.3). Unlike the selection
/// read's <see cref="BookingMenuCourseResponse"/> it carries the pool's <b>count</b>, not the pool.
/// </summary>
public class BookingQuoteCourseResponse
{
    public int CourseId { get; set; }
    public string? CourseName { get; set; }
    public int RequiredQuota { get; set; }
    public int? PortionGrams { get; set; }

    /// <summary><c>PortionGrams x NumberOfHeads / 1000</c> (DEC-145); <c>null</c> when no portion.</summary>
    public decimal? BatchMassKg { get; set; }

    public string? RuleDescription { get; set; }

    /// <summary>The number of live dishes in the rule's qualified pool.</summary>
    public int QualifiedPool { get; set; }
}
