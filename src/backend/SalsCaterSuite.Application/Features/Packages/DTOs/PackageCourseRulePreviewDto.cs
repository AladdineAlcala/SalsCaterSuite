namespace SalsCaterSuite.Application.Features.Packages.DTOs;

/// <summary>
/// The body of <c>POST /api/packages/{id}/course-rules/preview</c> (contract §13.3).
/// <para>
/// The <c>selections</c> member is <b>nullable</b> on purpose: an absent or explicitly null
/// member is a rejected payload (<c>400</c>), never an empty selection.
/// </para>
/// </summary>
public record PackageCourseRulePreviewRequest(
    IReadOnlyList<PackageCourseSelectionDto>? Selections = null,
    decimal? StressPercentage = null,
    int? Pax = null);

/// <summary>One selected dish of one course (contract §13.3).</summary>
public record PackageCourseSelectionDto(
    int CourseId = 0,
    string MenuId = "");

/// <summary>
/// The selection simulator and the headcount forecast (contract §13.3, REQ-PKG-02).
/// <para>
/// A preview is a <b>what-if</b>: it persists nothing, it changes no price and it is not a
/// booking total (BR-20). The two verdicts are published separately — <see cref="IsQuotaCompliant"/>
/// is the quota verdict alone and <see cref="IsCompliant"/> is the overall verdict (DEC-080).
/// </para>
/// </summary>
public class PackageCourseRulePreviewDto
{
    public int PackageId { get; set; }

    /// <summary>The per-guest rate. <c>null</c> = not priced.</summary>
    public decimal? AmountPax { get; set; }

    /// <summary>The tier's headcount floor. <c>null</c> = not set.</summary>
    public int? MinPax { get; set; }

    /// <summary>The resolved headcount: the requested one, otherwise the tier's floor, otherwise <c>null</c>.</summary>
    public int? Pax { get; set; }

    /// <summary>The configured benchmark.</summary>
    public decimal TargetFoodCostPercentage { get; set; }

    /// <summary><c>AmountPax × TargetFoodCostPercentage ÷ 100</c>.</summary>
    public decimal? CostCeilingPerPax { get; set; }

    /// <summary>
    /// The sum of the selected dishes' food costs. <c>null</c> when nothing is selected, or when
    /// any selected dish is not costed (DEC-087).
    /// </summary>
    public decimal? SelectedFoodCostPerPax { get; set; }

    /// <summary>The selection's food cost, otherwise the standard run. <c>null</c> when neither is available.</summary>
    public decimal? FoodCostPerPax { get; set; }

    /// <summary><c>SelectedFoodCostPerPax ÷ AmountPax × 100</c>.</summary>
    public decimal? EffectiveFoodCostPercentage { get; set; }

    /// <summary><c>false</c> when the selected food cost is null (an uncosted dish, or no selection).</summary>
    public bool IsCostKnown { get; set; }

    /// <summary>A breach <b>warns</b>; it never reprices and never blocks (DEC-080).</summary>
    public bool IsCeilingBreached { get; set; }

    /// <summary><c>max(0, SelectedFoodCostPerPax − CostCeilingPerPax)</c>.</summary>
    public decimal? CeilingShortfallPerPax { get; set; }

    /// <summary><c>max(0, SelectedFoodCostPerPax − CostCeilingPerPax) ÷ CostCeilingPerPax × 100</c>.</summary>
    public decimal? CeilingOveragePercentage { get; set; }

    /// <summary>The echoed stress factor. <c>null</c> means no stress (a factor of 1).</summary>
    public decimal? StressPercentage { get; set; }

    /// <summary><c>SelectedFoodCostPerPax × (1 + StressPercentage ÷ 100)</c>.</summary>
    public decimal? StressedFoodCostPerPax { get; set; }

    /// <summary><c>StressedFoodCostPerPax ÷ AmountPax × 100</c>.</summary>
    public decimal? StressedFoodCostPercentage { get; set; }

    /// <summary><c>AmountPax × Pax</c>.</summary>
    public decimal? GrossRevenue { get; set; }

    /// <summary><c>FoodCostPerPax × Pax</c>.</summary>
    public decimal? FoodCostTotal { get; set; }

    /// <summary><c>GrossRevenue − FoodCostTotal</c>.</summary>
    public decimal? GrossProfit { get; set; }

    /// <summary><c>GrossProfit ÷ GrossRevenue × 100</c>.</summary>
    public decimal? GrossProfitPercentage { get; set; }

    /// <summary>The quota verdict alone: every rule is satisfied and no selection names an unruled course.</summary>
    public bool IsQuotaCompliant { get; set; }

    /// <summary>The overall verdict: the quota verdict <b>and</b> the absence of a ceiling breach (DEC-080).</summary>
    public bool IsCompliant { get; set; }

    /// <summary>One entry per rule, plus one per unruled course named by the selection.</summary>
    public List<PackageCourseComplianceDto> Compliance { get; set; } = new();

    /// <summary>The selected dishes and their food costs.</summary>
    public List<PackageCourseSelectionResultDto> Selections { get; set; } = new();

    /// <summary>The server UTC timestamp of the payload.</summary>
    public DateTime GeneratedAt { get; set; }
}

/// <summary>One compliance verdict (contract §13.3).</summary>
public class PackageCourseComplianceDto
{
    public int CourseId { get; set; }

    public string CourseName { get; set; } = string.Empty;

    /// <summary>The rule's required quota. <c>0</c> for an unruled course.</summary>
    public int RequiredQuota { get; set; }

    /// <summary>The number of selected dishes in this course.</summary>
    public int SelectedCount { get; set; }

    public bool IsCompliant { get; set; }

    /// <summary><c>false</c> when the selection names a course the tier holds no rule for.</summary>
    public bool HasRule { get; set; }
}

/// <summary>One selected dish of the preview (contract §13.3).</summary>
public class PackageCourseSelectionResultDto
{
    public int CourseId { get; set; }

    public string CourseName { get; set; } = string.Empty;

    public string MenuId { get; set; } = string.Empty;

    public string MenuName { get; set; } = string.Empty;

    /// <summary>The dish's food cost. <c>null</c> when the dish carries no food cost.</summary>
    public decimal? FoodCost { get; set; }
}
