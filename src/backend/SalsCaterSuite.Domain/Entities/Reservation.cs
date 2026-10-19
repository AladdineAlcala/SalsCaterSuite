using SalsCaterSuite.Domain.Common;

namespace SalsCaterSuite.Domain.Entities;

public class Reservation : BaseEntity
{
    public int ReservationId { get; set; }
    public int CustomerId { get; set; }
    public DateTime ReservationDate { get; set; }
    public int NoOfPax { get; set; }
    public string Occasion { get; set; } = string.Empty;
    public bool ReserveStatus { get; set; }
    public string? EventVenue { get; set; }

    public Customer? Customer { get; set; }
}

