using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Dashboard.DTOs.Response;

/// <summary>KPI-15 — catalogue gross margin (a catalogue measure, not a banquet measure).</summary>
public record DashboardMarginResponse
{
    /// <summary>Σ price over the costed, priced menus.</summary>
    public required decimal CatalogueRevenueAmount { get; init; }

    /// <summary>Σ food cost over the same population.</summary>
    public required decimal CatalogueFoodCostAmount { get; init; }

    /// <summary>Catalogue revenue − catalogue food cost.</summary>
    public required decimal CatalogueMarginAmount { get; init; }

    /// <summary>Margin ÷ catalogue revenue × 100; <c>null</c> when the catalogue revenue is zero.</summary>
    public required decimal? CatalogueMarginPercentage { get; init; }

    /// <summary>Margin percentage − the configured target margin; <c>null</c> when the margin is <c>null</c>.</summary>
    public required decimal? VarianceToBenchmarkPercentage { get; init; }
}
