namespace SalsCaterSuite.Application.Features.Packages.DTOs.Response;

/// <summary>One course-coverage rule of a tier (contract §13.1).</summary>
public class PackageCourseRuleResponse
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

    /// <summary>The non-soft-deleted dishes of the course.</summary>
    public int PoolSize { get; set; }

    /// <summary>The pool's dishes whose food cost is <b>greater than zero</b> (DEC-087).</summary>
    public int PoolCostedCount { get; set; }

    /// <summary>The mean food cost of the pool's costed dishes. <c>null</c> when none is costed.</summary>
    public decimal? PoolAverageFoodCost { get; set; }

    /// <summary>The pool's dishes.</summary>
    public List<PackageCoursePoolDishResponse> Pool { get; set; } = new();
}
