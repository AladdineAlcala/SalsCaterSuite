namespace SalsCaterSuite.Application.Features.Packages.DTOs.Response;

/// <summary>One selected dish of the preview (contract §13.3).</summary>
public class PackageCourseSelectionResultResponse
{
    public int CourseId { get; set; }

    public string CourseName { get; set; } = string.Empty;

    public string MenuId { get; set; } = string.Empty;

    public string MenuName { get; set; } = string.Empty;

    /// <summary>The dish's food cost. <c>null</c> when the dish carries no food cost.</summary>
    public decimal? FoodCost { get; set; }
}
