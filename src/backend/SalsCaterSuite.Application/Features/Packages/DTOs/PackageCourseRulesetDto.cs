namespace SalsCaterSuite.Application.Features.Packages.DTOs;

/// <summary>
/// The tier's course-coverage ruleset and its planning figures (contract §13.1, REQ-PKG-02).
/// <para>
/// Every figure on this payload is computed on the server (DEC-002). <c>null</c> means
/// <b>unknown</b>, never zero (contract §9.2 rule 6).
/// </para>
/// </summary>
public class PackageCourseRulesetDto
{
    public int PackageId { get; set; }

    /// <summary><c>Package.Description</c>.</summary>
    public string PackageName { get; set; } = string.Empty;

    /// <summary><c>Package.PackageType</c>, verbatim (DEC-070).</summary>
    public string PackageType { get; set; } = string.Empty;

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
    public List<PackageCourseRuleDto> Rules { get; set; } = new();

    /// <summary>The server UTC timestamp of the payload.</summary>
    public DateTime GeneratedAt { get; set; }
}

/// <summary>One course-coverage rule of a tier (contract §13.1).</summary>
public class PackageCourseRuleDto
{
    public int PackageCourseRuleId { get; set; }

    public int CourseId { get; set; }

    /// <summary>The course's name.</summary>
    public string CourseName { get; set; } = string.Empty;

    /// <summary>The dishes the client must take from this course.</summary>
    public int RequiredQuota { get; set; }

    /// <summary>The standard portion per guest. <c>null</c> = not specified.</summary>
    public int? PortionGrams { get; set; }

    public string RuleDescription { get; set; } = string.Empty;

    /// <summary>The operator's inclusion label. <c>null</c> = no tag.</summary>
    public string? InclusionTag { get; set; }

    /// <summary>The allowed tier-1 upgrade. <c>null</c> = no upgrade.</summary>
    public int? UpgradeAddonId { get; set; }

    /// <summary>The add-on's description. <c>null</c> when the add-on is soft-deleted or absent.</summary>
    public string? UpgradeAddonDescription { get; set; }

    /// <summary>The add-on's unit price. <c>null</c> when the add-on is soft-deleted or absent.</summary>
    public decimal? UpgradeAddonUnitPrice { get; set; }

    /// <summary>The add-on's unit. <c>null</c> when the add-on is soft-deleted or absent.</summary>
    public string? UpgradeAddonUnit { get; set; }

    /// <summary>The non-soft-deleted dishes of the course.</summary>
    public int PoolSize { get; set; }

    /// <summary>The pool's dishes whose food cost is <b>greater than zero</b> (DEC-087).</summary>
    public int PoolCostedCount { get; set; }

    /// <summary>The mean food cost of the pool's costed dishes. <c>null</c> when none is costed.</summary>
    public decimal? PoolAverageFoodCost { get; set; }

    /// <summary>The pool's dishes.</summary>
    public List<PackageCoursePoolDishDto> Pool { get; set; } = new();
}

/// <summary>One dish of a rule's qualified pool (contract §13.1).</summary>
public class PackageCoursePoolDishDto
{
    public string MenuId { get; set; } = string.Empty;

    public string MenuName { get; set; } = string.Empty;

    /// <summary>The dish's food cost. <c>null</c> or zero means the dish is not costed (DEC-087).</summary>
    public decimal? FoodCost { get; set; }

    public decimal? Price { get; set; }
}

/// <summary>
/// The body of <c>PUT /api/packages/{id}/course-rules</c> (contract §13.2).
/// <para>
/// The <c>rules</c> member is <b>nullable</b> on purpose: an absent or explicitly null member is
/// a rejected payload (<c>400</c>), never an empty set — an empty set has the documented,
/// destructive meaning of clearing the tier's ruleset (DEC-085).
/// </para>
/// </summary>
public record PackageCourseRulesetRequest(
    IReadOnlyList<PackageCourseRuleRequestDto>? Rules = null);

/// <summary>One submitted rule (contract §13.2).</summary>
public record PackageCourseRuleRequestDto(
    int CourseId = 0,
    int RequiredQuota = 0,
    int? PortionGrams = null,
    string RuleDescription = "",
    string? InclusionTag = null,
    int? UpgradeAddonId = null);
