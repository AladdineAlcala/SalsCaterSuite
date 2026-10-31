using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>One course of the proposed selection (contract §14.3).</summary>
public class BookingQuoteSelectionCourseResponse
{
    public int CourseId { get; set; }
    public string? CourseName { get; set; }
    public List<BookingQuoteSelectedMenuResponse> SelectedMenus { get; set; } = new();
}
