using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Request;

/// <summary>
/// The workbench quote request (contract §14.3).
/// <para>
/// The quote is a <b>what-if</b>: it prices a proposed booking and persists nothing (BR-31).
/// </para>
/// </summary>
public class BookingQuoteRequest
{
    /// <summary>The client.</summary>
    public int CustomerId { get; set; }

    /// <summary>The tier.</summary>
    public int PackageId { get; set; }

    /// <summary>The headcount.</summary>
    public int NoOfPerson { get; set; }

    /// <summary>The occasion.</summary>
    public string Occasion { get; set; } = string.Empty;

    /// <summary>The venue.</summary>
    public string Venue { get; set; } = string.Empty;

    /// <summary>The event date.</summary>
    public DateOnly EventDate { get; set; }

    /// <summary>The programme's start time.</summary>
    public TimeSpan StartTime { get; set; }

    /// <summary>The service's end time.</summary>
    public TimeSpan EndTime { get; set; }

    /// <summary>The kitchen call time.</summary>
    public TimeSpan? CallTime { get; set; }

    /// <summary>The pack-out time.</summary>
    public TimeSpan? PackoutTime { get; set; }

    /// <summary>The municipality.</summary>
    public int? ExtendedAreaId { get; set; }

    /// <summary>The operator's creation-time switch for the out-of-town surcharge.</summary>
    public bool ApplyExtendedAmount { get; set; }

    /// <summary>The discount code to resolve.</summary>
    public string? DiscountCode { get; set; }

    /// <summary>The deposit the operator stated.</summary>
    public decimal? DepositAmount { get; set; }

    /// <summary>The initial payment the operator intends to record.</summary>
    public decimal? InitialPaymentAmount { get; set; }

    /// <summary>
    /// The proposed dish selection, in exactly the create request's shape (contract §15.3).
    /// The quote prices it through the same code path the create uses (BR-31, DEC-153).
    /// </summary>
    public List<BookingMenuSelectionEntryRequest>? Selections { get; set; }

    /// <summary>
    /// The proposed add-on lines, in exactly the create request's shape (contract §16.4).
    /// </summary>
    public List<BookingAddonRequest>? Addons { get; set; }
}
