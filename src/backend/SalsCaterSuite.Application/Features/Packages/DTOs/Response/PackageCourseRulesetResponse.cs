namespace SalsCaterSuite.Application.Features.Packages.DTOs.Response;

/// <summary>
/// The tier's course-coverage ruleset and its planning figures (contract §13.1, REQ-PKG-02).
/// <para>
/// Every figure on this payload is computed on the server (DEC-002). <c>null</c> means
/// <b>unknown</b>, never zero (contract §9.2 rule 6).
/// </para>
/// </summary>
public class PackageCourseRulesetResponse
{
    public int PackageId { get; set; }

    /// <summary><c>Package.Description</c>.</summary>
    public string PackageName { get; set; } = string.Empty;

    /// <summary>The human-readable code (the owner's 2026-10-24 request).</summary>
    public string PackageCode { get; set; } = string.Empty;

    /// <summary><c>Package.PackageType</c>, verbatim (DEC-070).</summary>
    public string PackageType { get; set; } = string.Empty;

    /// <summary>The tier's per-guest extended-location amount. <c>null</c> = not stated.</summary>
    public decimal? ExtendedLocationAmount { get; set; }

    /// <summary>The per-guest rate. <c>null</c> = not priced.</summary>
    public decimal? AmountPax { get; set; }

    /// <summary>The headcount floor. <c>null</c> = not set.</summary>
    public int? MinPax { get; set; }

    public bool IsActive { get; set; }

    /// <summary>The deployment-configured food-cost benchmark (DEC-040).</summary>
    public decimal TargetFoodCostPercentage { get; set; }

    /// <summary><c>100 − TargetFoodCostPercentage</c>.</summary>
    public decimal TargetMarginPercentage { get; set; }

    /// <summary><c>false</c> when the configuration provider is absent.</summary>
    public bool IsBenchmarkConfigured { get; set; }

    /// <summary><c>AmountPax × TargetFoodCostPercentage ÷ 100</c>. <c>null</c> when the tier carries no rate.</summary>
    public decimal? CostCeilingPerPax { get; set; }

    /// <summary><c>AmountPax × MinPax</c> — the derived floor on the contract (DEC-079).</summary>
    public decimal? MinimumContractValue { get; set; }

    /// <summary>
    /// The planning estimate: the mean food cost of each rule's qualified <b>costed</b> pool,
    /// summed. The pool means are computed at full precision and the sum is rounded <b>once</b>
    /// (DEC-083). <c>null</c> when there is no rule, or when no rule's pool holds a costed dish.
    /// </summary>
    public decimal? StandardRunFoodCostPerPax { get; set; }

    /// <summary><c>StandardRunFoodCostPerPax ÷ AmountPax × 100</c>.</summary>
    public decimal? StandardRunFoodCostPercentage { get; set; }

    /// <summary><c>(AmountPax − StandardRunFoodCostPerPax) ÷ AmountPax × 100</c>.</summary>
    public decimal? BaseGrossProfitPercentage { get; set; }

    /// <summary>The tier's rules, ordered by course name. Soft-deleted rules are excluded.</summary>
    public List<PackageCourseRuleResponse> Rules { get; set; } = new();

    /// <summary>The server UTC timestamp of the payload.</summary>
    public DateTime GeneratedAt { get; set; }
}
