using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Dashboard.DTOs.Response;

/// <summary>KPI-14 — dish-costing coverage of the menu catalogue.</summary>
public record DashboardDishCostingResponse
{
    /// <summary>Number of non-deleted menus.</summary>
    public required int TotalMenuCount { get; init; }

    /// <summary>Of those, the menus whose food cost is known and greater than zero.</summary>
    public required int CostedMenuCount { get; init; }

    /// <summary><c>TotalMenuCount − CostedMenuCount</c>.</summary>
    public required int UncostedMenuCount { get; init; }

    /// <summary>Costed ÷ total × 100; <c>0</c> when the catalogue is empty.</summary>
    public required decimal CoveragePercentage { get; init; }
}
