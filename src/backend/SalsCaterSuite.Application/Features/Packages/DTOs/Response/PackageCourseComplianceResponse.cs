namespace SalsCaterSuite.Application.Features.Packages.DTOs.Response;

/// <summary>One compliance verdict (contract §13.3).</summary>
public class PackageCourseComplianceResponse
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
