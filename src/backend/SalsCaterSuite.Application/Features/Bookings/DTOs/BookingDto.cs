using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs;

/// <summary>
/// Read-only projection of a booking used by the details and list endpoints.
/// Financial totals are server-calculated; the frontend must treat them as authoritative.
/// </summary>
public class BookingDto
{
    public int TransactionId { get; set; }
    public string BookingNumber { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public string BookType { get; set; } = string.Empty;       // enum name, e.g. "Inside"
    public string TypeOfService { get; set; } = string.Empty;  // enum name, e.g. "Catering"
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerContact { get; set; }
    public string? Occasion { get; set; }
    public string? Venue { get; set; }
    public DateTime EventDate { get; set; }
    public TimeSpan EventStartTime { get; set; }
    public TimeSpan EventEndTime { get; set; }
    public int NumberOfHeads { get; set; }
    public string? EventColor { get; set; }
    public int? PackageId { get; set; }
    public string? PackageName { get; set; }
    public string? Reference { get; set; }

    // Price snapshot (frozen at creation)
    public decimal PricePerHead { get; set; }
    public decimal PackageAmount { get; set; }

    // Financial breakdown
    public decimal Subtotal { get; set; }
    public string DiscountType { get; set; } = "None";
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal AmountDue { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance { get; set; }

    // Status
    public string Status { get; set; } = "Pending";           // enum name
    public bool ReminderSent { get; set; }
}

/// <summary>
/// Request body for POST /api/bookings.
/// </summary>
public class CreateBookingRequestDto
{
    public int CustomerId { get; set; }
    public BookingType BookType { get; set; } = BookingType.Inside;
    public ServiceType TypeOfService { get; set; } = ServiceType.Catering;
    public int NoOfPerson { get; set; }
    public string Occasion { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? EventColor { get; set; }
    public int? PackageId { get; set; }
    public string? Reference { get; set; }
    public int? ExtendedAreaId { get; set; }
    public bool ApplyExtendedAmount { get; set; }
    public string? SpecialInstructions { get; set; }
    /// <summary>
    /// Optional override for the package price per head. When null the system
    /// reads AmountPax from the selected Package record at the time of creation.
    /// </summary>
    public decimal? OverridePricePerHead { get; set; }
}
