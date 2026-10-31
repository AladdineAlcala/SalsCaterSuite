namespace SalsCaterSuite.Application.Features.Packages.DTOs.Request;

/// <summary>One selected dish of one course (contract §13.3).</summary>
public record PackageCourseSelectionRequest(
    int CourseId = 0,
    string MenuId = "");
