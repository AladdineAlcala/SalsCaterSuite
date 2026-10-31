using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Application.Features.Bookings.DTOs.Response;

/// <summary>One pre-dispatch check (BR-29).</summary>
public class BookingQuoteCheckResponse
{
    /// <summary>The check's key — <c>ClientValidated</c>, <c>TierActive</c>,
    /// <c>CoursesSpecified</c>, <c>MunicipalityCovered</c> or <c>DepositMatched</c>.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The server-provided display label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Whether the check passes.</summary>
    public bool IsSatisfied { get; set; }

    /// <summary>Whether the check blocks. Only <c>TierActive</c> ever does.</summary>
    public bool IsBlocking { get; set; }

    /// <summary>The server-provided detail line.</summary>
    public string? Detail { get; set; }
}
