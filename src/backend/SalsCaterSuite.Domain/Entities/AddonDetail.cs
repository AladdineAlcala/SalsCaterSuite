namespace SalsCaterSuite.Domain.Entities;

public class AddonDetail
{
    public int AddonId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public string? Unit { get; set; }
    public bool IsActive { get; set; } = true;
}

