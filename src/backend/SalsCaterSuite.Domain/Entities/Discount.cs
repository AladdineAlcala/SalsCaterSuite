using SalsCaterSuite.Domain.Common;

namespace SalsCaterSuite.Domain.Entities;

public class Discount : BaseEntity
{
    public int DiscountId { get; set; }
    public string DiscountCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Percentage { get; set; }
    public decimal? FixedAmount { get; set; }
    public bool IsActive { get; set; } = true;
}

