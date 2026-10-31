using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Addons.DTOs.Response;

/// <summary>One department of the read-only lookup the add-on form needs.</summary>
public class DepartmentResponse
{
    public int DeptId { get; set; }
    public string DeptName { get; set; } = string.Empty;
}
