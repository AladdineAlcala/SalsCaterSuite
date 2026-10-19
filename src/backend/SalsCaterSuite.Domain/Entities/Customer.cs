using SalsCaterSuite.Domain.Common;

namespace SalsCaterSuite.Domain.Entities;

public class Customer : BaseEntity
{
    public int CustomerId { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string? Middle { get; set; }
    public string? Address { get; set; }
    public string? Contact1 { get; set; }
    public string? Contact2 { get; set; }
    public DateTime? DateRegistered { get; set; } = DateTime.UtcNow;
    public string? Company { get; set; }

    public string FullName => string.IsNullOrWhiteSpace(Middle)
        ? $"{LastName}, {FirstName}"
        : $"{LastName}, {FirstName} {Middle}.";

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}

