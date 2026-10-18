namespace SalsCaterSuite.Domain.Common;

public abstract class BaseEntity
{
    public DateTime? CreatedDate { get; set; } = DateTime.UtcNow;
    public string? CreatedByUser { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public bool IsDeleted { get; set; } = false;
}

