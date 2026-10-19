using SalsCaterSuite.Domain.Common;

namespace SalsCaterSuite.Domain.Entities;

public class Area : BaseEntity
{
    public int AreaId { get; set; }
    public string AreaDetails { get; set; } = string.Empty;
    public decimal? AdditionalRate { get; set; }
}

