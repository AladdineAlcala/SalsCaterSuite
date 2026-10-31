using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Dashboard.DTOs.Response;

/// <summary>One entry of the upcoming banquet schedule.</summary>
public record DashboardUpcomingBookingResponse
{
    public required int TransactionId { get; init; }

    /// <summary>The contractual booking number (BR-09, DEC-016).</summary>
    public required string BookingNumber { get; init; }

    public required string CustomerName { get; init; }

    public required string Occasion { get; init; }

    public required DateOnly EventDate { get; init; }

    public required int NumberOfHeads { get; init; }

    /// <summary>The persisted BR-10 lifecycle status, serialised as its string name.</summary>
    public required BookingStatus Status { get; init; }

    /// <summary>The frozen package name; <c>null</c> for a bespoke booking.</summary>
    public required string? PackageName { get; init; }

    /// <summary>The same net-due path the ledger uses (DEC-042).</summary>
    public required decimal NetAmountDue { get; init; }

    /// <summary>Σ payment amount (authoritative, DEC-012).</summary>
    public required decimal AmountPaid { get; init; }

    /// <summary><c>max(0, net amount due − amount paid)</c>.</summary>
    public required decimal Balance { get; init; }
}
