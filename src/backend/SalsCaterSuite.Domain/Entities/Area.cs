namespace SalsCaterSuite.Domain.Entities;

public class Area
{
    public int AreaId { get; set; }
    public string AreaDetails { get; set; } = string.Empty;
    public decimal? AdditionalRate { get; set; }
}

