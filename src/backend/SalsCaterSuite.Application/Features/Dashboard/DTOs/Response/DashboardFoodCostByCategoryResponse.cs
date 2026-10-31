using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Dashboard.DTOs.Response;

/// <summary>KPI-16 — the food-cost share of one course category.</summary>
public record DashboardFoodCostByCategoryResponse
{
    /// <summary>The course id; <c>null</c> for the uncategorised group.</summary>
    public required int? CourseId { get; init; }

    /// <summary>The course name, or <c>Uncategorised</c> for menus without a course.</summary>
    public required string CourseName { get; init; }

    /// <summary>Σ food cost of the costed menus in this category.</summary>
    public required decimal FoodCostAmount { get; init; }

    /// <summary>This category's share of the catalogue's total food cost.</summary>
    public required decimal SharePercentage { get; init; }

    /// <summary>The number of costed menus in this category.</summary>
    public required int MenuCount { get; init; }
}
