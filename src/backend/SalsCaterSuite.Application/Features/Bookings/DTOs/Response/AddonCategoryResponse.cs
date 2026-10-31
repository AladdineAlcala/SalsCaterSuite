using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>One row of the add-on category master (contract §16.2).</summary>
public class AddonCategoryResponse
{
    public int AddonCategoryId { get; set; }
    public string AddonCategoryName { get; set; } = string.Empty;
}
