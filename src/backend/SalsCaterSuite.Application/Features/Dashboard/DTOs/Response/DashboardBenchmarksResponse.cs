using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Dashboard.DTOs.Response;

/// <summary>The margin benchmark that travels with the payload (contract §9.2 rule 7).</summary>
public record DashboardBenchmarksResponse
{
    /// <summary>The configured target food-cost percentage (default 35 %).</summary>
    public required decimal TargetFoodCostPercentage { get; init; }

    /// <summary><c>100 − TargetFoodCostPercentage</c>.</summary>
    public required decimal TargetMarginPercentage { get; init; }

    /// <summary><c>false</c> when the configuration key was absent and the default was substituted.</summary>
    public required bool IsConfigured { get; init; }
}
