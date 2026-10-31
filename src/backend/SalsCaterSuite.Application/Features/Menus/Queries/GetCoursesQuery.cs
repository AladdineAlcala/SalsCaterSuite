using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Common.Models;
using SalsCaterSuite.Application.Features.Menus.DTOs.Request;
using SalsCaterSuite.Application.Features.Menus.DTOs.Response;
using SalsCaterSuite.Application.Features.Menus.Models;

namespace SalsCaterSuite.Application.Features.Menus.Queries;

/// <summary>GET /api/menus/courses — list course categories.</summary>
public class GetCoursesQuery
{
    private readonly IMenuRepository _menus;

    public GetCoursesQuery(IMenuRepository menus)
    {
        _menus = menus;
    }

    public async Task<Result<List<CourseResponse>>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var courses = await _menus.GetCourseProjectionsAsync(cancellationToken);

        return Result<List<CourseResponse>>.Success(courses);
    }
}
